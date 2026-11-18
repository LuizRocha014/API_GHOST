using Application.Abstractions;
using Microsoft.Extensions.Configuration;

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
