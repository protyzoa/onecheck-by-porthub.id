using System.Globalization;
using System.Text;
using OneCheck.Core.Diagnosis;
using OneCheck.Core.Localization;
using OneCheck.Core.Models;

namespace OneCheck.Core.Reporting;

/// <summary>
/// Menyusun ringkasan teknis (panel kanan), laporan teks lengkap (Salin Teks / WhatsApp) dalam bahasa yang
/// dipilih, dan ringkasan padat untuk QR (selalu kode/English singkat agar netral bahasa).
/// </summary>
public static class ReportFormatter
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    public static string OverallLabel(Severity s, Language lang = Language.Id) => Texts.Get(lang, s switch
    {
        Severity.Ok => "overall.ok",
        Severity.Minor => "overall.minor",
        _ => "overall.major",
    });

    public static string FormatTimestamp(DateTimeOffset t) => t.ToString("dd/MM/yyyy HH:mm:ss", Id);

    /// <summary>Baris "Kunci : Nilai" ringkas untuk panel teknis di layar hasil.</summary>
    public static IReadOnlyList<(string Key, string Value)> SummaryLines(NetworkReport r, Language lang = Language.Id)
    {
        string T(string key, params object?[] args) => Texts.Get(lang, key, args);

        // Dibuat padat (satu baris per hal) agar muat satu layar/foto. Detail lengkap ada di laporan teks & QR.
        var lines = new List<(string, string)>();
        if (r.System is { } s)
            lines.Add((T("sum.computer"), $"{s.ComputerName} · {s.OsName} · up {FormatUptime(s.Uptime, lang)}"));

        var a = r.PrimaryAdapter;
        if (a is null)
        {
            lines.Add((T("sum.adapter"), T("val.none_connected")));
        }
        else
        {
            lines.Add((T("sum.adapter"), $"{a.Name} · {Truncate(a.Description, 34)} · {(a.IsUp ? "UP" : "DOWN")}"
                                         + (a.Kind == AdapterKind.Ethernet && a.SpeedMbps > 0 ? $" · {FormatSpeed(a.SpeedMbps)}" : "")));
            if (r.Wifi is { } w)
            {
                lines.Add((T("sum.wifi"), $"{w.Ssid} · {FormatDbm(w)} ({w.SignalQualityPercent}%) · {ShortPhy(w.PhyType)}"
                                          + (w.Band is null ? "" : $" {w.Band}") + (w.Channel is null ? "" : $" ch{w.Channel}")));
                lines.Add((T("sum.linkrate"), Inv($"RX {w.RxRateMbps:0} / TX {w.TxRateMbps:0} Mbps (Windows)")));
            }
            lines.Add((T("sum.ipdns"), (a.IPv4 is null ? "-" : $"{a.IPv4}/{a.PrefixLength} ({(a.DhcpEnabled ? "DHCP" : T("val.manual"))})")
                                       + (a.IsApipa ? " ⚠ APIPA" : "")
                                       + (a.OtherIPv4.Count > 0 ? " " + T("val.more_ips", a.OtherIPv4.Count) : "")
                                       + $" · DNS {(a.DnsServers.Count == 0 ? "-" : string.Join(", ", a.DnsServers.Take(2)))}"));
            lines.Add((T("sum.gateway"), (a.Gateway ?? "-")
                                         + (a.GatewayOutsideSubnet ? " " + T("val.gw_outside") : "")
                                         + (r.PingGateway is { } g ? $" · {PingShort(g, lang)}" : "")));
        }

        if (r.PingPublic.Count > 0)
            lines.Add((T("sum.ping_internet"), string.Join(" · ", r.PingPublic.Select(p => $"{p.Target} {PingCompact(p)}"))));
        if (r.PingDomain is { } d)
            lines.Add((T("sum.ping", d.Target), PingShort(d, lang)));
        if (r.DnsSystem is { } dns)
            lines.Add((T("sum.dns", dns.Domain), (dns.Success ? $"OK {dns.ElapsedMs} ms" : $"{T("val.failed")} ({Texts.Error(lang, dns.Error)})")
                                                 + (r.DnsPublic is { } pub ? $" · via {pub.Server}: {(pub.Success ? $"OK {pub.ElapsedMs} ms" : T("val.failed"))}" : "")));
        if (r.Trace is { } tr)
            lines.Add((T("sum.trace"), TraceShort(tr, lang)));
        if (r.Http is { } http)
            lines.Add((T("sum.http"), $"{ProbeShort(http.Http, lang)} / {ProbeShort(http.Https, lang)}"
                                      + (http.CaptivePortal ? " · CAPTIVE PORTAL" : "")));
        if (r.PublicIp is { } ip)
            lines.Add((T("sum.publicip"), ip.Ip + (ip.Isp is null ? "" : $" · {Truncate(ip.Isp, 46)}")
                                          + (r.ProxyVpn is { VpnActive: true, VpnAllTraffic: true } ? " (VPN)" : "")));
        else if (r.Http is not null)
            lines.Add((T("sum.publicip"), T("val.cannot_check")));
        if (r.ProxyVpn is { } pv)
            lines.Add((T("sum.vpnproxy"),
                (pv.VpnActive ? T("val.vpn", ShortVpnName(pv.VpnName)) + (pv.VpnAllTraffic ? " " + T("val.all_traffic") : "") : T("val.vpn_none"))
                + " · " + (pv.ProxyActive ? T("val.proxy", pv.UserProxy ?? pv.PacUrl ?? pv.WinHttpProxy) : T("val.proxy_none"))));
        foreach (var c in r.CustomTargets)
            lines.Add(($"{c.Target.DisplayName}", $"{c.Target.Host} · {CustomShort(c, lang)}"));
        lines.Add((T("sum.clock"), $"{r.StartedAt:HH:mm} {r.System?.TimeZoneLabel} · {ClockShort(r.Clock, lang)}"));
        return lines;
    }

    /// <summary>
    /// Ringkasan sangat padat untuk QR code agar tetap bisa di-scan dari foto layar. Netral bahasa (kode singkat).
    /// </summary>
    public static string CompactSummary(NetworkReport r, DiagnosisResult dx)
    {
        // Hanya hal yang membantu diagnosis; baris "normal" yang tidak informatif dihilangkan agar QR tidak terlalu padat.
        var sb = new StringBuilder();
        sb.AppendLine($"OneCheck {r.CheckId} {r.StartedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"{dx.Overall.ToString().ToUpperInvariant()} " + string.Join(",", dx.All.Select(f => f.Code).Distinct().DefaultIfEmpty("OK")));
        if (r.System is { } s)
            sb.AppendLine($"PC {s.ComputerName} up {UptimeTiny(s.Uptime)}");
        if (r.PrimaryAdapter is { } a)
            sb.AppendLine($"{(a.Kind == AdapterKind.Wifi ? "WIFI" : a.Kind == AdapterKind.Ethernet ? "LAN" : "NIC")} {a.IPv4}/{a.PrefixLength}"
                          + (a.OtherIPv4.Count > 0 ? $" +{a.OtherIPv4.Count}ip" : "")
                          + $" gw {a.Gateway ?? "-"}{(a.GatewayOutsideSubnet ? "!" : "")} dns {string.Join(",", a.DnsServers.Take(2))}");
        else
            sb.AppendLine("NIC none");
        if (r.Wifi is { } w)
            sb.AppendLine(Inv($"SSID {w.Ssid} {w.RssiDbm}dBm {w.RxRateMbps:0}/{w.TxRateMbps:0}M ch{w.Channel}"));
        var pings = new List<string>();
        if (r.PingGateway is { } g)
            pings.Add($"gw {PingTiny(g)}");
        pings.AddRange(r.PingPublic.Select(PingTiny));
        if (pings.Count > 0)
            sb.AppendLine("PING " + string.Join(" ", pings));
        if (r.DnsSystem is { Success: false } || r.DnsPublic is { Success: false })
            sb.AppendLine($"DNS sys {(r.DnsSystem?.Success == true ? "ok" : "FAIL")} pub {(r.DnsPublic?.Success == true ? "ok" : "FAIL")}");
        if (r.Http is { } http && (!http.HttpsOk || http.CaptivePortal))
            sb.AppendLine($"WEB https FAIL:{http.Https?.Error}" + (http.CaptivePortal ? " CAPTIVE" : ""));
        if (r.PublicIp is { } ip)
            sb.AppendLine($"PUB {ip.Ip} {AsnOnly(ip.Isp)}");
        if (r.ProxyVpn is { } pv && (pv.VpnActive || pv.ProxyActive))
            sb.AppendLine($"VPN {(pv.VpnActive ? ShortVpnName(pv.VpnName) + (pv.VpnAllTraffic ? "*" : "") : "-")} PROXY {(pv.ProxyActive ? "on" : "-")}");
        if (r.Clock?.Skew is { } skew && Math.Abs(skew.TotalSeconds) >= 60)
            sb.AppendLine(Inv($"CLK {skew.TotalMinutes:+0;-0}min"));
        if (r.Trace is { ReachedTarget: false } tr)
            sb.AppendLine($"TRACE stop {tr.LastResponding?.Hop}:{tr.LastResponding?.Address}");
        foreach (var c in r.CustomTargets.Where(c => !c.Reachable))
            sb.AppendLine($"FAIL {c.Target.DisplayName} {c.Target.Host}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>Laporan teks lengkap sesuai spek bagian 9.</summary>
    public static string FullText(NetworkReport r, DiagnosisResult dx, string companyName, Language lang = Language.Id)
    {
        string T(string key, params object?[] args) => Texts.Get(lang, key, args);
        string Label(string key) => $"{T(key),-9}: ";

        var sb = new StringBuilder();
        sb.AppendLine($"=== OneCheck by {companyName} ===");
        sb.AppendLine(Label("rep.check_id") + r.CheckId);
        sb.AppendLine(Label("rep.time") + $"{FormatTimestamp(r.StartedAt)} ({T("rep.duration", Inv($"{r.Duration.TotalSeconds:0}"))})");
        if (r.System is { } s)
            sb.AppendLine(Label("rep.computer") + $"{s.ComputerName} ({s.OsName}), user {s.UserName}, uptime {FormatUptime(s.Uptime, lang)}");
        sb.AppendLine(Label("rep.status") + OverallLabel(dx.Overall, lang).ToUpperInvariant());
        sb.AppendLine(Label("rep.result") + $"[{dx.Primary.Code}] {dx.Primary.Message}");
        foreach (var f in dx.All.Where(f => f != dx.Primary))
            sb.AppendLine($"           [{f.Code}] {f.Message}");
        sb.AppendLine();

        var a = r.PrimaryAdapter;
        if (a is null)
            sb.AppendLine($"[Adapter] {T("rep.no_adapter")}");
        else
        {
            sb.AppendLine($"[Adapter] {a.Name} ({a.Description}) {(a.IsUp ? "UP" : "DOWN")}, MAC {a.MacAddress}"
                          + (a.SpeedMbps > 0 ? $", link {FormatSpeed(a.SpeedMbps)}" : ""));
            if (r.Wifi is { } w)
            {
                sb.AppendLine($"[WiFi] SSID {w.Ssid}" + (w.Bssid is null ? "" : $", BSSID {w.Bssid}") + $", {w.Security}");
                sb.AppendLine($"  {T("rep.signal")} {FormatDbm(w)} ({w.SignalQualityPercent}%), {w.PhyType}"
                              + (w.Band is null ? "" : $", {w.Band}") + (w.Channel is null ? "" : $" ch {w.Channel}"));
                sb.AppendLine(Inv($"  Link rate RX {w.RxRateMbps:0} Mbps / TX {w.TxRateMbps:0} Mbps ") + T("rep.from_windows"));
            }
            sb.AppendLine($"  IPv4 {a.IPv4 ?? "-"}/{a.PrefixLength} "
                          + (a.DhcpEnabled ? T("rep.dhcp_from", a.DhcpServer ?? "?") : $"({T("val.manual")})")
                          + (a.IsApipa ? " APIPA!" : ""));
            if (a.OtherIPv4.Count > 0)
                sb.AppendLine($"  {T("rep.other_ips")}: {string.Join(", ", a.OtherIPv4)}");
            sb.AppendLine($"  Gateway {a.Gateway ?? "-"}{(a.GatewayOutsideSubnet ? " " + T("val.gw_outside") : "")}"
                          + $" | DNS {(a.DnsServers.Count == 0 ? "-" : string.Join(", ", a.DnsServers))}");
        }

        if (r.PingGateway is { } g)
            sb.AppendLine(PingLine(g, lang));
        foreach (var p in r.PingPublic)
            sb.AppendLine(PingLine(p, lang));
        if (r.PingDomain is { } d)
            sb.AppendLine(PingLine(d, lang));
        if (r.DnsSystem is { } dns)
            sb.AppendLine($"[DNS ] {dns.Domain} via {dns.Server} : {DnsShort(dns, lang)}"
                          + (r.DnsPublic is { } pub ? $" | via {pub.Server} : {DnsShort(pub, lang)}" : ""));
        if (r.Trace is { } tr)
            sb.AppendLine($"[Trace] {tr.Target} : " + string.Join("  ", tr.Hops.Select(h =>
                h.Address is null ? $"{h.Hop}) *" : $"{h.Hop}) {h.Address} {h.RttMs}ms"))
                + (tr.ReachedTarget ? "" : "  " + T("rep.not_reached")));
        if (r.Http is { } http)
            sb.AppendLine($"[HTTP] {ProbeLine(http.Http, lang)} | {ProbeLine(http.Https, lang)} | captive portal: "
                          + (http.CaptivePortal ? T("rep.yes") + (http.CaptivePortalUrl is null ? "" : $" ({http.CaptivePortalUrl})") : T("rep.no")));
        sb.AppendLine($"[{T("rep.public_ip")}] {(r.PublicIp is { } ip ? PublicIpShort(ip, r.ProxyVpn, lang) : T("rep.cannot_check"))}");
        foreach (var c in r.CustomTargets)
            sb.AppendLine($"[Custom] {c.Target.DisplayName} {c.Target.Host} : {CustomShort(c, lang)}");
        if (r.ProxyVpn is { } pv)
            sb.AppendLine($"[Proxy/VPN] {ProxyVpnShort(pv, lang)} | WPAD: {(pv.AutoDetect ? "on" : "off")}");
        if (r.System is { } sys)
            sb.AppendLine($"[{T("rep.clock")}] {r.StartedAt:HH:mm:ss} {sys.TimeZoneLabel}, {ClockShort(r.Clock, lang)}"
                          + (r.Clock?.Source is { } src ? $" ({T("rep.reference", src)})" : ""));

        if (r.InternalErrors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"[{T("rep.internal")}]");
            foreach (var (key, msg) in r.InternalErrors)
                sb.AppendLine($"  {key}: {msg}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private static string ProbeShort(HttpProbe? p, Language lang)
    {
        if (p is null)
            return "-";
        if (p.Success)
            return $"OK {p.ElapsedMs} ms";
        var reason = p.Error == HttpErrorKind.UnexpectedResponse
            ? Texts.Get(lang, "err.unexpected", p.StatusCode)
            : Texts.Error(lang, p.ErrorMessage);
        return $"{Texts.Get(lang, "val.failed")} ({reason})";
    }

    private static string ProbeLine(HttpProbe? p, Language lang)
        => p is null ? "-" : $"{new Uri(p.Url).Host} : {ProbeShort(p, lang)}";

    private static string PublicIpShort(PublicIpInfo ip, ProxyVpnInfo? pv, Language lang)
    {
        var s = ip.Ip + (ip.Isp is null ? "" : $" · {ip.Isp}")
                + (ip.City is null && ip.Country is null ? "" : $" · {string.Join(", ", new[] { ip.City, ip.Country }.Where(x => x is not null))}");
        if (pv is { VpnActive: true, VpnAllTraffic: true })
            s += " " + Texts.Get(lang, "val.vpn_owned");
        return s;
    }

    private static string ProxyVpnShort(ProxyVpnInfo pv, Language lang)
    {
        var none = Texts.Get(lang, "rep.no");
        var vpn = pv.VpnActive ? $"VPN: {pv.VpnName}{(pv.VpnAllTraffic ? " " + Texts.Get(lang, "val.all_traffic") : "")}" : $"VPN: {none}";
        var proxies = new[] { pv.UserProxy, pv.PacUrl is null ? null : $"PAC {pv.PacUrl}", pv.WinHttpProxy is null ? null : $"WinHTTP {pv.WinHttpProxy}" }
            .Where(x => x is not null).ToList();
        return $"{vpn} | Proxy: {(proxies.Count == 0 ? none : string.Join(", ", proxies))}";
    }

    private static string ClockShort(ClockInfo? c, Language lang)
        => c?.Skew is { } skew
            ? (Math.Abs(skew.TotalSeconds) < 60 ? Texts.Get(lang, "val.clock_ok") : Texts.Get(lang, "val.clock_skew", DiagnosisEngine.FormatSkew(skew, lang)))
            : Texts.Get(lang, "val.clock_unknown");

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";

    /// <summary>"802.11ax (WiFi 6)" → "WiFi 6".</summary>
    private static string ShortPhy(string phy)
    {
        var open = phy.IndexOf('(');
        return open >= 0 && phy.EndsWith(')') ? phy[(open + 1)..^1] : phy;
    }

    /// <summary>"ZeroTier One [e897…] (ZeroTier Virtual Port)" → "ZeroTier One".</summary>
    private static string ShortVpnName(string? name)
    {
        if (name is null)
            return "";
        var cut = name.IndexOfAny(['[', '(']);
        return Truncate((cut > 0 ? name[..cut] : name).Trim(), 30);
    }

    private static string PingCompact(PingStats p)
        => p.AllLost ? "loss 100%" : p.LossPercent > 0 ? Inv($"{FormatMs(p.AvgMs)} (loss {p.LossPercent:0}%)") : FormatMs(p.AvgMs);

    private static string PingTiny(PingStats p)
        => p.AllLost ? "X" : p.LossPercent > 0 ? Inv($"{p.AvgMs:0}ms/{p.LossPercent:0}%") : Inv($"{p.AvgMs:0}ms");

    /// <summary>"AS7713 PT Telekomunikasi Indonesia" → "AS7713".</summary>
    private static string AsnOnly(string? isp)
        => isp is not null && isp.StartsWith("AS", StringComparison.Ordinal) && isp.Contains(' ') ? isp[..isp.IndexOf(' ')] : Truncate(isp ?? "", 20);

    public static string FormatDbm(WifiInfo w)
        => w.RssiDbm is { } d ? (w.RssiIsEstimated ? "≈" : "") + $"{d} dBm" : "- dBm";

    public static string FormatUptime(TimeSpan t, Language lang = Language.Id)
        => t.TotalDays >= 1 ? Texts.Duration(lang, (int)t.TotalDays, "day")
            : t.TotalHours >= 1 ? Texts.Duration(lang, (int)t.TotalHours, "hour")
            : Texts.Duration(lang, (int)t.TotalMinutes, "minute");

    private static string UptimeTiny(TimeSpan t)
        => t.TotalDays >= 1 ? $"{(int)t.TotalDays}d" : t.TotalHours >= 1 ? $"{(int)t.TotalHours}h" : $"{(int)t.TotalMinutes}m";

    private static string FormatSpeed(long mbps) => mbps >= 1000 ? Inv($"{mbps / 1000.0:0.#} Gbps") : $"{mbps} Mbps";

    private static string PingShort(PingStats p, Language lang)
        => p.AllLost
            ? "loss 100%" + (p.Error is null ? "" : $" ({Texts.Error(lang, p.Error)})")
            : Inv($"loss {p.LossPercent:0.#}%, avg {FormatMs(p.AvgMs)}");

    private static string FormatMs(double? ms) => ms < 1 ? "<1 ms" : Inv($"{ms:0} ms");

    private static string PingLine(PingStats p, Language lang)
        => Inv($"[Ping] {p.Label,-12} {p.Target} : {p.Received}/{p.Sent}, loss {p.LossPercent:0.#}%")
           + (p.AllLost ? (p.Error is null ? "" : $" ({Texts.Error(lang, p.Error)})")
                        : Inv($", min/avg/max {p.MinMs}/{p.AvgMs:0}/{p.MaxMs} ms, jitter {p.JitterMs ?? 0:0.#} ms"));

    private static string DnsShort(DnsLookupResult d, Language lang)
        => d.Success ? $"OK {d.ElapsedMs} ms ({d.Addresses[0]})" : $"{Texts.Get(lang, "val.failed")} ({Texts.Error(lang, d.Error)})";

    private static string TraceShort(TraceResult t, Language lang)
    {
        if (t.ReachedTarget)
            return Texts.Get(lang, "trace.reached", t.Hops.Count);
        return t.LastResponding is { } last
            ? Texts.Get(lang, "trace.stopped", last.Hop, last.Address)
            : Texts.Get(lang, "trace.none");
    }

    private static string CustomShort(CustomTargetResult c, Language lang)
    {
        var s = PingShort(c.Ping, lang);
        if (c.TcpOk is { } ok)
            s += $" | TCP {c.Target.TcpPort}: {(ok ? $"OK {c.TcpMs} ms" : Texts.Get(lang, "val.failed"))}";
        if (c.IcmpBlockedButTcpOk)
            s += " " + Texts.Get(lang, "val.ping_blocked");
        return s;
    }
}
