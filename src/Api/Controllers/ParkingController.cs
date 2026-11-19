using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Agenda;
using Application.Abstractions;
using Application.Agenda;
using Application.Parking;
using Domain.Agenda;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Estacionamento rotativo da filial: câmeras (IP e DVR, imagem ao vivo, busca na rede), mensalistas, tickets, leituras de
/// placa e o pátio agora. Ajustes, câmeras e mensalistas: só o dono.
/// </summary>
[ApiController]
[Route("api/agenda/{branchId:guid}/estacionamento")]
[Authorize]
[TypeFilter(typeof(AgendaExceptionFilter))]
[TypeFilter(typeof(AgendaBranchAccessFilter))]
[TypeFilter(typeof(AgendaChangePublisher))]
public sealed class ParkingController(ParkingService parking) : ControllerBase
{
    [HttpGet("ajustes")]
    public async Task<ActionResult<ParkingSettingsModel>> Settings(Guid branchId, CancellationToken ct) => Ok(await parking.SettingsAsync(branchId, ct));

    [HttpPut("ajustes")]
    [AgendaOwnerOnly]
    public async Task<ActionResult<ParkingSettingsModel>> SaveSettings(Guid branchId, [FromBody] ParkingSettingsModel request, CancellationToken ct) =>
        Ok(await parking.SaveSettingsAsync(branchId, request, ct));

    /// <summary>Tickets abertos com o valor até agora, mensalistas dentro, entradas e saídas do dia e as últimas leituras.</summary>
    [HttpGet("patio")]
    public async Task<ActionResult<YardDto>> Yard(Guid branchId, CancellationToken ct) => Ok(await parking.YardAsync(branchId, ct));

    /* ---------- Câmeras ---------- */

    [HttpGet("cameras")]
    public async Task<ActionResult<IReadOnlyList<CameraDto>>> Cameras(Guid branchId, CancellationToken ct) => Ok(await parking.ListCamerasAsync(branchId, ct));

    [HttpPost("cameras")]
    [AgendaOwnerOnly]
    public async Task<ActionResult<CameraDto>> CreateCamera(Guid branchId, [FromBody] CameraRequest request, CancellationToken ct) =>
        Ok(await parking.SaveCameraAsync(branchId, null, request, ct));

    [HttpPut("cameras/{id:guid}")]
    [AgendaOwnerOnly]
    public async Task<ActionResult<CameraDto>> UpdateCamera(Guid branchId, Guid id, [FromBody] CameraRequest request, CancellationToken ct) =>
        Ok(await parking.SaveCameraAsync(branchId, id, request, ct));

    [HttpDelete("cameras/{id:guid}")]
    [AgendaOwnerOnly]
    public async Task<IActionResult> DeleteCamera(Guid branchId, Guid id, CancellationToken ct)
    {
        await parking.DeleteCameraAsync(branchId, id, ct);
        return NoContent();
    }

    /// <summary>Imagem da câmera agora (JPEG). O app pede de novo a cada segundo para mostrar "ao vivo".</summary>
    [HttpGet("cameras/{id:guid}/imagem")]
    public async Task<IActionResult> Snapshot(Guid branchId, Guid id, CancellationToken ct)
    {
        var bytes = await parking.SnapshotAsync(await parking.CameraAsync(branchId, id, ct), ct);
        Response.Headers.CacheControl = "no-store";
        return File(bytes, bytes.Length > 1 && bytes[0] == 0x89 ? "image/png" : "image/jpeg");
    }

    /// <summary>Pega a imagem da câmera agora e lê a placa (na nuvem). Serve para testar a câmera de entrada ou de saída.</summary>
    [HttpPost("cameras/{id:guid}/ler")]
    public async Task<ActionResult<ReadingDto>> ReadNow(
        Guid branchId, Guid id, [FromServices] IPlateReader reader, CancellationToken ct)
    {
        if (!reader.Enabled)
            throw new AgendaValidationException("Leitura na nuvem não configurada: defina Parking:PlateRecognizerToken na API (ou use câmera com LPR).");
        var camera = await parking.CameraAsync(branchId, id, ct);
        var image = await parking.SnapshotAsync(camera, ct);
        var read = await reader.ReadAsync(image, ct);
        if (read is null) throw new AgendaValidationException("Nenhuma placa encontrada na imagem agora.");
        return Ok(await parking.ProcessReadingAsync(branchId, camera, read.Value.Plate, read.Value.Confidence, ReadingSources.Cloud, null, image, ct));
    }

