<#
.SYNOPSIS
  Instala e prepara o SQL Server 2022 Express para a API_GHOST (local ou numa VM na nuvem).

.DESCRIPTION
  1. Baixa o instalador oficial (winget; se não houver, o link da Microsoft com conferência de SHA256).
  2. Instala a instância (padrão SQLEXPRESS) em modo misto (Windows + login SQL), sem interação.
  3. Fixa a porta TCP (padrão 1433) e liga o SQL Browser, para outras máquinas conectarem.
  4. Abre o firewall do Windows na porta.
  5. Cria o banco (padrão AgendaGhost) e um login SQL só da API (padrão ghost_api) como db_owner.
  6. Grava as connection strings em %USERPROFILE%\.api_ghost\sqlexpress.txt (fora do repositório).

  Pode rodar de novo: o que já existe é mantido (instância, banco, login).
  Precisa de administrador; se não estiver, o script pede elevação (UAC).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\instalar-sqlexpress.ps1
  powershell -ExecutionPolicy Bypass -File .\instalar-sqlexpress.ps1 -Port 1433 -ApiPassword 'MinhaSenha#2026'
#>
param(
  [string]$Instance = 'SQLEXPRESS',
  [int]$Port = 1433,
  [string]$Database = 'AgendaGhost',
  [string]$ApiLogin = 'ghost_api',
  [string]$ApiPassword = '',
  # Caminho de um SQLEXPR_x64_ENU.exe já baixado (pula o download).
  [string]$MediaPath = '',
  [switch]$NoFirewall,
  [switch]$NoPause
)

$ErrorActionPreference = 'Stop'

