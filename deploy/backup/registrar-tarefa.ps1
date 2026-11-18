<#
.SYNOPSIS
  Agenda o backup diário do banco no Agendador de Tarefas do Windows (rode como administrador).

.EXAMPLE
  .\registrar-tarefa.ps1 -Hora '23:30' -CopiarPara "$env:USERPROFILE\OneDrive\Backups\Agenda"

.NOTES
  A tarefa roda como SYSTEM (funciona com o computador sem ninguém logado) e chama backup-sqlserver.ps1 desta pasta.
  Para tirar: Unregister-ScheduledTask -TaskName 'Agenda - backup do banco'
#>
param(
    [string]$Hora = '23:30',
    [string]$Servidor = 'localhost,1433',
    [string]$Banco = 'AgendaGhost',
    [string]$CopiarPara = '',
    [int]$ManterDias = 14
)
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'backup-sqlserver.ps1'
$log = Join-Path $PSScriptRoot 'backup.log'
$argumentos = "-NoProfile -ExecutionPolicy Bypass -Command `"& '$script' -Servidor '$Servidor' -Banco '$Banco' -ManterDias $ManterDias" +
    $(if ($CopiarPara) { " -CopiarPara '$CopiarPara'" } else { '' }) + " *>> '$log'`""

$acao = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argumentos
$gatilho = New-ScheduledTaskTrigger -Daily -At $Hora
$conta = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$config = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2)
Register-ScheduledTask -TaskName 'Agenda - backup do banco' -Action $acao -Trigger $gatilho -Principal $conta -Settings $config -Force |
    Out-Null
Write-Output "Tarefa 'Agenda - backup do banco' agendada para todo dia às $Hora. Log: $log"
Write-Output 'A conta SYSTEM precisa de login no SQL Server: veja docs/deploy.md (backup no Windows).'
