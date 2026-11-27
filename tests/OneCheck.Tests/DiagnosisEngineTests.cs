using OneCheck.Core.Diagnosis;
using OneCheck.Core.Models;
using static OneCheck.Tests.TestReports;

namespace OneCheck.Tests;

public class DiagnosisEngineTests
{
    private static readonly ThresholdsConfig T = new();

    private static DiagnosisResult Eval(NetworkReport r) => DiagnosisEngine.Evaluate(r, T);

    [Fact]
    public void Healthy_network_is_green()
    {
        var dx = Eval(Healthy());
        Assert.Equal(Severity.Ok, dx.Overall);
        Assert.Equal("OK", dx.Primary.Code);
        Assert.All(dx.Checklist, i => Assert.Equal(ItemStatus.Ok, i.Status));
    }

    [Fact]
    public void No_adapter_is_E01_major()
    {
        var r = Healthy();
        r.PrimaryAdapter = null;
        var dx = Eval(r);
        Assert.Equal(Severity.Major, dx.Overall);
        Assert.Equal("E01", dx.Primary.Code);
        Assert.Single(dx.Checklist);
    }

    [Fact]
    public void Apipa_address_is_E02()
    {
        var r = Healthy(Lan() with { IPv4 = "169.254.10.5", Gateway = null });
        r.PingGateway = null;
        InternetDown(r);
        var dx = Eval(r);
        Assert.Equal("E02", dx.Primary.Code);
        Assert.Equal(Severity.Major, dx.Overall);
    }

    [Fact]
    public void Gateway_and_internet_dead_is_E03()
    {
        var r = Healthy();
        r.PingGateway = Lost("192.168.1.1");
        InternetDown(r);
        Assert.Equal("E03", Eval(r).Primary.Code);
    }

    [Fact]
    public void Gateway_blocking_ping_while_internet_works_is_not_a_problem()
    {
        var r = Healthy();
        r.PingGateway = Lost("192.168.1.1");
        var dx = Eval(r);
        Assert.Equal(Severity.Ok, dx.Overall);
        Assert.Contains(dx.Checklist, i => i.Label == "Router" && i.Status == ItemStatus.Ok);
    }

    [Fact]
    public void Router_ok_but_isp_down_is_E04()
    {
        var r = Healthy();
        InternetDown(r);
        var dx = Eval(r);
        Assert.Equal("E04", dx.Primary.Code);
        Assert.Contains(dx.Checklist, i => i.Label == "Internet" && i.Status == ItemStatus.Fail);
        Assert.Contains(dx.Checklist, i => i.Label == "Buka website" && i.Status == ItemStatus.Fail);
        Assert.Contains(dx.Checklist, i => i.Label == "Jam komputer" && i.Status == ItemStatus.Unknown);
    }

    [Fact]
    public void Icmp_blocked_network_with_working_https_is_green()
    {
        var r = Healthy();
        r.PingPublic = [Lost("8.8.8.8"), Lost("1.1.1.1")];
        r.PingDomain = Lost("google.com");
        r.Trace = new TraceResult("8.8.8.8", [new(1, "192.168.1.1", 2), new(2, null, null)], false);
        var dx = Eval(r);
        Assert.Equal(Severity.Ok, dx.Overall);
        Assert.Contains(dx.Checklist, i => i.Label == "Internet" && i.Status == ItemStatus.Ok);
    }

    [Fact]
    public void Captive_portal_is_W02_and_hides_isp_down()
    {
        var r = Healthy();
        InternetDown(r);
        r.DnsSystem = DnsOk();
        r.Http = new HttpCheckResult(HttpOk() with { Success = false, StatusCode = 302, Error = HttpErrorKind.UnexpectedResponse, RedirectLocation = "http://login.hotel.test/" },
            HttpsFail(HttpErrorKind.Certificate), true, "http://login.hotel.test/");
        var dx = Eval(r);
        Assert.Equal("W02", dx.Primary.Code);
        Assert.Equal(Severity.Minor, dx.Overall);
        Assert.DoesNotContain(dx.All, f => f.Code is "E04" or "E08");
        Assert.Contains(dx.Checklist, i => i.Label == "Buka website (perlu login)" && i.Status == ItemStatus.Warn);
    }