function Test-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  return ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
  Write-Host 'Pedindo permissão de administrador (UAC)...'
  $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Instance', $Instance, '-Port', $Port,
    '-Database', $Database, '-ApiLogin', $ApiLogin)
  if ($ApiPassword) { $argList += @('-ApiPassword', "`"$ApiPassword`"") }
  if ($MediaPath) { $argList += @('-MediaPath', "`"$MediaPath`"") }
  if ($NoFirewall) { $argList += '-NoFirewall' }
  if ($NoPause) { $argList += '-NoPause' }
  Start-Process powershell -Verb RunAs -ArgumentList $argList -Wait
  exit
}

function New-StrongPassword([int]$length = 20) {
  $sets = @('ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789', '#%+=_-')
  $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
  $bytes = New-Object byte[] ($length)
  $rng.GetBytes($bytes)
  $chars = for ($i = 0; $i -lt $length; $i++) { $set = $sets[$i % $sets.Length]; $set[$bytes[$i] % $set.Length] }
  return -join $chars
}

$work = Join-Path $env:ProgramData 'API_GHOST\sqlexpress'
$infoDir = Join-Path $env:USERPROFILE '.api_ghost'
New-Item -ItemType Directory -Force $work, $infoDir | Out-Null
Start-Transcript -Path (Join-Path $work 'instalacao.log') -Append | Out-Null

try {
  $serviceName = if ($Instance -eq 'MSSQLSERVER') { 'MSSQLSERVER' } else { "MSSQL`$$Instance" }
  $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

  if ($service) {
    Write-Host "Instância $Instance já instalada ($serviceName). Pulando a instalação."
    $saPassword = $null
  }
  else {
    # ---------- 1. Mídia de instalação ----------
    # Link direto da mídia do SQL Server 2022 Express (Core). O instalador "web" (SSEI) falha em
    # redes com filtro (código 1011), por isso ele fica só como alternativa.
    $mediaUrl = 'https://download.microsoft.com/download/3/8/d/38de7036-2433-4207-8eae-06e247e17b25/SQLEXPR_x64_ENU.exe'
    $media = $null
    if ($MediaPath -and (Test-Path $MediaPath)) { $media = Get-Item $MediaPath }
    if (-not $media) { $media = Get-ChildItem $work -Filter 'SQLEXPR*_x64_*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1 }
    if (-not $media) {
      Write-Host 'Baixando a mídia do SQL Server 2022 Express (~270 MB)...'
      $target = Join-Path $work 'SQLEXPR_x64_ENU.exe'
      $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
      if ($curl) {
        & curl.exe -sSL --fail -o $target $mediaUrl
      }
      else {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $mediaUrl -OutFile $target -UseBasicParsing
      }
      if (Test-Path $target) { $media = Get-Item $target }
    }
    if (-not $media) {
      Write-Host 'Link direto indisponível; tentando o instalador web (SSEI)...'
      $dl = Join-Path $work 'winget'
      & winget download --id Microsoft.SQLServer.2022.Express --source winget --download-directory $dl --accept-package-agreements --accept-source-agreements | Out-Host
      $ssei = Get-ChildItem $dl -Filter *.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
      if (-not $ssei) { throw 'Não consegui baixar o instalador do SQL Server.' }
      $p = Start-Process $ssei.FullName -ArgumentList '/ACTION=Download', "/MEDIAPATH=$work", '/MEDIATYPE=Core', '/QUIET' -Wait -PassThru
      if ($p.ExitCode -ne 0) { throw "O download da mídia falhou (código $($p.ExitCode))." }
      $media = Get-ChildItem $work -Filter 'SQLEXPR*_x64_*.exe' | Select-Object -First 1
    }
    # Só executa binário assinado pela Microsoft.
    $sig = Get-AuthenticodeSignature $media.FullName
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
      throw "Assinatura digital inválida em $($media.FullName) ($($sig.Status)). Abortando."
    }
    Write-Host "Mídia: $($media.FullName) (versão $($media.VersionInfo.ProductVersion), assinada pela Microsoft)"

    $setupDir = Join-Path $work 'setup'
    if (-not (Test-Path (Join-Path $setupDir 'setup.exe'))) {
      Write-Host 'Extraindo a mídia...'
      $p = Start-Process $media.FullName -ArgumentList '/q', "/x:`"$setupDir`"" -Wait -PassThru
      if ($p.ExitCode -ne 0) { throw "A extração falhou (código $($p.ExitCode))." }
    }

    # ---------- 2. Instalação silenciosa ----------
    $saPassword = New-StrongPassword 20
    $admin = "$env:USERDOMAIN\$env:USERNAME"
    Write-Host "Instalando a instância $Instance (alguns minutos)..."
    $setupArgs = @(
      '/Q', '/ACTION=Install', '/IACCEPTSQLSERVERLICENSETERMS', '/FEATURES=SQLEngine',
      "/INSTANCENAME=$Instance", "/SQLSYSADMINACCOUNTS=`"$admin`"", '/SECURITYMODE=SQL', "/SAPWD=`"$saPassword`"",
      '/TCPENABLED=1', '/NPENABLED=0', '/BROWSERSVCSTARTUPTYPE=Automatic', '/UPDATEENABLED=0'
    )
    $p = Start-Process (Join-Path $setupDir 'setup.exe') -ArgumentList $setupArgs -Wait -PassThru
    if ($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010) {
      throw "A instalação falhou (código $($p.ExitCode)). Veja o log em C:\Program Files\Microsoft SQL Server\160\Setup Bootstrap\Log."
    }
    if ($p.ExitCode -eq 3010) { Write-Warning 'Instalado. O Windows pede reinicialização para concluir algumas partes.' }
  }

  # ---------- 3. Porta TCP fixa ----------
  $instKey = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL').$Instance
  if (-not $instKey) { throw "Instância $Instance não encontrada no registro." }
  $tcp = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instKey\MSSQLServer\SuperSocketNetLib\Tcp"
  Set-ItemProperty $tcp -Name Enabled -Value 1
  Set-ItemProperty "$tcp\IPAll" -Name TcpDynamicPorts -Value ''
  Set-ItemProperty "$tcp\IPAll" -Name TcpPort -Value "$Port"
  # Modo misto (Windows + SQL), caso a instância já existisse só com Windows.
  Set-ItemProperty "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instKey\MSSQLServer" -Name LoginMode -Value 2

  Write-Host 'Reiniciando o serviço...'
  Restart-Service -Name $serviceName -Force
  Set-Service -Name 'SQLBrowser' -StartupType Automatic -ErrorAction SilentlyContinue
  Start-Service -Name 'SQLBrowser' -ErrorAction SilentlyContinue

  # ---------- 4. Firewall ----------
  if (-not $NoFirewall) {
    if (-not (Get-NetFirewallRule -DisplayName "SQL Server $Instance TCP $Port" -ErrorAction SilentlyContinue)) {
      New-NetFirewallRule -DisplayName "SQL Server $Instance TCP $Port" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
    }
    if (-not (Get-NetFirewallRule -DisplayName 'SQL Server Browser UDP 1434' -ErrorAction SilentlyContinue)) {
      New-NetFirewallRule -DisplayName 'SQL Server Browser UDP 1434' -Direction Inbound -Protocol UDP -LocalPort 1434 -Action Allow | Out-Null
    }
  }

  # ---------- 5. Banco e login da API ----------
  $cs = "Server=localhost,$Port;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
  $conn = $null
  for ($i = 0; $i -lt 30 -and -not $conn; $i++) {
    try { $c = New-Object System.Data.SqlClient.SqlConnection $cs; $c.Open(); $conn = $c } catch { Start-Sleep -Seconds 2 }
  }
  if (-not $conn) { throw "Não consegui conectar em localhost,$Port depois de reiniciar o serviço." }

  if (-not $ApiPassword) {
    $existing = Join-Path $infoDir 'sqlexpress.txt'
    if (Test-Path $existing) {
      $line = Select-String -Path $existing -Pattern '^ApiPassword=(.+)$' | Select-Object -First 1
      if ($line) { $ApiPassword = $line.Matches[0].Groups[1].Value }
    }
    if (-not $ApiPassword) { $ApiPassword = New-StrongPassword 20 }
  }
  $pwdSql = $ApiPassword.Replace("'", "''")
  $sql = @"
IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];
IF SUSER_ID(N'$ApiLogin') IS NULL
  CREATE LOGIN [$ApiLogin] WITH PASSWORD = N'$pwdSql', DEFAULT_DATABASE = [$Database], CHECK_POLICY = OFF;
ELSE
  ALTER LOGIN [$ApiLogin] WITH PASSWORD = N'$pwdSql';
"@
  $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; [void]$cmd.ExecuteNonQuery()
  $cmd.CommandText = @"
USE [$Database];
IF USER_ID(N'$ApiLogin') IS NULL CREATE USER [$ApiLogin] FOR LOGIN [$ApiLogin];
ALTER ROLE db_owner ADD MEMBER [$ApiLogin];
"@
  [void]$cmd.ExecuteNonQuery()
  $cmd.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(100))"
  $version = $cmd.ExecuteScalar()
  $conn.Close()

  # ---------- 6. Connection strings ----------
  $hostName = $env:COMPUTERNAME
  $ips = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' }).IPAddress -join ', '
  $local = "Server=localhost,$Port;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  $remote = "Server=$hostName,$Port;Database=$Database;User Id=$ApiLogin;Password=$ApiPassword;TrustServerCertificate=True;MultipleActiveResultSets=true"
  $info = @"
