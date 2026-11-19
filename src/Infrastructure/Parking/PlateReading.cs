using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.Parking;

/// <summary>Seção Parking do appsettings. O token fica fora do repositório (user-secrets ou Parking__PlateRecognizerToken).</summary>
public sealed class ParkingOptions
{
    public const string SectionName = "Parking";

    /// <summary>Token do Plate Recognizer (platerecognizer.com; plano grátis com 2.500 leituras por mês). Vazio = sem leitura na nuvem.</summary>
    public string PlateRecognizerToken { get; set; } = string.Empty;
    public string PlateRecognizerUrl { get; set; } = "https://api.platerecognizer.com/v1/plate-reader/";

    /// <summary>Região das placas (br = Brasil, inclui Mercosul).</summary>
    public string Region { get; set; } = "br";

    /// <summary>Dias que a imagem da leitura fica guardada (a leitura em texto continua).</summary>
    public int ImageRetentionDays { get; set; } = 30;
}

/// <summary>Leitura de placa pela API do Plate Recognizer (só manda imagem quando a câmera detecta movimento).</summary>
public sealed class PlateRecognizerReader(IOptions<ParkingOptions> options, ILogger<PlateRecognizerReader> logger) : IPlateReader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public bool Enabled => options.Value.PlateRecognizerToken.Length > 0;

    public async Task<(string Plate, decimal Confidence)?> ReadAsync(byte[] image, CancellationToken ct = default)
    {
        var o = options.Value;
        if (!Enabled) return null;
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "upload", "frame.jpg");
        form.Add(new StringContent(o.Region), "regions");
        using var req = new HttpRequestMessage(HttpMethod.Post, o.PlateRecognizerUrl) { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Token", o.PlateRecognizerToken);
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Plate Recognizer respondeu {Status}: {Body}", (int)resp.StatusCode, body.Length > 300 ? body[..300] : body);
            return null;
        }
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
        var best = results.EnumerateArray().OrderByDescending(r => r.TryGetProperty("score", out var s) ? s.GetDecimal() : 0m).First();
        var plate = best.GetProperty("plate").GetString() ?? string.Empty;
        var score = best.TryGetProperty("score", out var sc) ? sc.GetDecimal() : 0m;
        return plate.Length == 0 ? null : (plate.ToUpperInvariant(), score);
    }
}

/// <summary>
/// Movimento entre duas imagens da mesma câmera: compara miniaturas 64×36 em tons de cinza e devolve a porcentagem de pixels
/// que mudaram bastante. Evita mandar à leitura (que custa) imagens iguais da cancela parada.
/// </summary>
public sealed class MotionDetector : IMotionDetector
{
    private readonly ConcurrentDictionary<Guid, byte[]> _last = new();

    public decimal Change(Guid cameraId, byte[] image)
    {
        byte[] current;
        try
        {
            using var img = Image.Load<L8>(image);
            img.Mutate(x => x.Resize(64, 36));
            current = new byte[64 * 36];
            img.CopyPixelDataTo(current);
        }
        catch (Exception)
        {
            return 0m;
        }
        var previous = _last.GetValueOrDefault(cameraId);
        _last[cameraId] = current;
        if (previous is null || previous.Length != current.Length) return 100m;
        var changed = 0;
        for (var i = 0; i < current.Length; i++)
            if (Math.Abs(current[i] - previous[i]) > 25) changed++;
        return Math.Round(100m * changed / current.Length, 1);
    }
}
