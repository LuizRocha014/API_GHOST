using System.Text.Json;
using Api.Agenda;
using Application.Bot;
using Domain.Agenda;
using Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

/// <summary>
/// Webhook da WhatsApp Cloud API. Cadastre a URL pública <c>https://…/api/whatsapp/webhook</c> no app da Meta
/// (produto WhatsApp → Configuração) com o mesmo <c>WhatsApp:VerifyToken</c> e assine o campo <c>messages</c>.
/// </summary>
[ApiController]
[Route("api/whatsapp/webhook")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class WhatsAppWebhookController(
    IBotStore store,
    BotSignal signal,
    IOptions<WhatsAppOptions> options,
    IHostEnvironment environment,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 1024 * 1024;

    /// <summary>Verificação da Meta: devolve o hub.challenge quando o token confere.</summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" && challenge is not null && WebhookSignature.SameToken(token, options.Value.VerifyToken))
            return Content(challenge, "text/plain");
        logger.LogWarning("Webhook: verificação recusada (modo {Mode}).", mode);
        return StatusCode(StatusCodes.Status403Forbidden);
    }

    /// <summary>Mensagens e status de entrega: valida a assinatura, enfileira e responde 200 na hora.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();

        var secret = options.Value.AppSecret;
        if (string.IsNullOrEmpty(secret))
        {
            // Sem a chave do app não há como saber se veio da Meta: só aceitável no desenvolvimento.
            if (!environment.IsDevelopment())
            {
                logger.LogError("Webhook: WhatsApp:AppSecret não configurado; mensagem recusada.");
                return Unauthorized();
            }
        }
        else if (!WebhookSignature.IsValid(body, Request.Headers[WebhookSignature.Header], secret))
        {
            logger.LogWarning("Webhook: assinatura inválida, mensagem recusada.");
            return Unauthorized();
        }

        ParsedWebhook parsed;
        try
        {
            using var doc = JsonDocument.Parse(body);
            parsed = WebhookParser.Parse(doc.RootElement);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        // Grava tudo na fila do banco antes do 200: se a API cair depois, nada se perde. Banco fora do ar = 503 e a Meta
        // reenvia o webhook mais tarde (o mesmo id não entra duas vezes).
        var now = DateTime.UtcNow;
        var queued = 0;
        try
        {
            foreach (var m in parsed.Messages)
            {
                var branchId = options.Value.BranchFor(m.PhoneNumberId);
                if (branchId is null)
                {
                    logger.LogWarning("Webhook: número {PhoneNumberId} sem filial em WhatsApp:Numbers; mensagem ignorada.", m.PhoneNumberId);
                    continue;
                }
                if (await store.EnqueueAsync(BotQueuePayload.ForMessage(branchId.Value, m.Message, now), ct)) queued++;
            }

            foreach (var s in parsed.Statuses)
            {
                // "sent" já é o estado gravado no envio; só entregue, lida e falha mudam o lembrete.
                if (s.Status.Status is not (ReminderStatuses.Delivered or ReminderStatuses.Read or ReminderStatuses.Failed)) continue;
                var branchId = options.Value.BranchFor(s.PhoneNumberId);
                if (branchId is null) continue;
                if (await store.EnqueueAsync(BotQueuePayload.ForStatus(branchId.Value, s.Status, now), ct)) queued++;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Webhook: não foi possível gravar na fila; a Meta vai reenviar.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (queued > 0) signal.Ring();
        return Ok();
    }
}
