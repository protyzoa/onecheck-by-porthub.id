using System.Globalization;
using OneCheck.Core.Localization;
using OneCheck.Core.Models;

namespace OneCheck.Core.Diagnosis;

/// <summary>Warna tombol: Ok = hijau, Minor = oranye, Major = merah.</summary>
public enum Severity { Ok = 0, Minor = 1, Major = 2 }

public sealed record Finding(string Code, Severity Severity, string Message, string? Advice);

public enum ItemStatus { Ok, Warn, Fail, Unknown }

public sealed record ChecklistItem(string Label, ItemStatus Status);

public sealed record DiagnosisResult(Severity Overall, Finding Primary, IReadOnlyList<Finding> All, IReadOnlyList<ChecklistItem> Checklist);

/// <summary>
/// Aturan spek bagian 4. Semua aturan yang cocok dikumpulkan; aturan pertama (urutan spek) menjadi kesimpulan
/// utama, dan severity tertinggi menentukan warna tombol. Teks dihasilkan dalam bahasa yang diminta, jadi
/// mengganti bahasa cukup dengan memanggil Evaluate lagi untuk laporan yang sama.
/// </summary>
public static class DiagnosisEngine
{
    public static DiagnosisResult Evaluate(NetworkReport r, ThresholdsConfig t, Language lang = Language.Id)
    {
        string T(string key, params object?[] args) => Texts.Get(lang, key, args);
        static string N0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        var findings = new List<Finding>();
        var a = r.PrimaryAdapter;

        // E01 — tidak tersambung sama sekali
        if (a is null || !a.IsUp || a.IPv4 is null)
        {
            findings.Add(new("E01", Severity.Major, T("dx.E01.msg"), T("dx.E01.adv")));
            return Build(findings, r, t, lang);
        }

        // Internet dianggap ada jika ping ATAU HTTPS berhasil (sebagian jaringan kantor memblokir ping).
        var internetOk = IsInternetOk(r);
        var captive = r.Http?.CaptivePortal == true;
        var gatewayLoss = r.PingGateway?.LossPercent;

        // W01 / E06 — sinyal WiFi
        if (r.Wifi?.RssiDbm is { } rssi && rssi < t.WifiSignalDbm.Fair)
        {
            if (rssi < t.WifiSignalDbm.Weak && gatewayLoss > t.GatewayLossMajorPercent)
                findings.Add(new("E06", Severity.Major, T("dx.E06.msg", rssi), T("dx.E06.adv")));
            else
                findings.Add(new("W01", Severity.Minor, T("dx.W01.msg", rssi), T("dx.W01.adv")));
        }

        // E02 — tidak dapat IP dari router
        if (a.IsApipa || !a.HasGateway)
            findings.Add(new("E02", Severity.Major, T("dx.E02.msg"), T("dx.E02.adv")));
        // E03 — router tidak menjawab (dan internet juga mati; jika internet jalan berarti router hanya memblokir ping)
        else if (r.PingGateway is { AllLost: true } && !internetOk && !captive)
            findings.Add(new("E03", Severity.Major, T("dx.E03.msg"), T("dx.E03.adv")));
        // E04 — router OK, internet mati
        else if (!internetOk && !captive && r.PingPublic.Count > 0 && r.DnsPublic is not { Success: true })
            findings.Add(new("E04", Severity.Major, T("dx.E04.msg"), T("dx.E04.adv")));

        // E05 — internet ada, DNS bermasalah
        if (r.PingPublic.Any(p => !p.AllLost) && r.DnsSystem is { Success: false })
            findings.Add(new("E05", Severity.Major, T("dx.E05.msg"),
                r.DnsPublic is { Success: true } ? T("dx.E05.adv_local") : T("dx.E05.adv_all")));

        var https = r.Http?.Https;
        var httpsFailed = https is { Success: false };
        var proxyOrVpn = r.ProxyVpn is { } pv && (pv.ProxyActive || pv.VpnActive);
        var skew = r.Clock?.Skew;
        var clockWrong = skew is { } s && Math.Abs(s.TotalMinutes) > t.ClockSkewWarnMinutes;

        // E09 — jam salah membuat HTTPS ditolak
        if (clockWrong && https is { Error: HttpErrorKind.Certificate })
            findings.Add(new("E09", Severity.Major, T("dx.E09.msg", FormatSkew(skew!.Value, lang)), T("dx.E09.adv")));
        // E08 — ping & DNS jalan, tapi website tidak bisa dibuka (bukan karena portal/proxy)
        else if (httpsFailed && !captive && !proxyOrVpn && r.PingPublic.Any(p => !p.AllLost) && r.DnsSystem is { Success: true })
        {
            var cert = https!.Error == HttpErrorKind.Certificate;
            findings.Add(new("E08", Severity.Major,
                T(cert ? "dx.E08.msg_cert" : "dx.E08.msg"), T(cert ? "dx.E08.adv_cert" : "dx.E08.adv")));
        }

        // W02 — captive portal
        if (captive)
            findings.Add(new("W02", Severity.Minor, T("dx.W02.msg"), T("dx.W02.adv", T("action.portal"))));

        // W03 — VPN/proxy aktif dan HTTPS gagal
        if (proxyOrVpn && httpsFailed && !captive)
        {
            var vpn = r.ProxyVpn!.VpnActive;
            findings.Add(new("W03", Severity.Minor, T("dx.W03.msg", vpn ? "VPN" : "Proxy"),
                T(vpn ? "dx.W03.adv_vpn" : "dx.W03.adv_proxy")));
        }

        // W04 — jam meleset (tanpa error sertifikat)
        if (clockWrong && !findings.Any(f => f.Code == "E09"))
            findings.Add(new("W04", Severity.Minor, T("dx.W04.msg", FormatSkew(skew!.Value, lang)), T("dx.W04.adv")));

        // W06 — target kritis gagal
        foreach (var c in r.CustomTargets.Where(c => c.Target.Critical && !c.Reachable))
            findings.Add(new("W06", Severity.Minor, T("dx.W06.msg", c.Target.DisplayName), T("dx.W06.adv")));

        // W05 / E07 — tidak stabil / lambat
        var reachable = r.PingPublic.Where(p => !p.AllLost).ToList();
        if (reachable.Count > 0)
        {
            var loss = reachable.Average(p => p.LossPercent);
            var latency = reachable.Average(p => p.AvgMs ?? 0);
            if (loss > t.PacketLossMajorPercent)
                findings.Add(new("E07", Severity.Major, T("dx.E07.msg", N0(loss)), T("dx.E07.adv")));
            else if (loss > t.PacketLossWarnPercent || latency > t.HighLatencyMs)
                findings.Add(new("W05", Severity.Minor,
                    loss > t.PacketLossWarnPercent ? T("dx.W05.msg_loss", N0(loss)) : T("dx.W05.msg_slow", N0(latency)),
                    T("dx.W05.adv")));
        }

        // W07 — sinyal kuat tapi link rate rendah
        if (r.Wifi is { RssiDbm: { } strong } w && strong >= t.WifiSignalDbm.Good
            && Math.Max(w.RxRateMbps, w.TxRateMbps) is > 0 and var rate && rate < t.WifiLowRateMbps)
            findings.Add(new("W07", Severity.Minor, T("dx.W07.msg", N0(rate)), T("dx.W07.adv")));

        // W08 — target non-kritis gagal / lambat
        foreach (var c in r.CustomTargets.Where(c => !(c.Target.Critical && !c.Reachable)))
        {
            if (!c.Reachable)
                findings.Add(new("W08", Severity.Minor, T("dx.W08.down", c.Target.DisplayName), null));
            else if (c.Target.MaxLatencyMs is { } max && c.Ping.AvgMs > max)
                findings.Add(new("W08", Severity.Minor, T("dx.W08.slow", c.Target.DisplayName, N0(c.Ping.AvgMs!.Value)), null));
        }

        return Build(findings, r, t, lang);
    }

