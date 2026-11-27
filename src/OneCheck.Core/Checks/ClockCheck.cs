using System.Buffers.Binary;
using System.Net.Sockets;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>
/// Membandingkan jam komputer dengan header Date dari respons HTTPS (tanpa request tambahan),
/// cadangannya query SNTP ke server NTP.
/// </summary>
public static class ClockCheck
{
    public static async Task<ClockInfo> RunAsync(HttpCheckResult? http, IReadOnlyList<string> ntpServers, CancellationToken ct)
    {
        // Header Date presisi 1 detik; ambil dari probe yang paling cepat agar bias latensi kecil.
        var probe = new[] { http?.Https, http?.Http }
            .Where(p => p?.ServerDate is not null && p.StatusCode is not null)
            .OrderBy(p => p!.ElapsedMs)
            .FirstOrDefault();
        if (probe?.ServerDate is { } date)
            return new ClockInfo(DateTimeOffset.UtcNow, date.ToUniversalTime(), $"HTTP Date ({new Uri(probe.Url).Host})");

        foreach (var server in ntpServers.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var ntp = await QueryNtpAsync(server, ct);
            if (ntp is not null)
                return new ClockInfo(DateTimeOffset.UtcNow, ntp, $"NTP ({server})");
        }
        return new ClockInfo(DateTimeOffset.UtcNow, null, null);
    }

    /// <summary>SNTP sederhana (RFC 4330). Mengembalikan waktu server yang sudah dikoreksi setengah RTT.</summary>
    internal static async Task<DateTimeOffset?> QueryNtpAsync(string server, CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            udp.Connect(server, 123);
            var request = new byte[48];
            request[0] = 0x1B; // LI=0, VN=3, Mode=3 (client)
            var sent = DateTimeOffset.UtcNow;
            await udp.SendAsync(request, cts.Token);
            var response = await udp.ReceiveAsync(cts.Token);
            var received = DateTimeOffset.UtcNow;
            if (response.Buffer.Length < 48)
                return null;
            var serverTime = ParseNtpTimestamp(response.Buffer.AsSpan(40, 8));
            return serverTime + (received - sent) / 2;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    internal static DateTimeOffset ParseNtpTimestamp(ReadOnlySpan<byte> bytes)
    {
        var seconds = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        var fraction = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]);
        var epoch = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return epoch.AddSeconds(seconds).AddTicks((long)(fraction * (double)TimeSpan.TicksPerSecond / 4294967296.0));
    }
}
