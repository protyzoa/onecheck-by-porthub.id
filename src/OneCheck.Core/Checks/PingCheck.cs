using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>Setara ping -n N, plus statistik jitter.</summary>
public static class PingCheck
{
    private static readonly byte[] Payload = new byte[32];

    public static async Task<PingStats> RunAsync(string label, string target, int count, int timeoutMs, CancellationToken ct)
    {
        IPAddress address;
        try
        {
            address = await ResolveAsync(target, ct);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return new PingStats(label, target, count, 0, null, null, null, null, "err.dns_not_found");
        }

        var rtts = new List<long>();
        string? lastError = null;
        using var ping = new Ping();
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            try
            {
                var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(timeoutMs), Payload, null, ct);
                if (reply.Status == IPStatus.Success)
                    rtts.Add(reply.RoundtripTime);
                else
                    lastError = DescribeStatus(reply.Status);
            }
            catch (PingException ex)
            {
                lastError = ex.InnerException?.Message ?? ex.Message;
            }
            // jeda antar paket ±250 ms (lebih cepat dari ping Windows yang 1 detik)
            var wait = 250 - (int)sw.ElapsedMilliseconds;
            if (wait > 0 && i < count - 1)
                await Task.Delay(wait, ct);
        }

        return Summarize(label, target, count, rtts, rtts.Count == 0 ? lastError : null);
    }

    internal static PingStats Summarize(string label, string target, int sent, IReadOnlyList<long> rtts, string? error)
    {
        if (rtts.Count == 0)
            return new PingStats(label, target, sent, 0, null, null, null, null, error);

        double? jitter = null;
        if (rtts.Count > 1)
            jitter = Math.Round(rtts.Zip(rtts.Skip(1), (a, b) => Math.Abs(b - a)).Average(), 1);

        return new PingStats(label, target, sent, rtts.Count, rtts.Min(), Math.Round(rtts.Average(), 1), rtts.Max(), jitter);
    }

    internal static async Task<IPAddress> ResolveAsync(string host, CancellationToken ct)
    {
        if (IPAddress.TryParse(host, out var ip))
            return ip;
        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
               ?? addresses.FirstOrDefault()
               ?? throw new SocketException((int)SocketError.HostNotFound);
    }

    internal static string DescribeStatus(IPStatus status) => status switch
    {
        // Kunci teks (diterjemahkan saat ditampilkan, lihat Texts.Error).
        IPStatus.TimedOut => "err.timeout",
        IPStatus.DestinationHostUnreachable => "err.host_unreachable",
        IPStatus.DestinationNetworkUnreachable => "err.net_unreachable",
        IPStatus.TtlExpired => "err.ttl",
        _ => status.ToString(),
    };
}