    private static DiagnosisResult Build(List<Finding> findings, NetworkReport r, ThresholdsConfig t, Language lang)
    {
        var overall = findings.Count == 0 ? Severity.Ok : findings.Max(f => f.Severity);
        // Kesimpulan utama = aturan pertama dengan severity tertinggi.
        var primary = findings.FirstOrDefault(f => f.Severity == overall)
                      ?? new Finding("OK", Severity.Ok, Texts.Get(lang, "dx.ok.msg"), null);
        return new DiagnosisResult(overall, primary, findings, BuildChecklist(r, t, lang));
    }

    private static bool IsInternetOk(NetworkReport r)
        => r.PingPublic.Any(p => !p.AllLost) || r.PingDomain is { AllLost: false } || r.Http is { HttpsOk: true };

    /// <summary>Selisih jam dalam bahasa awam, mis. "lebih cepat 2 jam" / "2 hours fast".</summary>
    public static string FormatSkew(TimeSpan skew, Language lang = Language.Id)
    {
        var abs = skew.Duration();
        var amount = abs.TotalDays >= 1 ? Texts.Duration(lang, (int)abs.TotalDays, "day")
            : abs.TotalHours >= 1 ? Texts.Duration(lang, (int)abs.TotalHours, "hour")
            : abs.TotalMinutes >= 1 ? Texts.Duration(lang, (int)abs.TotalMinutes, "minute")
            : Texts.Duration(lang, (int)abs.TotalSeconds, "second");
        return Texts.Get(lang, skew > TimeSpan.Zero ? "skew.fast" : "skew.slow", amount);
    }