# SQL Server Express para a API_GHOST — gerado em $(Get-Date -Format 'yyyy-MM-dd HH:mm')
# NÃO versionar este arquivo (tem senha).
Versao=$version
Instancia=$Instance
Porta=$Port
Banco=$Database
IPs=$ips
ApiLogin=$ApiLogin
ApiPassword=$ApiPassword
$(if ($saPassword) { "SaPassword=$saPassword" })

# Na própria máquina (autenticação do Windows, sem senha):
ConnectionStrings__Express=$local

# De outra máquina (troque $hostName pelo IP se o nome não resolver):
ConnectionStrings__Express=$remote
"@
  Set-Content -Path (Join-Path $infoDir 'sqlexpress.txt') -Value $info -Encoding UTF8

  Write-Host ''
  Write-Host "Pronto: $version" -ForegroundColor Green
  Write-Host "Banco $Database em localhost,$Port. Login da API: $ApiLogin"
  Write-Host "Connection strings e senhas em: $(Join-Path $infoDir 'sqlexpress.txt')"
  Write-Host 'RESULTADO=OK'
}
catch {
  Write-Host "ERRO: $($_.Exception.Message)" -ForegroundColor Red
  Write-Host 'RESULTADO=ERRO'
  $global:LASTEXITCODE = 1
}
finally {
  Stop-Transcript | Out-Null
  if (-not $NoPause) { Read-Host 'Pressione Enter para fechar' | Out-Null }
}