    /// <summary>Procura câmeras e DVRs na rede do servidor (ONVIF e portas típicas). <c>rede</c> = "192.168.0" para outra faixa.</summary>
    [HttpPost("cameras/descobrir")]
    [AgendaOwnerOnly]
    public async Task<ActionResult<IReadOnlyList<DiscoveredDeviceDto>>> Discover(
        Guid branchId, [FromServices] ICameraDiscovery discovery, [FromQuery] string? rede, CancellationToken ct) =>
        Ok(await discovery.DiscoverAsync(rede, ct));

    /* ---------- Mensalistas ---------- */

    [HttpGet("mensalistas")]
    public async Task<ActionResult<IReadOnlyList<MonthlyDto>>> Monthlies(Guid branchId, CancellationToken ct) => Ok(await parking.ListMonthliesAsync(branchId, ct));

    [HttpPost("mensalistas")]
    public async Task<ActionResult<MonthlyDto>> CreateMonthly(Guid branchId, [FromBody] MonthlyRequest request, CancellationToken ct) =>
        Ok(await parking.SaveMonthlyAsync(branchId, null, request, ct));

    [HttpPut("mensalistas/{id:guid}")]
    public async Task<ActionResult<MonthlyDto>> UpdateMonthly(Guid branchId, Guid id, [FromBody] MonthlyRequest request, CancellationToken ct) =>
        Ok(await parking.SaveMonthlyAsync(branchId, id, request, ct));

    [HttpDelete("mensalistas/{id:guid}")]
    [AgendaOwnerOnly]
    public async Task<IActionResult> DeleteMonthly(Guid branchId, Guid id, CancellationToken ct)
    {
        await parking.DeleteMonthlyAsync(branchId, id, ct);
        return NoContent();
    }

    /* ---------- Tickets e leituras ---------- */

    [HttpGet("tickets")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> Tickets(
        Guid branchId, [FromQuery] string? de, [FromQuery] string? ate, [FromQuery] bool abertos, CancellationToken ct) =>
        Ok(await parking.ListTicketsAsync(branchId, de, ate, abertos, ct));

    /// <summary>Abre ticket pelo balcão (placa digitada).</summary>
    [HttpPost("tickets")]
    public async Task<ActionResult<TicketDto>> OpenTicket(Guid branchId, [FromBody] OpenTicketRequest request, CancellationToken ct) =>
        Ok(await parking.OpenTicketAsync(branchId, request, ct));

    /// <summary>Saída: fecha com o valor da tabela e, com a forma de pagamento, recebe.</summary>
    [HttpPost("tickets/{id:guid}/fechar")]
    public async Task<ActionResult<TicketDto>> CloseTicket(Guid branchId, Guid id, [FromBody] CloseTicketRequest request, CancellationToken ct) =>
        Ok(await parking.CloseTicketAsync(branchId, id, request, ct));

    [HttpPost("tickets/{id:guid}/cancelar")]
    public async Task<ActionResult<TicketDto>> CancelTicket(Guid branchId, Guid id, CancellationToken ct) =>
        Ok(await parking.CancelTicketAsync(branchId, id, ct));

    /// <summary>Placa digitada na entrada ou na saída (sem câmera, ou corrigindo uma leitura).</summary>
    [HttpPost("leituras")]
    public async Task<ActionResult<ReadingDto>> Reading(Guid branchId, [FromBody] ReadingRequest request, CancellationToken ct)
    {
        ParkingCamera? camera = Guid.TryParse(request.CameraId, out var cid) ? await parking.CameraAsync(branchId, cid, ct) : null;
        return Ok(await parking.ProcessReadingAsync(branchId, camera, request.Plate, request.Confidence, ReadingSources.Manual,
            request.Direction, null, ct));
    }

    [HttpGet("leituras")]
    public async Task<ActionResult<IReadOnlyList<ReadingDto>>> Readings(
        Guid branchId, [FromQuery] int dias = 1, [FromQuery] int limite = 200, CancellationToken ct = default) =>
        Ok(await parking.ListReadingsAsync(branchId, dias, limite, ct));

    [HttpGet("leituras/{id:guid}/imagem")]
    public async Task<IActionResult> ReadingImage(Guid branchId, Guid id, CancellationToken ct) =>
        await parking.ReadingImageAsync(branchId, id, ct) is { Length: > 0 } bytes ? File(bytes, "image/jpeg") : NotFound();
}

/// <summary>
/// Câmera com LPR (leitura de placa embutida) avisando a placa lida: configure na câmera o envio HTTP (POST) para
/// <c>/api/estacionamento/leitura/{token}</c> (o token está no cadastro da câmera). Aceita JSON ({"placa": "ABC1D23"} ou
/// plate/licensePlate), XML (ANPR da Hikvision: licensePlate, confidenceLevel) e multipart com o XML/JSON e a foto.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/estacionamento/leitura")]
public sealed partial class ParkingIngestController(ParkingService parking, IParkingRepository repo, AgendaChangeFeed feed) : ControllerBase
{
    [HttpPost("{token}")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Ingest(string token, CancellationToken ct)
    {
        var camera = await repo.GetCameraByTokenAsync(token, ct);
        if (camera is null) return NotFound(new { mensagem = "Câmera não encontrada (token inválido)." });

        string? text = null;
        byte[]? image = null;
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            foreach (var f in form.Files)
            {
                await using var s = f.OpenReadStream();
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms, ct);
                var bytes = ms.ToArray();
                if (f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || f.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    image ??= bytes;
                else text ??= Encoding.UTF8.GetString(bytes);
            }
            text ??= string.Join('\n', form.Select(kv => $"{kv.Key}={kv.Value}"));
        }
        else
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            text = await reader.ReadToEndAsync(ct);
        }