    [Fact]
    public void Proxy_active_and_https_failing_is_W03()
    {
        var r = Healthy();
        r.ProxyVpn = NoProxy() with { UserProxy = "10.9.9.9:8080" };
        r.Http = r.Http! with { Https = HttpsFail(HttpErrorKind.Proxy) };
        var dx = Eval(r);
        Assert.Equal("W03", dx.Primary.Code);
        Assert.Contains("Proxy", dx.Primary.Message);
        Assert.Contains(dx.Checklist, i => i.Label == "Proxy aktif" && i.Status == ItemStatus.Warn);
    }

    [Fact]
    public void Working_vpn_is_not_a_problem()
    {
        var r = Healthy();
        r.ProxyVpn = NoProxy() with { VpnActive = true, VpnName = "WireGuard", VpnAllTraffic = true };
        var dx = Eval(r);
        Assert.Equal(Severity.Ok, dx.Overall);
        Assert.Contains(dx.Checklist, i => i.Label == "VPN aktif" && i.Status == ItemStatus.Ok);
    }

    [Fact]
    public void Clock_skew_is_W04()
    {
        var r = Healthy();
        var now = DateTimeOffset.UtcNow;
        r.Clock = new ClockInfo(now, now.AddMinutes(-20), "NTP");
        var dx = Eval(r);
        Assert.Equal("W04", dx.Primary.Code);
        Assert.Contains("lebih cepat 20 menit", dx.Primary.Message);
    }

    [Fact]
    public void Clock_skew_with_certificate_error_is_E09()
    {
        var r = Healthy();
        var now = DateTimeOffset.UtcNow;
        r.Clock = new ClockInfo(now, now.AddDays(3), "NTP");
        r.Http = r.Http! with { Https = HttpsFail(HttpErrorKind.Certificate) };
        var dx = Eval(r);
        Assert.Equal("E09", dx.Primary.Code);
        Assert.Contains("tertinggal 3 hari", dx.Primary.Message);
        Assert.DoesNotContain(dx.All, f => f.Code is "W04" or "E08");
    }

    [Fact]
    public void Https_blocked_while_ping_and_dns_work_is_E08()
    {
        var r = Healthy();
        r.Http = r.Http! with { Https = HttpsFail(HttpErrorKind.Timeout) };
        var dx = Eval(r);
        Assert.Equal("E08", dx.Primary.Code);
        Assert.Equal(Severity.Major, dx.Overall);
    }

    [Fact]
    public void Dns_broken_but_internet_up_is_E05()
    {
        var r = Healthy();
        r.DnsSystem = DnsFail();
        r.PingDomain = Lost("google.com") with { Error = "nama tidak ditemukan (DNS)" };
        var dx = Eval(r);
        Assert.Equal("E05", dx.Primary.Code);
        Assert.Equal(Severity.Major, dx.Overall);
    }

    [Fact]
    public void Weak_wifi_is_W01_minor()
    {
        var r = Healthy(Wifi());
        r.Wifi = Signal(-74);
        var dx = Eval(r);
        Assert.Equal(Severity.Minor, dx.Overall);
        Assert.Equal("W01", dx.Primary.Code);
        Assert.Contains(dx.Checklist, i => i.Label == "WiFi: Lemah" && i.Status == ItemStatus.Warn);
    }

    [Fact]
    public void Very_weak_wifi_with_gateway_loss_escalates_to_E06()
    {
        var r = Healthy(Wifi());
        r.Wifi = Signal(-84);
        r.PingGateway = Ok("192.168.1.1", 30, received: 6);
        Assert.Equal("E06", Eval(r).Primary.Code);
    }

