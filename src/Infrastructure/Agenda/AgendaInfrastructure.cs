using Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Agenda;

/// <summary>
/// Relógio no fuso da estética (padrão America/Sao_Paulo; configure em Agenda:TimeZone).
/// A API pode rodar num servidor em UTC e mesmo assim "hoje" e "agora" saem certos.
/// <para>
/// Agenda:RelogioDemo ("15:30", só para demonstração e prints): ao subir, "agora" passa a ser hoje nesse horário e o
/// tempo segue andando a partir dele. Os registros de data e hora (criado, pago) continuam com a hora real.
/// </para>
/// </summary>
public sealed class AgendaClock : IAgendaClock
{
    private readonly TimeZoneInfo _zone;

    public AgendaClock(IConfiguration configuration)
    {
        var id = configuration["Agenda:TimeZone"] ?? "America/Sao_Paulo";
        _zone = TryFind(id) ?? TryFind("E. South America Standard Time") ?? TimeZoneInfo.Local;
        if (TimeSpan.TryParse(configuration["Agenda:RelogioDemo"], out var at) && at >= TimeSpan.Zero && at < TimeSpan.FromDays(1))
        {
            var real = ToLocal(DateTime.UtcNow);
            _offset = real.Date + at - real;
        }
    }

    private readonly TimeSpan _offset;

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

    public DateTime NowLocal => ToLocal(DateTime.UtcNow) + _offset;

    public DateTime ToLocal(DateTime utc) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone), DateTimeKind.Unspecified);
}
