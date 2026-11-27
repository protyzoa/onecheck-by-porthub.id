using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneCheck.Core.Models;

/// <summary>Isi file onecheck.json. Semua nilai punya default sehingga file bersifat opsional.</summary>
public sealed class OneCheckConfig
{
    public const int MaxCustomTargets = 10;
    public const string FileName = "onecheck.json";

    public BrandingConfig Branding { get; set; } = new();
    public TargetsConfig Targets { get; set; } = new();
    public List<CustomTarget> CustomTargets { get; set; } = [];
    public ThresholdsConfig Thresholds { get; set; } = new();
    public int PingCount { get; set; } = 10;
    public int PingTimeoutMs { get; set; } = 1000;
    public int TraceMaxHops { get; set; } = 15;
    /// <summary>Bahasa awal: "id", "en", atau "auto" (ikut bahasa Windows). Pilihan user di aplikasi lebih diutamakan.</summary>
    public string Language { get; set; } = "id";
    public TechnicianModeConfig TechnicianMode { get; set; } = new();
    public List<string> VpnKeywords { get; set; } =
    [
        "WireGuard", "OpenVPN", "TAP-Windows", "Wintun", "FortiClient", "Fortinet", "AnyConnect", "Cisco",
        "GlobalProtect", "PANGP", "ZeroTier", "Tailscale", "NordVPN", "NordLynx", "ExpressVPN", "Surfshark",
        "ProtonVPN", "SoftEther", "Hamachi", "Pulse Secure", "Check Point", "SonicWall", "Cloudflare WARP",
    ];

    /// <summary>Target dari --target di command line. Tidak ikut disimpan ke onecheck.json.</summary>
    [JsonIgnore]
    public List<CustomTarget> CommandLineTargets { get; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static OneCheckConfig Parse(string json)
        => JsonSerializer.Deserialize<OneCheckConfig>(json, JsonOptions) ?? new OneCheckConfig();

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// Memuat onecheck.json dari folder yang sama dengan .exe. Jika tidak ada atau rusak, memakai default.
    /// </summary>
    public static (OneCheckConfig Config, string? Warning) LoadFromDirectory(string directory)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            return (new OneCheckConfig(), null);
        try
        {
            return (Parse(File.ReadAllText(path)), null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Pesan mentah; aplikasi menampilkannya dengan teks "ui.config_warning" sesuai bahasa.
            return (new OneCheckConfig(), ex.Message);
        }
    }

    /// <summary>
    /// Menambahkan target dari argumen command line: --target "Nama=host" (boleh diulang).
    /// Format host boleh "host:port" untuk sekaligus cek TCP.
    /// </summary>
    public void ApplyCommandLine(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], "--target", StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Count)
                continue;
            var target = CustomTarget.ParseCommandLine(args[++i]);
            if (target is not null)
                CommandLineTargets.Add(target);
        }
    }

    /// <summary>Menyimpan konfigurasi ke onecheck.json (dipakai Mode Teknisi). Komentar di file lama akan hilang.</summary>
    public void SaveToDirectory(string directory)
        => File.WriteAllText(Path.Combine(directory, FileName), ToJson());

    /// <summary>Target custom yang benar-benar dijalankan (config + command line, valid, maksimal 10).</summary>
    public IReadOnlyList<CustomTarget> EffectiveCustomTargets()
        => CustomTargets.Concat(CommandLineTargets)
            .Where(t => !string.IsNullOrWhiteSpace(t.Host))
            .Take(MaxCustomTargets)
            .ToList();
}

public sealed class BrandingConfig
{
    public string CompanyName { get; set; } = "PortHub";
    /// <summary>Logo klien (PNG/JPG) untuk layar awal, relatif ke folder OneCheck.exe. Kosong = logo OneCheck.</summary>
    public string LogoPath { get; set; } = "";
    /// <summary>Nomor WhatsApp support format internasional tanpa '+', mis. 62812xxxx. Kosong = tombol kirim nonaktif.</summary>
    public string SupportPhone { get; set; } = "";
    /// <summary>Kosong = teks bawaan sesuai bahasa. Jika diisi, dipakai untuk kedua bahasa.</summary>
    public string SupportText { get; set; } = "";
}

