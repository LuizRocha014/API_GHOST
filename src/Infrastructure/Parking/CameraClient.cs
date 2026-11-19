using System.Net;
using Application.Abstractions;
using Domain.Agenda;

namespace Infrastructure.Parking;

/// <summary>
/// Endereços e imagem das câmeras. Predefinições das marcas mais comuns no Brasil:
/// <list type="bullet">
/// <item>Hikvision (e DVR/NVR): imagem em /ISAPI/Streaming/channels/{canal}01/picture; vídeo rtsp://…/Streaming/Channels/{canal}01.</item>
/// <item>Intelbras e Dahua (mesma base): imagem em /cgi-bin/snapshot.cgi?channel={canal}; vídeo rtsp://…/cam/realmonitor?channel={canal}&amp;subtype=0.</item>
/// <item>Outra: os endereços informados no cadastro.</item>
/// </list>
/// A autenticação é a do aparelho (Basic ou Digest; o HttpClient negocia).
/// </summary>
public sealed class CameraClient : ICameraClient
{
    public (string Snapshot, string Rtsp) Urls(ParkingCamera c, bool withPassword)
    {
        var cred = c.Username.Length == 0 ? string.Empty
            : withPassword ? $"{Uri.EscapeDataString(c.Username)}:{Uri.EscapeDataString(c.Password)}@"
            : $"{Uri.EscapeDataString(c.Username)}:***@";
        var http = c.HttpPort == 80 ? $"http://{c.Host}" : $"http://{c.Host}:{c.HttpPort}";
        var rtsp = $"rtsp://{cred}{c.Host}:{c.RtspPort}";
        return c.Brand switch
        {
            CameraBrands.Hikvision => ($"{http}/ISAPI/Streaming/channels/{c.Channel}01/picture", $"{rtsp}/Streaming/Channels/{c.Channel}01"),
            CameraBrands.Intelbras or CameraBrands.Dahua =>
                ($"{http}/cgi-bin/snapshot.cgi?channel={c.Channel}", $"{rtsp}/cam/realmonitor?channel={c.Channel}&subtype=0"),
            _ => (c.SnapshotUrl, c.RtspUrl),
        };
    }

    public async Task<byte[]> SnapshotAsync(ParkingCamera c, CancellationToken ct = default)
    {
        var (url, _) = Urls(c, withPassword: false);
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Sem endereço de imagem: cadastre o snapshot (a imagem pela rede) da câmera.");
        using var handler = new HttpClientHandler
        {
            Credentials = c.Username.Length == 0 ? null : new NetworkCredential(c.Username, c.Password),
            PreAuthenticate = false,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        using var resp = await http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) throw new InvalidOperationException("Usuário ou senha da câmera recusados (401).");
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"A câmera respondeu {(int)resp.StatusCode} em {url}.");
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var type = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var isJpeg = bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8;
        var isPng = bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50;
        if (!isJpeg && !isPng && !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"O endereço {url} não devolveu uma imagem ({type}).");
        return bytes;
    }
}
