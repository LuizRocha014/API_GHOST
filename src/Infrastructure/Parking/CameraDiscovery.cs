using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Application.Abstractions;
using Application.Parking;

namespace Infrastructure.Parking;

/// <summary>
/// Procura câmeras e DVRs na rede local do servidor:
/// <list type="number">
/// <item>ONVIF WS-Discovery: pergunta em multicast (239.255.255.250:3702) e as câmeras ONVIF respondem com nome e endereço.</item>
/// <item>Varredura da rede (/24 de cada placa de rede, ou a informada): quem responde nas portas típicas de câmera e DVR —
/// 554 (RTSP), 80/8080 (web), 8000 (Hikvision), 37777 (Intelbras/Dahua), 34567 (DVRs genéricos XMEye).</item>
/// </list>
/// </summary>
public sealed partial class CameraDiscovery : ICameraDiscovery
{
    private static readonly int[] Ports = [554, 80, 8080, 8000, 37777, 34567];

    public async Task<IReadOnlyList<DiscoveredDeviceDto>> DiscoverAsync(string? subnet, CancellationToken ct = default)
    {
        var onvifTask = OnvifProbeAsync(TimeSpan.FromSeconds(3), ct);
        var prefixes = string.IsNullOrWhiteSpace(subnet) ? LocalPrefixes() : [NormalizePrefix(subnet)];
        var scanTask = ScanAsync(prefixes, ct);
        var onvif = await onvifTask;
        var scan = await scanTask;

        var ips = onvif.Keys.Union(scan.Keys).OrderBy(ip => IPAddress.TryParse(ip, out var a) ? BitConverter.ToUInt32(a.GetAddressBytes().Reverse().ToArray()) : 0);
        return ips.Select(ip =>
        {
            var ports = scan.TryGetValue(ip, out var p) ? p.OrderBy(x => x).ToList() : [];
            onvif.TryGetValue(ip, out var info);
            var brand = info.Brand ?? (ports.Contains(8000) ? "hikvision" : ports.Contains(37777) ? "intelbras" : null);
            var kind = ports.Contains(34567) || ports.Contains(37777) || (info.Name ?? string.Empty).Contains("DVR", StringComparison.OrdinalIgnoreCase)
                || (info.Name ?? string.Empty).Contains("NVR", StringComparison.OrdinalIgnoreCase) ? "dvr" : "ip";
            return new DiscoveredDeviceDto(ip, ports, brand, info.Name, info.XAddr, kind);
        }).Where(d => d.Onvif is not null || d.Ports.Contains(554) || d.Ports.Contains(8000) || d.Ports.Contains(37777) || d.Ports.Contains(34567))
          .ToList();
    }

    private static string NormalizePrefix(string subnet)
    {
        var parts = subnet.Trim().Split('/')[0].Split('.');
        if (parts.Length < 3 || parts.Take(3).Any(p => !byte.TryParse(p, out _)))
            throw new Application.Agenda.AgendaValidationException("Rede inválida (ex.: 192.168.0.0 ou 192.168.0).");
        return string.Join('.', parts.Take(3));
    }

    /// <summary>Prefixos /24 das placas de rede ativas (IPv4 privados).</summary>
    private static List<string> LocalPrefixes() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.GetAddressBytes())
            .Where(b => b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31))
            .Select(b => $"{b[0]}.{b[1]}.{b[2]}")
            .Distinct().Take(4).ToList();

    private static async Task<Dictionary<string, HashSet<int>>> ScanAsync(IReadOnlyList<string> prefixes, CancellationToken ct)
    {
        var found = new Dictionary<string, HashSet<int>>();
        using var gate = new SemaphoreSlim(256);
        var tasks = new List<Task>();
        foreach (var prefix in prefixes)
            for (var host = 1; host < 255; host++)
                foreach (var port in Ports)
                {
                    var ip = $"{prefix}.{host}";
                    var p = port;
                    tasks.Add(Task.Run(async () =>
                    {
                        await gate.WaitAsync(ct);
                        try
                        {
                            if (await OpenAsync(ip, p, ct))
                                lock (found)
                                {
                                    if (!found.TryGetValue(ip, out var set)) found[ip] = set = [];
                                    set.Add(p);
                                }
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }, ct));
                }
        await Task.WhenAll(tasks);
        return found;
    }

    private static async Task<bool> OpenAsync(string ip, int port, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(400));
        try
        {
            await client.ConnectAsync(ip, port, timeout.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<Dictionary<string, (string? Name, string? Brand, string? XAddr)>> OnvifProbeAsync(TimeSpan wait, CancellationToken ct)
    {
        var result = new Dictionary<string, (string?, string?, string?)>();
        var probe = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <e:Envelope xmlns:e="http://www.w3.org/2003/05/soap-envelope" xmlns:w="http://schemas.xmlsoap.org/ws/2004/08/addressing"
              xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery" xmlns:dn="http://www.onvif.org/ver10/network/wsdl">
              <e:Header><w:MessageID>uuid:{Guid.NewGuid()}</w:MessageID><w:To e:mustUnderstand="true">urn:schemas-xmlsoap-org:ws:2005:04:discovery</w:To>
              <w:Action e:mustUnderstand="true">http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</w:Action></e:Header>
              <e:Body><d:Probe><d:Types>dn:NetworkVideoTransmitter</d:Types></d:Probe></e:Body>
            </e:Envelope>
            """;
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            var data = Encoding.UTF8.GetBytes(probe);
            await udp.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702));
            var end = DateTime.UtcNow + wait;
            while (DateTime.UtcNow < end)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(end - DateTime.UtcNow);
                UdpReceiveResult r;
                try
                {
                    r = await udp.ReceiveAsync(cts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    break;
                }
                var xml = Encoding.UTF8.GetString(r.Buffer);
                var ip = r.RemoteEndPoint.Address.ToString();
                var xaddr = XAddrs().Match(xml) is { Success: true } m ? m.Groups[1].Value.Split(' ')[0] : null;
                var scopes = Scopes().Match(xml) is { Success: true } s ? s.Groups[1].Value : string.Empty;
                string? Scope(string key) => Regex.Match(scopes, $@"onvif://www\.onvif\.org/{key}/([^\s<]+)") is { Success: true } x
                    ? Uri.UnescapeDataString(x.Groups[1].Value) : null;
                var name = Scope("name") ?? Scope("hardware");
                var hw = $"{Scope("name")} {Scope("hardware")} {Scope("mac")}".ToLowerInvariant();
                var brand = hw.Contains("hik") ? "hikvision" : hw.Contains("intelbras") ? "intelbras" : hw.Contains("dahua") ? "dahua" : null;
                result[ip] = (name, brand, xaddr);
            }
        }
        catch (SocketException)
        {
            // Sem multicast nesta máquina: fica só a varredura de portas.
        }
        return result;
    }

    [GeneratedRegex(@"<[^>]*XAddrs>([^<]+)</[^>]*XAddrs>")]
    private static partial Regex XAddrs();

    [GeneratedRegex(@"<[^>]*Scopes[^>]*>([^<]+)</[^>]*Scopes>")]
    private static partial Regex Scopes();
}
