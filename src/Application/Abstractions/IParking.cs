using Application.Parking;
using Domain.Agenda;

namespace Application.Abstractions;

public interface IParkingRepository
{
    Task<ParkingSettingsModel> GetSettingsAsync(Guid branchId, CancellationToken ct = default);
    Task SaveSettingsAsync(Guid branchId, ParkingSettingsModel settings, CancellationToken ct = default);

    Task<IReadOnlyList<ParkingCamera>> ListCamerasAsync(Guid branchId, CancellationToken ct = default);
    Task<ParkingCamera?> GetCameraAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<ParkingCamera?> GetCameraByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Câmeras de entrada/saída com leitura automática, de todas as filiais (para o worker).</summary>
    Task<IReadOnlyList<ParkingCamera>> ListAutoReadCamerasAsync(CancellationToken ct = default);
    Task SaveCameraAsync(ParkingCamera camera, CancellationToken ct = default);
    Task DeleteCameraAsync(ParkingCamera camera, CancellationToken ct = default);

    Task<IReadOnlyList<ParkingMonthly>> ListMonthliesAsync(Guid branchId, CancellationToken ct = default);
    Task<ParkingMonthly?> GetMonthlyAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<ParkingMonthly?> FindMonthlyAsync(Guid branchId, string plate, CancellationToken ct = default);
    Task SaveMonthlyAsync(ParkingMonthly monthly, CancellationToken ct = default);
    Task DeleteMonthlyAsync(ParkingMonthly monthly, CancellationToken ct = default);

    Task<IReadOnlyList<ParkingTicket>> ListTicketsAsync(Guid branchId, DateTime? from, DateTime? to, bool onlyOpen, CancellationToken ct = default);
    Task<ParkingTicket?> GetTicketAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<ParkingTicket?> FindOpenTicketAsync(Guid branchId, string plate, CancellationToken ct = default);

    /// <summary>Grava o ticket novo com o próximo número da filial (sem repetir número com leituras simultâneas).</summary>
    Task InsertTicketAsync(ParkingTicket ticket, CancellationToken ct = default);
    Task UpdateTicketAsync(ParkingTicket ticket, CancellationToken ct = default);

    Task InsertReadingAsync(ParkingReading reading, CancellationToken ct = default);
    Task<IReadOnlyList<ParkingReading>> ListReadingsAsync(Guid branchId, DateTime from, int limit, CancellationToken ct = default);
    Task<ParkingReading?> LastReadingAsync(Guid branchId, Guid? cameraId, string plate, CancellationToken ct = default);
    Task<byte[]?> ReadingImageAsync(Guid branchId, Guid id, CancellationToken ct = default);

    /// <summary>Apaga as imagens das leituras mais velhas que <paramref name="before"/> (guarda só o texto).</summary>
    Task<int> PruneReadingImagesAsync(DateTime before, CancellationToken ct = default);
}

/// <summary>Imagem e endereços das câmeras (pela rede local).</summary>
public interface ICameraClient
{
    (string Snapshot, string Rtsp) Urls(ParkingCamera camera, bool withPassword);
    Task<byte[]> SnapshotAsync(ParkingCamera camera, CancellationToken ct = default);
}

/// <summary>Procura câmeras e DVRs na rede local do servidor.</summary>
public interface ICameraDiscovery
{
    Task<IReadOnlyList<DiscoveredDeviceDto>> DiscoverAsync(string? subnet, CancellationToken ct = default);
}

/// <summary>Leitura de placa numa imagem (nuvem). Nulo = nenhuma placa.</summary>
public interface IPlateReader
{
    bool Enabled { get; }
    Task<(string Plate, decimal Confidence)?> ReadAsync(byte[] image, CancellationToken ct = default);
}

/// <summary>Quanto a imagem mudou (0 a 100%) em relação à anterior da mesma câmera.</summary>
public interface IMotionDetector
{
    decimal Change(Guid cameraId, byte[] image);
}