    [Fact]
    public void Packet_loss_above_warn_is_W05()
    {
        var r = Healthy();
        r.PingPublic = [Ok("8.8.8.8", 20, received: 9), Ok("1.1.1.1", 18, received: 9)];
        var dx = Eval(r);
        Assert.Equal("W05", dx.Primary.Code);
        Assert.Equal(Severity.Minor, dx.Overall);
    }

    [Fact]
    public void High_latency_is_W05()
    {
        var r = Healthy();
        r.PingPublic = [Ok("8.8.8.8", 220), Ok("1.1.1.1", 200)];
        Assert.Equal("W05", Eval(r).Primary.Code);
    }

    [Fact]
    public void Heavy_packet_loss_escalates_to_E07()
    {
        var r = Healthy();
        r.PingPublic = [Ok("8.8.8.8", 20, received: 5), Ok("1.1.1.1", 18, received: 6)];
        Assert.Equal("E07", Eval(r).Primary.Code);
    }

    [Fact]
    public void Strong_signal_low_rate_is_W07()
    {
        var r = Healthy(Wifi());
        r.Wifi = Signal(-52, rate: 6.5);
        Assert.Equal("W07", Eval(r).Primary.Code);
    }

    [Fact]
    public void Critical_custom_target_down_is_W06_with_name()
    {
        var r = Healthy();
        r.CustomTargets = [Custom("Server Kasir", reachable: false, critical: true)];
        var dx = Eval(r);
        Assert.Equal("W06", dx.Primary.Code);
        Assert.Contains("Server Kasir", dx.Primary.Message);
        Assert.Contains(dx.Checklist, i => i.Label == "Server Kasir" && i.Status == ItemStatus.Fail);
    }

    [Fact]
    public void Non_critical_custom_target_down_is_W08()
    {
        var r = Healthy();
        r.CustomTargets = [Custom("Printer", reachable: false)];
        var dx = Eval(r);
        Assert.Equal("W08", dx.Primary.Code);
        Assert.Contains(dx.Checklist, i => i.Label == "Printer" && i.Status == ItemStatus.Warn);
    }

    [Fact]
    public void Custom_target_slower_than_its_limit_is_W08()
    {
        var r = Healthy();
        r.CustomTargets = [Custom("Portal", reachable: true, maxLatency: 50, avg: 120)];
        Assert.Equal("W08", Eval(r).Primary.Code);
    }

    [Fact]
    public void Icmp_blocked_but_tcp_open_counts_as_reachable()
    {
        var r = Healthy();
        r.CustomTargets = [Custom("Portal", reachable: false, critical: true, tcpPort: 443, tcpOk: true)];
        var dx = Eval(r);
        Assert.Equal(Severity.Ok, dx.Overall);
        Assert.True(r.CustomTargets[0].IcmpBlockedButTcpOk);
    }

    [Fact]
    public void Major_beats_minor_for_button_color_and_primary()
    {
        var r = Healthy(Wifi());
        r.Wifi = Signal(-75);                 // W01 (minor), diperiksa lebih dulu
        r.DnsSystem = DnsFail();              // E05 (major)
        var dx = Eval(r);
        Assert.Equal(Severity.Major, dx.Overall);
        Assert.Equal("E05", dx.Primary.Code);
        Assert.Contains(dx.All, f => f.Code == "W01");
    }

    [Theory]
    [InlineData(-45, "Sangat baik", ItemStatus.Ok)]
    [InlineData(-55, "Baik", ItemStatus.Ok)]
    [InlineData(-65, "Cukup", ItemStatus.Ok)]
    [InlineData(-75, "Lemah", ItemStatus.Warn)]
    [InlineData(-85, "Sangat lemah", ItemStatus.Fail)]
    public void Signal_categories_follow_spec(int dbm, string label, ItemStatus status)
    {
        var (l, s) = DiagnosisEngine.SignalCategory(dbm, new WifiSignalThresholds());
        Assert.Equal(label, l);
        Assert.Equal(status, s);
    }
}
