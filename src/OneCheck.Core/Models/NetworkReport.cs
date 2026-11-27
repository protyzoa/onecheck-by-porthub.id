namespace OneCheck.Core.Models;

/// <summary>Seluruh data mentah hasil pemeriksaan satu kali tes.</summary>
public sealed class NetworkReport
{
    public required string CheckId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; set; }

    public SystemInfo? System { get; set; }
    public AdapterInfo? PrimaryAdapter { get; set; }
    public IReadOnlyList<AdapterInfo> AllAdapters { get; set; } = [];
    public WifiInfo? Wifi { get; set; }

    public PingStats? PingGateway { get; set; }
    public IReadOnlyList<PingStats> PingPublic { get; set; } = [];
    public PingStats? PingDomain { get; set; }

    public DnsLookupResult? DnsSystem { get; set; }
    public DnsLookupResult? DnsPublic { get; set; }

    public TraceResult? Trace { get; set; }

    public IReadOnlyList<CustomTargetResult> CustomTargets { get; set; } = [];

    public HttpCheckResult? Http { get; set; }
    public PublicIpInfo? PublicIp { get; set; }
    public ProxyVpnInfo? ProxyVpn { get; set; }
    public ClockInfo? Clock { get; set; }

    /// <summary>Pemeriksaan yang gagal dijalankan karena error internal (bukan masalah jaringan).</summary>
    public Dictionary<string, string> InternalErrors { get; } = new();

    public TimeSpan Duration => FinishedAt - StartedAt;
}

public sealed record SystemInfo(
    string ComputerName,
    string UserName,
    string OsName,
    TimeSpan Uptime,
    string TimeZoneLabel);

public enum AdapterKind { Wifi, Ethernet, Other }

public sealed record AdapterInfo(
    string Id,
    string Name,
    string Description,
    AdapterKind Kind,
    bool IsUp,
    string MacAddress,
    string? IPv4,
    int? PrefixLength,
    string? Gateway,
    IReadOnlyList<string> DnsServers,
    bool DhcpEnabled,
    string? DhcpServer,
    long SpeedMbps)
{
    /// <summary>169.254.x.x = DHCP gagal, Windows memberi alamat otomatis.</summary>
    public bool IsApipa => IPv4 is not null && IPv4.StartsWith("169.254.", StringComparison.Ordinal);

    public bool HasGateway => !string.IsNullOrEmpty(Gateway);
}

public sealed record WifiInfo(
    string Ssid,
    string? Bssid,
    int? RssiDbm,
    bool RssiIsEstimated,
    int SignalQualityPercent,
    double RxRateMbps,
    double TxRateMbps,
    string PhyType,
    int? Channel,
    string? Band,
    string Security);

public sealed record PingStats(
    string Label,
    string Target,
    int Sent,
    int Received,
    long? MinMs,
    double? AvgMs,
    long? MaxMs,
    double? JitterMs,
    string? Error = null)
{
    public double LossPercent => Sent == 0 ? 100 : Math.Round((Sent - Received) * 100.0 / Sent, 1);
    public bool AllLost => Received == 0;
}

public sealed record DnsLookupResult(
    string Server,
    string Domain,
    bool Success,
    IReadOnlyList<string> Addresses,
    long ElapsedMs,
    string? Error);

public sealed record TraceHop(int Hop, string? Address, long? RttMs);

public sealed record TraceResult(string Target, IReadOnlyList<TraceHop> Hops, bool ReachedTarget)
{
    public TraceHop? LastResponding => Hops.LastOrDefault(h => h.Address is not null);
}

public enum HttpErrorKind { None, Timeout, Dns, ConnectionFailed, Certificate, Proxy, UnexpectedResponse, Other }

public sealed record HttpProbe(
    string Url,
    bool Success,
    int? StatusCode,
    long ElapsedMs,
    HttpErrorKind Error,
    string? ErrorMessage,
    string? RedirectLocation,
    DateTimeOffset? ServerDate);

public sealed record HttpCheckResult(HttpProbe? Http, HttpProbe? Https, bool CaptivePortal, string? CaptivePortalUrl)
{
    public bool HttpsOk => Https is { Success: true };
}

public sealed record PublicIpInfo(string Ip, string? Isp, string? City, string? Country, string Source);

public sealed record ProxyVpnInfo(
    bool VpnActive,
    string? VpnName,
    bool VpnAllTraffic,
    string? UserProxy,
    string? PacUrl,
    bool AutoDetect,
    string? WinHttpProxy)
{
    public bool ProxyActive => UserProxy is not null || PacUrl is not null || WinHttpProxy is not null;
}

public sealed record ClockInfo(DateTimeOffset LocalUtc, DateTimeOffset? ReferenceUtc, string? Source)
{
    /// <summary>Positif = jam komputer lebih cepat dari jam server.</summary>
    public TimeSpan? Skew => ReferenceUtc is { } r ? LocalUtc - r : null;
}

public sealed record CustomTargetResult(
    CustomTarget Target,
    PingStats Ping,
    bool? TcpOk,
    long? TcpMs)
{
    /// <summary>Target dianggap terjangkau jika ping berhasil atau (bila diatur) port TCP terbuka.</summary>
    public bool Reachable => !Ping.AllLost || TcpOk == true;

    public bool IcmpBlockedButTcpOk => Ping.AllLost && TcpOk == true;
}