public sealed class TargetsConfig
{
    public List<string> PingPublic { get; set; } = ["8.8.8.8", "1.1.1.1"];
    public string DnsTestDomain { get; set; } = "google.com";
    /// <summary>Harus menjawab 200 dengan isi persis <see cref="HttpCheckExpectedBody"/>.</summary>
    public string HttpCheckUrl { get; set; } = "http://www.msftconnecttest.com/connecttest.txt";
    public string HttpCheckExpectedBody { get; set; } = "Microsoft Connect Test";
    /// <summary>Harus menjawab 204. URL berikutnya dipakai bila yang pertama gagal.</summary>
    public List<string> HttpsCheckUrls { get; set; } =
        ["https://www.google.com/generate_204", "https://cp.cloudflare.com/generate_204"];
    /// <summary>Halaman yang dibuka tombol "Buka Halaman Login" saat captive portal terdeteksi.</summary>
    public string CaptivePortalOpenUrl { get; set; } = "http://www.msftconnecttest.com/redirect";
    /// <summary>Layanan IP publik, dicoba berurutan. JSON (ipinfo.io / ip-api.com) atau teks polos (ipify).</summary>
    public List<string> PublicIpUrls { get; set; } =
        ["https://ipinfo.io/json", "http://ip-api.com/json", "https://api.ipify.org"];
    public List<string> NtpServers { get; set; } = ["time.windows.com", "pool.ntp.org"];
    public string DnsPublicServer { get; set; } = "8.8.8.8";
    public string TraceTarget { get; set; } = "8.8.8.8";
}

public sealed class CustomTarget
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int? Count { get; set; }
    public int? MaxLatencyMs { get; set; }
    public bool Critical { get; set; }
    public int? TcpPort { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Host : Name;

    internal static CustomTarget? ParseCommandLine(string value)
    {
        var eq = value.IndexOf('=');
        var name = eq > 0 ? value[..eq].Trim() : "";
        var hostPart = (eq > 0 ? value[(eq + 1)..] : value).Trim();
        if (hostPart.Length == 0)
            return null;

        int? port = null;
        var colon = hostPart.LastIndexOf(':');
        // host:port (abaikan alamat IPv6 yang punya banyak ':')
        if (colon > 0 && hostPart.IndexOf(':') == colon && int.TryParse(hostPart[(colon + 1)..], out var p) && p is > 0 and < 65536)
        {
            port = p;
            hostPart = hostPart[..colon];
        }
        return new CustomTarget { Name = name, Host = hostPart, TcpPort = port };
    }
}

public sealed class ThresholdsConfig
{
    public WifiSignalThresholds WifiSignalDbm { get; set; } = new();
    public double WifiLowRateMbps { get; set; } = 20;
    public double HighLatencyMs { get; set; } = 150;
    public double PacketLossWarnPercent { get; set; } = 5;
    public double PacketLossMajorPercent { get; set; } = 30;
    public double GatewayLossMajorPercent { get; set; } = 20;
    public double ClockSkewWarnMinutes { get; set; } = 5;
}

public sealed class TechnicianModeConfig
{
    /// <summary>false = Ctrl+Shift+T tidak melakukan apa-apa.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Kosong = tanpa PIN. Hanya untuk mencegah user awam mengubah setting, bukan pengaman.</summary>
    public string Pin { get; set; } = "";
}

public sealed class WifiSignalThresholds
{
    public int Excellent { get; set; } = -50;
    public int Good { get; set; } = -60;
    public int Fair { get; set; } = -70;
    public int Weak { get; set; } = -80;
}
