<#
.SYNOPSIS
  Backup completo do banco da Agenda (SQL Server ou SQL Express no Windows), conferido e com retenção.

.DESCRIPTION
  Faz BACKUP DATABASE com CHECKSUM, confere o arquivo (RESTORE VERIFYONLY), copia para uma segunda pasta (OneDrive,
  Google Drive, HD externo: backup que fica só no mesmo disco não salva de defeito no disco) e apaga os backups deste
  banco mais velhos que -ManterDias nas duas pastas. Funciona no PowerShell 5.1, sem instalar nada.

.PARAMETER Pasta
  Onde o SQL Server grava o .bak. Vazio = pasta padrão de backup da instância. A conta do serviço do SQL Server
  precisa poder escrever nela.

.PARAMETER CopiarPara
  Segunda cópia (recomendado). Ex.: "$env:USERPROFILE\OneDrive\Backups\Agenda".

.EXAMPLE
  .\backup-sqlserver.ps1
  .\backup-sqlserver.ps1 -Servidor 'localhost\SQLEXPRESS' -CopiarPara 'D:\Backups\Agenda' -ManterDias 30

.NOTES
  Para rodar todo dia: .\registrar-tarefa.ps1 (como administrador). Restaurar: veja docs/deploy.md.
#>
param(
    [string]$Servidor = 'localhost,1433',
    [string]$Banco = 'AgendaGhost',
    [string]$Pasta = '',
    [string]$CopiarPara = '',
    [int]$ManterDias = 14,
    # Vazio = autenticação do Windows (a conta que roda o script precisa de permissão de backup no banco).
    [string]$Usuario = '',
    [string]$Senha = ''
)
$ErrorActionPreference = 'Stop'

$auth = if ($Usuario) { "User Id=$Usuario;Password=$Senha;" } else { 'Integrated Security=True;' }
$conn = New-Object System.Data.SqlClient.SqlConnection "Server=$Servidor;Database=master;TrustServerCertificate=True;Connect Timeout=30;$auth"
$conn.Open()
try {
    function Run([string]$sql, [hashtable]$params = @{}, [switch]$Scalar) {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $cmd.CommandTimeout = 0
        foreach ($k in $params.Keys) { [void]$cmd.Parameters.AddWithValue($k, $params[$k]) }
        if ($Scalar) { return $cmd.ExecuteScalar() }
        [void]$cmd.ExecuteNonQuery()
    }

    if ((Run 'SELECT ISNULL(DB_ID(@b), 0)' @{ '@b' = $Banco } -Scalar) -eq 0) { throw "O banco '$Banco' não existe em $Servidor." }
    if (-not $Pasta) { $Pasta = [string](Run "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))" -Scalar) }
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $arquivo = Join-Path $Pasta "$Banco-$stamp.bak"

    $inicio = Get-Date
    # Nome do banco não aceita parâmetro no BACKUP: QUOTENAME evita injeção pelo -Banco.
    $nome = [string](Run 'SELECT QUOTENAME(@b)' @{ '@b' = $Banco } -Scalar)
    Run "BACKUP DATABASE $nome TO DISK = @f WITH INIT, CHECKSUM, NAME = @n" @{ '@f' = $arquivo; '@n' = "Agenda $stamp" }
    Run 'RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM' @{ '@f' = $arquivo }
    Write-Output ("Backup conferido: {0} ({1:N0} s)" -f $arquivo, ((Get-Date) - $inicio).TotalSeconds)
}
finally {
    $conn.Close()
}

function Limpar([string]$dir) {
    if (-not (Test-Path $dir)) { return }
    Get-ChildItem $dir -Filter "$Banco-*.bak" -File |
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$ManterDias) } |
        ForEach-Object { Remove-Item $_.FullName -Force; Write-Output "Apagado (mais de $ManterDias dias): $($_.Name)" }
}

if ($CopiarPara) {
    New-Item -ItemType Directory -Force $CopiarPara | Out-Null
    try {
        Copy-Item $arquivo $CopiarPara -Force
    }
    catch [System.UnauthorizedAccessException] {
        throw ("O backup foi feito em $arquivo, mas esta conta não consegue ler a pasta de backup do SQL Server para copiar. " +
            'Rode como administrador, pela tarefa agendada (registrar-tarefa.ps1, roda como SYSTEM) ou use -Pasta numa pasta ' +
            'que o serviço do SQL Server e você possam acessar.')
    }
    Write-Output "Cópia: $(Join-Path $CopiarPara (Split-Path $arquivo -Leaf))"
    Limpar $CopiarPara
}
if ($ManterDias -gt 0) { Limpar $Pasta }
