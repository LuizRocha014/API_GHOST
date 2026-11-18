<#
Teste da fila persistente do webhook: grava um item "pendente" e um "processando" com prazo vencido (como se a API
tivesse caído no meio) direto no banco, sobe a API e confere que o bot respondeu os dois.

Uso (com a API PARADA e o SQL Server Express local):
  pwsh infra/e2e/fila_reinicio_e2e.ps1            # grava os itens
  (suba a API: dotnet run --project src/Api --launch-profile rede)
  pwsh infra/e2e/fila_reinicio_e2e.ps1 -Conferir  # confere as respostas e apaga a conversa de teste
#>
param(
    [switch]$Conferir,
    [string]$Banco = 'Server=localhost,1433;Database=AgendaGhost;Trusted_Connection=True;TrustServerCertificate=True'
)

$phones = '5511990000401', '5511990000402'
$conn = New-Object System.Data.SqlClient.SqlConnection $Banco
$conn.Open()

function Exec([string]$sql, [hashtable]$p = @{}) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    foreach ($k in $p.Keys) { [void]$cmd.Parameters.AddWithValue($k, $p[$k]) }
    return $cmd.ExecuteScalar()
}

# Filial do número 0 do WhatsApp = a primeira filial (a mesma do bot_e2e).
$branch = Exec "SELECT TOP 1 Id FROM Branches WHERE Active = 1 ORDER BY Name"

if (-not $Conferir) {
    foreach ($i in 0, 1) {
        $phone = $phones[$i]
        Exec "DELETE FROM BotMessages WHERE Phone = @p; DELETE FROM BotSessions WHERE Phone = @p" @{ '@p' = $phone } | Out-Null
        $id = "wamid.reinicio.$([guid]::NewGuid().ToString('N'))"
        $payload = @{ id = $id; phone = $phone; timestamp = (Get-Date).ToUniversalTime().ToString('o'); kind = 'text'; text = 'oi' } | ConvertTo-Json -Compress
        $status = if ($i -eq 0) { 'pending' } else { 'processing' }
        $attempts = if ($i -eq 0) { 0 } else { 1 }
        Exec @"
INSERT INTO BotQueueItems (Id, BranchId, Kind, ExternalId, PayloadJson, Status, Attempts, ReceivedAt, NextAttemptAt)
VALUES (NEWID(), @b, 'message', @e, @j, @s, @a, SYSUTCDATETIME(), DATEADD(SECOND, -5, SYSUTCDATETIME()))
"@ @{ '@b' = $branch; '@e' = $id; '@j' = $payload; '@s' = $status; '@a' = $attempts } | Out-Null
        "gravado: $phone ($status)"
    }
    $conn.Close()
    exit 0
}

$ok = 0
foreach ($phone in $phones) {
    $out = Exec "SELECT COUNT(1) FROM BotMessages WHERE Phone = @p AND Direction = 'out'" @{ '@p' = $phone }
    $left = Exec "SELECT COUNT(1) FROM BotQueueItems WHERE ExternalId LIKE 'wamid.reinicio.%' AND PayloadJson LIKE @l AND Status <> 'done'" @{ '@l' = "%$phone%" }
    if ($out -ge 1 -and $left -eq 0) { "OK   $phone respondido depois do reinício"; $ok++ } else { "FAIL $phone (respostas: $out, na fila: $left)" }
    Exec "DELETE FROM BotMessages WHERE Phone = @p; DELETE FROM BotSessions WHERE Phone = @p" @{ '@p' = $phone } | Out-Null
}
$conn.Close()
"`n$ok/$($phones.Count) verificações OK"
exit ($(if ($ok -eq $phones.Count) { 0 } else { 1 }))
