using Application.Abstractions;
using Application.Agenda;
using Domain.Agenda;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Agenda;

/// <summary>
/// Relógio no fuso da estética (padrão America/Sao_Paulo; configure em Agenda:TimeZone).
/// A API pode rodar num servidor em UTC e mesmo assim "hoje" e "agora" saem certos.
/// </summary>
public sealed class AgendaClock : IAgendaClock
{
    private readonly TimeZoneInfo _zone;

    public AgendaClock(IConfiguration configuration)
    {
        var id = configuration["Agenda:TimeZone"] ?? "America/Sao_Paulo";
        _zone = TryFind(id) ?? TryFind("E. South America Standard Time") ?? TimeZoneInfo.Local;
    }

    private static TimeZoneInfo? TryFind(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    public DateTime NowLocal => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone), DateTimeKind.Unspecified);
}

/// <summary>
/// Envio do lembrete sem WhatsApp real: registra no log e devolve um id local. Quando a integração
/// com a WhatsApp Cloud API existir, troque o registro deste serviço no DependencyInjection.
/// </summary>
public sealed class LocalReminderSender(ILogger<LocalReminderSender> logger) : IReminderSender
{
    public Task<string> SendAsync(AgendaBooking booking, AgendaConfigModel config, CancellationToken ct = default)
    {
        var wamId = $"wamid.local.{Guid.NewGuid():N}";
        logger.LogInformation(
            "Lembrete D-1 (sem WhatsApp configurado) para {Phone}: {Service} em {Date:yyyy-MM-dd} às {Time} ({WamId})",
            booking.Phone, booking.ServiceName, booking.Date, AgendaFormat.Hm(booking.StartMin), wamId);
        return Task.FromResult(wamId);
    }
}
