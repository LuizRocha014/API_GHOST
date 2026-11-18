<#
.SYNOPSIS
  Gera o app web (Flutter) e copia para deploy/web, de onde a API serve o site.

.EXAMPLE
  .\publicar-web.ps1
  .\publicar-web.ps1 -App C:\DEV\personal\agenda_estetica\app

.NOTES
  Depois, no servidor: copie a pasta deploy/web (scp/rsync) e rode `docker compose up -d` (não precisa rebuild da API:
  a pasta é montada no container). O app no navegador usa o próprio endereço do site como servidor.
#>
param(
    [string]$App = (Join-Path $PSScriptRoot '..\..\agenda_estetica\app')
)
$ErrorActionPreference = 'Stop'

$App = (Resolve-Path $App).Path
if (-not (Test-Path (Join-Path $App 'pubspec.yaml'))) { throw "Não achei o app Flutter em $App (use -App <pasta do app>)." }
$destino = Join-Path $PSScriptRoot 'web'

Push-Location $App
try {
    $flutter = if (Get-Command fvm -ErrorAction SilentlyContinue) { @('fvm', 'flutter') } else { @('flutter') }
    Write-Host "Gerando o app web em $App ..."
    & $flutter[0] @($flutter[1..($flutter.Length - 1)] + @('build', 'web', '--release'))
    if ($LASTEXITCODE -ne 0) { throw "flutter build web falhou ($LASTEXITCODE)." }
}
finally {
    Pop-Location
}

$origem = Join-Path $App 'build\web'
if (-not (Test-Path (Join-Path $origem 'index.html'))) { throw "Build sem index.html em $origem." }

New-Item -ItemType Directory -Force $destino | Out-Null
# Troca o conteúdo (menos o LEIA-ME): arquivos de versões antigas não ficam para trás.
Get-ChildItem $destino -Force | Where-Object { $_.Name -ne 'LEIA-ME.txt' } | Remove-Item -Recurse -Force
Copy-Item (Join-Path $origem '*') $destino -Recurse -Force
Write-Host "App web copiado para $destino"
