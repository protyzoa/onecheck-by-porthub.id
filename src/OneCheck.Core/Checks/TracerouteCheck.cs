using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>
/// Traceroute buatan sendiri: semua TTL 1..maxHops dikirim paralel (2 probe per hop) sehingga selesai
/// dalam ±timeout, jauh lebih cepat dari tracert. Tidak butuh admin.
/// </summary>
public static class TracerouteCheck
{
    private const int ProbesPerHop = 2;
    private static readonly byte[] Payload = new byte[32];

    public static async Task<TraceResult> RunAsync(string target, int maxHops, int timeoutMs, CancellationToken ct)
    {
        var address = await PingCheck.ResolveAsync(target, ct);

        var tasks = Enumerable.Range(1, maxHops)
            .Select(ttl => ProbeHopAsync(address, ttl, timeoutMs, ct))
            .ToArray();
        var hops = await Task.WhenAll(tasks);

        // Potong setelah hop pertama yang mencapai tujuan.
        var result = new List<TraceHop>();
        var reached = false;
        foreach (var (hop, isDestination) in hops)
        {
            result.Add(hop);
            if (isDestination)
            {
                reached = true;
                break;
            }
        }
        // Buang ekor hop kosong ("*") yang berurutan di akhir jika tujuan tidak tercapai, sisakan satu.
        if (!reached)
        {
            var lastResponding = result.FindLastIndex(h => h.Address is not null);
            if (lastResponding >= 0 && lastResponding + 2 < result.Count)
                result.RemoveRange(lastResponding + 2, result.Count - lastResponding - 2);
        }
        return new TraceResult(target, result, reached);
    }

    private static async Task<(TraceHop Hop, bool IsDestination)> ProbeHopAsync(IPAddress address, int ttl, int timeoutMs, CancellationToken ct)
    {
        using var ping = new Ping();
        var options = new PingOptions(ttl, dontFragment: true);
        string? hopAddress = null;
        long? best = null;
        var destination = false;

        for (var i = 0; i < ProbesPerHop; i++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(timeoutMs), Payload, options, ct);
                sw.Stop();
                if (reply.Status is IPStatus.Success or IPStatus.TtlExpired or IPStatus.TimeExceeded)
                {
                    hopAddress = reply.Address.ToString();
                    // RoundtripTime bernilai 0 untuk TtlExpired, jadi pakai stopwatch.
                    var rtt = reply.Status == IPStatus.Success && reply.RoundtripTime > 0 ? reply.RoundtripTime : sw.ElapsedMilliseconds;
                    best = best is null ? rtt : Math.Min(best.Value, rtt);
                    destination |= reply.Status == IPStatus.Success;
                }
            }
            catch (PingException)
            {
                // abaikan, hop dianggap "*"
            }
        }
        return (new TraceHop(ttl, hopAddress, best), destination);
    }
}
