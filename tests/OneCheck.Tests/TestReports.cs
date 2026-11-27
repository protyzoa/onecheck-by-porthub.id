using OneCheck.Core.Models;

namespace OneCheck.Tests;

/// <summary>Pembuat laporan sintetis untuk menguji mesin diagnosis tanpa jaringan sungguhan.</summary>
internal static class TestReports
{
    public static AdapterInfo Wifi(string? ip = "192.168.1.23", string? gateway = "192.168.1.1") => new(
        "{00000000-0000-0000-0000-000000000001}", "Wi-Fi", "Intel AX201", AdapterKind.Wifi, true,
        "3C:00:00:00:00:01", ip, 24, gateway, ["192.168.1.1"], true, "192.168.1.1", 866);

    public static AdapterInfo Lan() => Wifi() with { Name = "Ethernet", Description = "Realtek PCIe GbE", Kind = AdapterKind.Ethernet };

    public static WifiInfo Signal(int dbm, double rate = 866) =>
        new("KANTOR-5G", "a4:2b:00:00:00:1f", dbm, false, 80, rate, rate, "802.11ax (WiFi 6)", 36, "5 GHz", "WPA2-Personal-AES");

    public static PingStats Ok(string target, double avg = 10, int received = 10, int sent = 10) =>
        new(target, target, sent, received, (long)avg, avg, (long)avg + 5, 1);

    public static PingStats Lost(string target) => new(target, target, 10, 0, null, null, null, null, "timeout");

    public static DnsLookupResult DnsOk(string server = "192.168.1.1") => new(server, "google.com", true, ["142.250.4.100"], 12, null);

    public static DnsLookupResult DnsFail(string server = "192.168.1.1") => new(server, "google.com", false, [], 2000, "timeout");

    /// <summary>Laporan dengan jaringan normal (LAN). Ubah properti untuk mensimulasikan masalah.</summary>
    public static NetworkReport Healthy(AdapterInfo? adapter = null)
    {
        var r = new NetworkReport { CheckId = "OC-TEST-0001", StartedAt = DateTimeOffset.Now };
        r.System = new SystemInfo("PC-KASIR-01", "kasir", "Windows 11 Pro 23H2", TimeSpan.FromDays(3), "WIB");
        r.PrimaryAdapter = adapter ?? Lan();
        r.AllAdapters = [r.PrimaryAdapter];
        r.PingGateway = Ok("192.168.1.1", 2);
        r.PingPublic = [Ok("8.8.8.8", 20), Ok("1.1.1.1", 18)];
        r.PingDomain = Ok("google.com", 22);
        r.DnsSystem = DnsOk();
        r.DnsPublic = DnsOk("8.8.8.8");
        r.Trace = new TraceResult("8.8.8.8", [new(1, "192.168.1.1", 2), new(2, "10.10.0.1", 8), new(3, "8.8.8.8", 20)], true);
        r.Http = new HttpCheckResult(HttpOk(), HttpsOk(), false, null);
        r.PublicIp = new PublicIpInfo("36.80.1.2", "AS7713 PT Telekomunikasi Indonesia", "Jakarta", "ID", "ipinfo.io");
        r.ProxyVpn = NoProxy();
        r.Clock = new ClockInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(-2), "HTTP Date (www.google.com)");
        r.FinishedAt = r.StartedAt.AddSeconds(14);
        return r;
    }

    public static HttpProbe HttpOk() => new("http://www.msftconnecttest.com/connecttest.txt", true, 200, 40, HttpErrorKind.None, null, null, DateTimeOffset.UtcNow);

    public static HttpProbe HttpsOk() => new("https://www.google.com/generate_204", true, 204, 60, HttpErrorKind.None, null, null, DateTimeOffset.UtcNow);

    public static HttpProbe HttpsFail(HttpErrorKind kind = HttpErrorKind.ConnectionFailed) =>
        new("https://www.google.com/generate_204", false, null, 5000, kind, kind.ToString(), null, null);

    public static ProxyVpnInfo NoProxy() => new(false, null, false, null, null, true, null);

    public static void InternetDown(NetworkReport r)
    {
        r.PingPublic = [Lost("8.8.8.8"), Lost("1.1.1.1")];
        r.PingDomain = Lost("google.com");
        r.DnsSystem = DnsFail();
        r.DnsPublic = DnsFail("8.8.8.8");
        r.Http = new HttpCheckResult(HttpsFail(HttpErrorKind.Dns) with { Url = "http://www.msftconnecttest.com/connecttest.txt" }, HttpsFail(HttpErrorKind.Dns), false, null);
        r.PublicIp = null;
        r.Clock = new ClockInfo(DateTimeOffset.UtcNow, null, null);
    }

    public static CustomTargetResult Custom(string name, bool reachable, bool critical = false, int? maxLatency = null, double avg = 5, int? tcpPort = null, bool? tcpOk = null)
        => new(new CustomTarget { Name = name, Host = "10.0.0.10", Critical = critical, MaxLatencyMs = maxLatency, TcpPort = tcpPort },
            reachable ? Ok("10.0.0.10", avg) : Lost("10.0.0.10"), tcpOk, tcpOk == true ? 3 : null);
}
