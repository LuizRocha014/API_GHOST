namespace Domain.Agenda;

/// <summary>
/// Câmera IP ou canal de um DVR/NVR da filial. A API busca a imagem (snapshot) pela rede local; a câmera marcada como
/// entrada ou saída do estacionamento tem a placa lida (pela própria câmera com LPR ou pela leitura na nuvem).
/// </summary>
public sealed class ParkingCamera
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>ip (câmera IP) ou dvr (canal de DVR/NVR).</summary>
    public string Kind { get; set; } = CameraKinds.Ip;

    /// <summary>hikvision, intelbras, dahua ou generic (endereços informados à mão).</summary>
    public string Brand { get; set; } = CameraBrands.Generic;
    public string Host { get; set; } = string.Empty;
    public int HttpPort { get; set; } = 80;
    public int RtspPort { get; set; } = 554;
    public int Channel { get; set; } = 1;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Endereço da imagem (só para a marca "generic"; nas outras sai da predefinição).</summary>
    public string SnapshotUrl { get; set; } = string.Empty;
    public string RtspUrl { get; set; } = string.Empty;

    /// <summary>none, entry (entrada do estacionamento) ou exit (saída).</summary>
    public string Role { get; set; } = CameraRoles.None;

    /// <summary>A API olha a câmera e lê a placa quando há movimento (leitura na nuvem).</summary>
    public bool AutoRead { get; set; }

    /// <summary>Segredo do endereço que a câmera com LPR chama para avisar a placa lida.</summary>
    public string IngestToken { get; set; } = string.Empty;
    public DateTime? LastSeenAt { get; set; }
    public string? LastError { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class CameraKinds
{
    public const string Ip = "ip";
    public const string Dvr = "dvr";
}

public static class CameraBrands
{
    public const string Hikvision = "hikvision";
    public const string Intelbras = "intelbras";
    public const string Dahua = "dahua";
    public const string Generic = "generic";
    public static readonly string[] All = [Hikvision, Intelbras, Dahua, Generic];
}

public static class CameraRoles
{
    public const string None = "none";
    public const string Entry = "entry";
    public const string Exit = "exit";
    public static readonly string[] All = [None, Entry, Exit];
}

/// <summary>Mensalista do estacionamento: a placa entra e sai sem ticket enquanto o cadastro estiver válido.</summary>
public sealed class ParkingMonthly
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Plate { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Vehicle { get; set; } = string.Empty;

    /// <summary>Último dia válido. Nulo = sem vencimento.</summary>
    public DateTime? ValidUntil { get; set; }
    public decimal? MonthlyFee { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Ticket do estacionamento rotativo: abre na entrada e fecha na saída com o valor da tabela.</summary>
public sealed class ParkingTicket
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }

    /// <summary>Número sequencial da filial (o que vai no papel).</summary>
    public int Number { get; set; }
    public string Plate { get; set; } = string.Empty;
    public string Vehicle { get; set; } = string.Empty;

    /// <summary>Hora local da estética.</summary>
    public DateTime EntryAt { get; set; }
    public DateTime? ExitAt { get; set; }
    public Guid? EntryCameraId { get; set; }
    public Guid? ExitCameraId { get; set; }

    /// <summary>open, closed ou canceled.</summary>
    public string Status { get; set; } = TicketStatuses.Open;
    public decimal? Amount { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class TicketStatuses
{
    public const string Open = "open";
    public const string Closed = "closed";
    public const string Canceled = "canceled";
}

/// <summary>Cada placa lida (ou digitada) e o que o sistema fez com ela.</summary>
public sealed class ParkingReading
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid? CameraId { get; set; }
    public string Plate { get; set; } = string.Empty;
    public decimal? Confidence { get; set; }

    /// <summary>camera (LPR da câmera), cloud (leitura na nuvem) ou manual.</summary>
    public string Source { get; set; } = ReadingSources.Manual;

    /// <summary>entry ou exit.</summary>
    public string Direction { get; set; } = CameraRoles.Entry;

    /// <summary>monthly, ticket_opened, ticket_closed, duplicate, already_open, no_ticket, expired_monthly, low_confidence.</summary>
    public string Result { get; set; } = string.Empty;
    public Guid? TicketId { get; set; }
    public Guid? MonthlyId { get; set; }
    public byte[]? Image { get; set; }

    /// <summary>Hora local da estética.</summary>
    public DateTime At { get; set; }
}

public static class ReadingSources
{
    public const string Camera = "camera";
    public const string Cloud = "cloud";
    public const string Manual = "manual";
}

/// <summary>Ajustes do estacionamento da filial (JSON).</summary>
public sealed class ParkingSettingsRow
{
    public Guid BranchId { get; set; }
    public string Json { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
