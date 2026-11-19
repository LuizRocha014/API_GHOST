using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Agenda;

namespace Application.Parking;

/// <summary>Tabela e regras do estacionamento rotativo da filial.</summary>
public sealed class ParkingSettingsModel
{
    public bool Enabled { get; set; } = true;

    /// <summary>Minutos sem cobrança (entrou e saiu logo).</summary>
    public int ToleranceMin { get; set; } = 15;
    public decimal FirstHour { get; set; } = 10m;

    /// <summary>Cada hora (ou fração) depois da primeira.</summary>
    public decimal ExtraHour { get; set; } = 5m;

    /// <summary>Teto por período de 24 h. 0 = sem teto.</summary>
    public decimal DailyMax { get; set; } = 40m;

    /// <summary>A mesma placa lida de novo na mesma câmera dentro deste tempo é ignorada.</summary>
    public int DedupMinutes { get; set; } = 3;

    /// <summary>Leitura com confiança menor que esta (0 a 1) só fica registrada, sem abrir ticket.</summary>
    public decimal MinConfidence { get; set; } = 0.7m;

    /// <summary>Leitura automática: intervalo entre imagens e quanto a imagem precisa mudar (%) para mandar à leitura.</summary>
    public int PollSeconds { get; set; } = 2;
    public decimal MotionPercent { get; set; } = 6m;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ParkingSettingsModel Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<ParkingSettingsModel>(json, Json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}

public static class ParkingRules
{
    private static readonly Regex Mercosul = new(@"^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$", RegexOptions.Compiled);

    /// <summary>Placa só com letras e números, maiúscula (ABC1D23 ou ABC1234).</summary>
    public static string NormalizePlate(string? plate) =>
        new((plate ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static bool IsValidPlate(string plate) => Mercosul.IsMatch(plate);

    /// <summary>
    /// Valor do período: até a tolerância, grátis; depois, 1ª hora + horas adicionais (fração conta como hora), com teto
    /// por 24 h. Mais de um dia: tetos dos dias cheios + o valor do que sobrou (também com teto).
    /// </summary>
    public static decimal Price(ParkingSettingsModel s, DateTime entry, DateTime exit)
    {
        var minutes = Math.Max(0, (int)Math.Ceiling((exit - entry).TotalMinutes));
        if (minutes <= s.ToleranceMin) return 0m;
        var days = minutes / 1440;
        var rest = minutes % 1440;
        decimal Day(int m)
        {
            if (m <= 0) return 0m;
            var hours = (int)Math.Ceiling(m / 60.0);
            var v = s.FirstHour + Math.Max(0, hours - 1) * s.ExtraHour;
            return s.DailyMax > 0 ? Math.Min(v, s.DailyMax) : v;
        }
        var full = s.DailyMax > 0 ? s.DailyMax : Day(1440);
        return days * full + Day(rest);
    }

    public static string Duration(DateTime entry, DateTime exit)
    {
        var t = exit - entry;
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 24 ? $"{(int)t.TotalDays}d {t.Hours}h{t.Minutes:00}" : $"{(int)t.TotalHours}h{t.Minutes:00}";
    }
}

public sealed record CameraDto(
    string Id, string Name, string Kind, string Brand, string Host, int HttpPort, int RtspPort, int Channel, string Username,
    bool HasPassword, string SnapshotUrl, string RtspUrl, string Role, bool AutoRead, string IngestPath, string? LastSeenAt,
    string? LastError);

public sealed class CameraRequest
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = CameraKinds.Ip;
    public string Brand { get; set; } = CameraBrands.Generic;
    public string Host { get; set; } = string.Empty;
    public int HttpPort { get; set; } = 80;
    public int RtspPort { get; set; } = 554;
    public int Channel { get; set; } = 1;
    public string? Username { get; set; }

    /// <summary>Nulo = mantém a senha gravada.</summary>
    public string? Password { get; set; }
    public string? SnapshotUrl { get; set; }
    public string? RtspUrl { get; set; }
    public string Role { get; set; } = CameraRoles.None;
    public bool AutoRead { get; set; }
}

/// <summary>Aparelho encontrado na rede: IP, portas abertas e o que dá para saber dele (ONVIF, marca provável).</summary>
public sealed record DiscoveredDeviceDto(string Ip, IReadOnlyList<int> Ports, string? Brand, string? Name, string? Onvif, string Kind);

public sealed record MonthlyDto(
    string Id, string Plate, string Name, string Phone, string Vehicle, string? ValidUntil, decimal? MonthlyFee, string Notes,
    bool Active, bool Valid);

public sealed class MonthlyRequest
{
    public string Plate { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Vehicle { get; set; }
    public string? ValidUntil { get; set; }
    public decimal? MonthlyFee { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; } = true;
}

public sealed record TicketDto(
    string Id, int Number, string Plate, string Vehicle, string EntryAt, string? ExitAt, string Status, decimal? Amount,
    decimal? AmountNow, string Duration, decimal? PaidAmount, string? PaymentMethod, string? PaidAt, string? EntryCamera,
    string? ExitCamera, string Notes);

public sealed class OpenTicketRequest
{
    public string Plate { get; set; } = string.Empty;
    public string? Vehicle { get; set; }
    public string? Notes { get; set; }
}

public sealed class CloseTicketRequest
{
    /// <summary>Forma de pagamento (cash, pix, debit, credit, other). Vazio = fecha sem receber agora.</summary>
    public string? Method { get; set; }

    /// <summary>Valor cobrado. Vazio = o da tabela.</summary>
    public decimal? Amount { get; set; }
}

/// <summary>Placa lida (pela câmera, pela nuvem ou digitada) para entrada ou saída.</summary>
public sealed class ReadingRequest
{
    public string Plate { get; set; } = string.Empty;
    public decimal? Confidence { get; set; }

    /// <summary>entry ou exit. Vazio = o papel da câmera (entrada ou saída).</summary>
    public string? Direction { get; set; }
    public string? CameraId { get; set; }
}

public sealed record ReadingDto(
    string Id, string At, string Plate, decimal? Confidence, string Source, string Direction, string Result, string Message,
    string? Camera, string? TicketId, int? TicketNumber, string? MonthlyName, bool HasImage);

/// <summary>Pátio agora: tickets abertos com o valor até agora, e o movimento do dia.</summary>
public sealed record YardDto(
    IReadOnlyList<TicketDto> Open, int MonthlyInside, int EntriesToday, int ExitsToday, decimal ReceivedToday,
    IReadOnlyList<ReadingDto> Recent);

public static class ParkingFormat
{
    public static string Local(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}
