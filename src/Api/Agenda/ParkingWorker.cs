using Application.Abstractions;
using Application.Agenda;
using Application.Parking;
using Domain.Agenda;
using Infrastructure.Parking;
using Microsoft.Extensions.Options;

namespace Api.Agenda;

/// <summary>
/// Leitura automática de placas: para cada câmera de entrada/saída com "ler sozinho", pega a imagem a cada poucos segundos,
/// compara com a anterior e, quando há movimento, manda à leitura na nuvem. A placa lida segue a regra do estacionamento
/// (mensalista, abre ou fecha ticket). Só roda com o token do Plate Recognizer configurado. Uma vez por dia apaga as imagens
/// das leituras antigas.
/// </summary>
public sealed class ParkingWorker(
    IServiceScopeFactory scopes, IPlateReader reader, IMotionDetector motion, AgendaChangeFeed feed, IOptions<ParkingOptions> options,
    ILogger<ParkingWorker> logger) : BackgroundService
{
    private readonly Dictionary<Guid, DateTime> _lastPoll = [];
    private readonly Dictionary<Guid, DateTime> _lastRead = [];
    private DateTime _lastPrune = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        IReadOnlyList<ParkingCamera> cameras = [];
        var listedAt = DateTime.MinValue;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = DateTime.UtcNow;
                if (now - _lastPrune > TimeSpan.FromHours(24)) await PruneAsync(stoppingToken);
                if (!reader.Enabled) continue;
                if (now - listedAt > TimeSpan.FromSeconds(30))
                {
                    await using var scope = scopes.CreateAsyncScope();
                    cameras = await scope.ServiceProvider.GetRequiredService<IParkingRepository>().ListAutoReadCamerasAsync(stoppingToken);
                    listedAt = now;
                }
                foreach (var camera in cameras)
                {
                    try
                    {
                        await PollAsync(camera, now, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogDebug(ex, "Estacionamento: câmera {Camera} sem imagem.", camera.Name);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PollAsync(ParkingCamera camera, DateTime now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IParkingRepository>();
        var settings = await repo.GetSettingsAsync(camera.BranchId, ct);
        if (!settings.Enabled) return;
        if (_lastPoll.TryGetValue(camera.Id, out var last) && (now - last).TotalSeconds < settings.PollSeconds) return;
        _lastPoll[camera.Id] = now;

        var image = await scope.ServiceProvider.GetRequiredService<ICameraClient>().SnapshotAsync(camera, ct);
        var change = motion.Change(camera.Id, image);
        if (change < settings.MotionPercent) return;
        if (_lastRead.TryGetValue(camera.Id, out var read) && (now - read).TotalSeconds < 3) return;
        _lastRead[camera.Id] = now;

        var plate = await reader.ReadAsync(image, ct);
        if (plate is null) return;
        var service = scope.ServiceProvider.GetRequiredService<ParkingService>();
        var r = await service.ProcessReadingAsync(camera.BranchId, camera, plate.Value.Plate, plate.Value.Confidence, ReadingSources.Cloud, null, image, ct);
        logger.LogInformation("Estacionamento: {Camera} leu {Plate} ({Confidence:P0}) -> {Result}", camera.Name, r.Plate, r.Confidence, r.Result);
        feed.Publish(camera.BranchId, AgendaChangeFeed.Parking);
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        _lastPrune = DateTime.UtcNow;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var days = Math.Max(1, options.Value.ImageRetentionDays);
            var n = await scope.ServiceProvider.GetRequiredService<IParkingRepository>().PruneReadingImagesAsync(DateTime.Now.AddDays(-days), ct);
            if (n > 0) logger.LogInformation("Estacionamento: {Count} imagem(ns) de leitura antiga(s) apagada(s).", n);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Estacionamento: falha ao apagar imagens antigas.");
        }
    }
}