        var (plate, confidence, img) = Parse(text ?? string.Empty);
        image ??= img;
        if (string.IsNullOrWhiteSpace(plate)) return BadRequest(new { mensagem = "Placa não encontrada no corpo (placa, plate ou licensePlate)." });
        var r = await parking.ProcessReadingAsync(camera.BranchId, camera, plate, confidence, ReadingSources.Camera, null, image, ct);
        feed.Publish(camera.BranchId, AgendaChangeFeed.Parking);
        return Ok(new { resultado = r.Result, mensagem = r.Message, ticket = r.TicketNumber });
    }

    private static (string? Plate, decimal? Confidence, byte[]? Image) Parse(string text)
    {
        var t = text.Trim();
        if (t.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(t);
                string? plate = null;
                decimal? conf = null;
                byte[]? image = null;
                void Walk(JsonElement e)
                {
                    if (e.ValueKind == JsonValueKind.Object)
                        foreach (var p in e.EnumerateObject())
                        {
                            var k = p.Name.ToLowerInvariant();
                            if (plate is null && k is "placa" or "plate" or "licenseplate" or "platenumber" && p.Value.ValueKind == JsonValueKind.String)
                                plate = p.Value.GetString();
                            else if (conf is null && k is "confianca" or "confidence" or "score" && p.Value.ValueKind == JsonValueKind.Number)
                                conf = Normalize(p.Value.GetDecimal());
                            else if (image is null && k is "imagem" or "image" && p.Value.ValueKind == JsonValueKind.String)
                                try { image = Convert.FromBase64String(p.Value.GetString()!); } catch (FormatException) { }
                            else Walk(p.Value);
                        }
                    else if (e.ValueKind == JsonValueKind.Array)
                        foreach (var x in e.EnumerateArray()) Walk(x);
                }
                Walk(doc.RootElement);
                return (plate, conf, image);
            }
            catch (JsonException)
            {
            }
        }
        var m = XmlPlate().Match(t);
        var c = XmlConfidence().Match(t);
        var kv = FormPlate().Match(t);
        var plateText = m.Success ? m.Groups[1].Value : kv.Success ? kv.Groups[1].Value : null;
        return (plateText, c.Success && decimal.TryParse(c.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var cv) ? Normalize(cv) : null, null);
    }

    /// <summary>Confiança em 0–1 (câmeras mandam 0–100).</summary>
    private static decimal Normalize(decimal v) => v > 1 ? Math.Round(v / 100m, 4) : v;

    [GeneratedRegex(@"<(?:\w+:)?(?:licensePlate|plateNumber|placa)>\s*([A-Za-z0-9\- ]+?)\s*</", RegexOptions.IgnoreCase)]
    private static partial Regex XmlPlate();

    [GeneratedRegex(@"<(?:\w+:)?confidence(?:Level)?>\s*([0-9.]+)\s*</", RegexOptions.IgnoreCase)]
    private static partial Regex XmlConfidence();

    [GeneratedRegex(@"(?:^|\n)(?:placa|plate)=([A-Za-z0-9\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FormPlate();
}