    public static (string Label, ItemStatus Status) SignalCategory(int rssi, WifiSignalThresholds t, Language lang = Language.Id)
    {
        var (key, status) = rssi switch
        {
            _ when rssi >= t.Excellent => ("signal.excellent", ItemStatus.Ok),
            _ when rssi >= t.Good => ("signal.good", ItemStatus.Ok),
            _ when rssi >= t.Fair => ("signal.fair", ItemStatus.Ok),
            _ when rssi >= t.Weak => ("signal.weak", ItemStatus.Warn),
            _ => ("signal.veryweak", ItemStatus.Fail),
        };
        return (Texts.Get(lang, key), status);
    }

    private static List<ChecklistItem> BuildChecklist(NetworkReport r, ThresholdsConfig t, Language lang)
    {
        string T(string key, params object?[] args) => Texts.Get(lang, key, args);

        var items = new List<ChecklistItem>();
        var a = r.PrimaryAdapter;
        var connected = a is { IsUp: true, IPv4: not null };

        items.Add(new(T("chk.nic"), connected && !a!.IsApipa ? ItemStatus.Ok : ItemStatus.Fail));
        if (!connected)
            return items;

        if (a!.Kind == AdapterKind.Wifi)
        {
            if (r.Wifi?.RssiDbm is { } rssi)
            {
                var (label, status) = SignalCategory(rssi, t.WifiSignalDbm, lang);
                items.Add(new(T("chk.wifi", label), status));
            }
            else
                items.Add(new(T("chk.wifi_plain"), ItemStatus.Unknown));
        }

        var internetOk = IsInternetOk(r);

        items.Add(new(T("chk.router"), r.PingGateway switch
        {
            null => a.HasGateway ? ItemStatus.Unknown : ItemStatus.Fail,
            { AllLost: false } g => g.LossPercent > t.PacketLossWarnPercent ? ItemStatus.Warn : ItemStatus.Ok,
            _ => internetOk ? ItemStatus.Ok : ItemStatus.Fail,
        }));

        var reachable = r.PingPublic.Where(p => !p.AllLost).ToList();
        items.Add(new(T("chk.internet"), reachable.Count == 0
            ? (internetOk ? ItemStatus.Ok : r.PingPublic.Count == 0 ? ItemStatus.Unknown : ItemStatus.Fail)
            : reachable.Average(p => p.LossPercent) > t.PacketLossWarnPercent || reachable.Average(p => p.AvgMs ?? 0) > t.HighLatencyMs
                ? ItemStatus.Warn
                : ItemStatus.Ok));

        items.Add(new(T("chk.dns"), r.DnsSystem switch
        {
            null => ItemStatus.Unknown,
            { Success: true } => ItemStatus.Ok,
            _ => ItemStatus.Fail,
        }));

        if (r.Http is { } http)
            items.Add(http switch
            {
                { HttpsOk: true } => new(T("chk.web"), ItemStatus.Ok),
                { CaptivePortal: true } => new(T("chk.web_login"), ItemStatus.Warn),
                { Https: null } => new(T("chk.web"), ItemStatus.Unknown),
                _ => new(T("chk.web"), ItemStatus.Fail),
            });

        if (r.ProxyVpn is { } pv)
        {
            var httpsFailed = r.Http?.Https is { Success: false };
            if (!pv.VpnActive && !pv.ProxyActive)
                items.Add(new(T("chk.noproxy"), ItemStatus.Ok));
            else
                items.Add(new(T(pv.VpnActive ? "chk.vpn" : "chk.proxy"), httpsFailed ? ItemStatus.Warn : ItemStatus.Ok));
        }

        if (r.Clock is { } clock)
            items.Add(new(T("chk.clock"), clock.Skew is not { } skew
                ? ItemStatus.Unknown
                : Math.Abs(skew.TotalMinutes) > t.ClockSkewWarnMinutes ? ItemStatus.Warn : ItemStatus.Ok));

        foreach (var c in r.CustomTargets)
        {
            var status = !c.Reachable
                ? (c.Target.Critical ? ItemStatus.Fail : ItemStatus.Warn)
                : c.Target.MaxLatencyMs is { } max && c.Ping.AvgMs > max ? ItemStatus.Warn : ItemStatus.Ok;
            items.Add(new(c.Target.DisplayName, status));
        }
        return items;
    }
}
