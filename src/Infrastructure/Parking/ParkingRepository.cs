using Application.Abstractions;
using Application.Parking;
using Domain.Agenda;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Parking;

public sealed class ParkingRepository(AppDbContext db) : IParkingRepository
{
    /// <summary>Na listagem a imagem não vem (é grande): um byte marca que a leitura tem foto.</summary>
    private static readonly byte[] HasImageMarker = [1];

    public async Task<ParkingSettingsModel> GetSettingsAsync(Guid branchId, CancellationToken ct = default)
    {
        var row = await db.ParkingSettings.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId, ct);
        return ParkingSettingsModel.Parse(row?.Json);
    }

    public async Task SaveSettingsAsync(Guid branchId, ParkingSettingsModel settings, CancellationToken ct = default)
    {
        var row = await db.ParkingSettings.FirstOrDefaultAsync(x => x.BranchId == branchId, ct);
        if (row is null) db.ParkingSettings.Add(row = new ParkingSettingsRow { BranchId = branchId });
        row.Json = settings.ToJson();
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ParkingCamera>> ListCamerasAsync(Guid branchId, CancellationToken ct = default) =>
        await db.ParkingCameras.AsNoTracking().Where(c => c.BranchId == branchId && c.Active).OrderBy(c => c.Name).ToListAsync(ct);

    public Task<ParkingCamera?> GetCameraAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        db.ParkingCameras.FirstOrDefaultAsync(c => c.BranchId == branchId && c.Id == id && c.Active, ct);

    public Task<ParkingCamera?> GetCameraByTokenAsync(string token, CancellationToken ct = default) =>
        db.ParkingCameras.FirstOrDefaultAsync(c => c.IngestToken == token && c.Active, ct);

    public async Task<IReadOnlyList<ParkingCamera>> ListAutoReadCamerasAsync(CancellationToken ct = default) =>
        await db.ParkingCameras.AsNoTracking().Where(c => c.Active && c.AutoRead && c.Role != CameraRoles.None).ToListAsync(ct);

    public async Task SaveCameraAsync(ParkingCamera camera, CancellationToken ct = default)
    {
        var tracked = db.ParkingCameras.Local.FirstOrDefault(x => x.Id == camera.Id);
        if (tracked is null)
        {
            if (await db.ParkingCameras.AnyAsync(x => x.Id == camera.Id, ct)) db.ParkingCameras.Update(camera);
            else db.ParkingCameras.Add(camera);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCameraAsync(ParkingCamera camera, CancellationToken ct = default)
    {
        camera.Active = false;
        camera.AutoRead = false;
        camera.UpdatedAt = DateTime.UtcNow;
        await SaveCameraAsync(camera, ct);
    }

    public async Task<IReadOnlyList<ParkingMonthly>> ListMonthliesAsync(Guid branchId, CancellationToken ct = default) =>
        await db.ParkingMonthlies.AsNoTracking().Where(m => m.BranchId == branchId).ToListAsync(ct);

    public Task<ParkingMonthly?> GetMonthlyAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        db.ParkingMonthlies.FirstOrDefaultAsync(m => m.BranchId == branchId && m.Id == id, ct);

    public Task<ParkingMonthly?> FindMonthlyAsync(Guid branchId, string plate, CancellationToken ct = default) =>
        db.ParkingMonthlies.AsNoTracking().FirstOrDefaultAsync(m => m.BranchId == branchId && m.Plate == plate, ct);

    public async Task SaveMonthlyAsync(ParkingMonthly monthly, CancellationToken ct = default)
    {
        if (db.ParkingMonthlies.Local.All(x => x.Id != monthly.Id) && !await db.ParkingMonthlies.AnyAsync(x => x.Id == monthly.Id, ct))
            db.ParkingMonthlies.Add(monthly);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteMonthlyAsync(ParkingMonthly monthly, CancellationToken ct = default)
    {
        db.ParkingMonthlies.Remove(monthly);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ParkingTicket>> ListTicketsAsync(
        Guid branchId, DateTime? from, DateTime? to, bool onlyOpen, CancellationToken ct = default)
    {
        var q = db.ParkingTickets.AsNoTracking().Where(t => t.BranchId == branchId);
        if (onlyOpen) q = q.Where(t => t.Status == TicketStatuses.Open);
        if (from is { } f) q = q.Where(t => t.EntryAt >= f || t.Status == TicketStatuses.Open);
        if (to is { } e) q = q.Where(t => t.EntryAt < e);
        return await q.OrderByDescending(t => t.EntryAt).Take(1000).ToListAsync(ct);
    }

    public Task<ParkingTicket?> GetTicketAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        db.ParkingTickets.FirstOrDefaultAsync(t => t.BranchId == branchId && t.Id == id, ct);

    public Task<ParkingTicket?> FindOpenTicketAsync(Guid branchId, string plate, CancellationToken ct = default) =>
        db.ParkingTickets.Where(t => t.BranchId == branchId && t.Plate == plate && t.Status == TicketStatuses.Open)
            .OrderByDescending(t => t.EntryAt).FirstOrDefaultAsync(ct);

    public async Task InsertTicketAsync(ParkingTicket ticket, CancellationToken ct = default)
    {
        // O número sai do maior da filial sob uma trava nomeada da filial: duas leituras ao mesmo tempo não repetem número.
        // (Isolamento normal: SERIALIZABLE voltaria ao pool de conexões e quebraria o READPAST da fila do bot.)
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC sp_getapplock @Resource = {"parking-ticket-" + ticket.BranchId}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
                ct);
            var max = await db.ParkingTickets.Where(t => t.BranchId == ticket.BranchId).MaxAsync(t => (int?)t.Number, ct) ?? 0;
            ticket.Number = max + 1;
            db.ParkingTickets.Add(ticket);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task UpdateTicketAsync(ParkingTicket ticket, CancellationToken ct = default)
    {
        if (db.ParkingTickets.Local.All(x => x.Id != ticket.Id)) db.ParkingTickets.Update(ticket);
        await db.SaveChangesAsync(ct);
    }

    public async Task InsertReadingAsync(ParkingReading reading, CancellationToken ct = default)
    {
        db.ParkingReadings.Add(reading);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ParkingReading>> ListReadingsAsync(Guid branchId, DateTime from, int limit, CancellationToken ct = default) =>
        await db.ParkingReadings.AsNoTracking().Where(r => r.BranchId == branchId && r.At >= from)
            .OrderByDescending(r => r.At).Take(limit)
            .Select(r => new ParkingReading
            {
                Id = r.Id, BranchId = r.BranchId, CameraId = r.CameraId, Plate = r.Plate, Confidence = r.Confidence, Source = r.Source,
                Direction = r.Direction, Result = r.Result, TicketId = r.TicketId, MonthlyId = r.MonthlyId, At = r.At,
                Image = r.Image != null ? HasImageMarker : null,
            }).ToListAsync(ct);

    public Task<ParkingReading?> LastReadingAsync(Guid branchId, Guid? cameraId, string plate, CancellationToken ct = default) =>
        db.ParkingReadings.AsNoTracking().Where(r => r.BranchId == branchId && r.CameraId == cameraId && r.Plate == plate)
            .OrderByDescending(r => r.At)
            .Select(r => new ParkingReading { Id = r.Id, At = r.At, Result = r.Result, Plate = r.Plate })
            .FirstOrDefaultAsync(ct);

    public async Task<byte[]?> ReadingImageAsync(Guid branchId, Guid id, CancellationToken ct = default) =>
        await db.ParkingReadings.AsNoTracking().Where(r => r.BranchId == branchId && r.Id == id).Select(r => r.Image).FirstOrDefaultAsync(ct);

    public Task<int> PruneReadingImagesAsync(DateTime before, CancellationToken ct = default) =>
        db.ParkingReadings.Where(r => r.At < before && r.Image != null).ExecuteUpdateAsync(s => s.SetProperty(r => r.Image, (byte[]?)null), ct);
}
