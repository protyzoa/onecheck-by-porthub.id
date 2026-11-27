using System.Text.RegularExpressions;
using OneCheck.Core;
using OneCheck.Core.Checks;
using OneCheck.Core.Diagnosis;
using OneCheck.Core.Models;
using OneCheck.Core.Reporting;
using static OneCheck.Tests.TestReports;

namespace OneCheck.Tests;

public class HelperTests
{
    [Fact]
    public void Config_parses_json_with_comments_and_custom_targets()
    {
        var cfg = OneCheckConfig.Parse("""
            // komentar
            {
              "branding": { "supportPhone": "62812" },
              "customTargets": [ { "name": "Server Kasir", "host": "10.0.0.10", "critical": true, "tcpPort": 443 }, ],
              "thresholds": { "wifiSignalDbm": { "fair": -72 } }
            }
            """);
        Assert.Equal("62812", cfg.Branding.SupportPhone);
        var t = Assert.Single(cfg.CustomTargets);
        Assert.True(t.Critical);
        Assert.Equal(443, t.TcpPort);
        Assert.Equal(-72, cfg.Thresholds.WifiSignalDbm.Fair);
        Assert.Equal(-80, cfg.Thresholds.WifiSignalDbm.Weak); // default tetap
        Assert.Equal(["8.8.8.8", "1.1.1.1"], cfg.Targets.PingPublic);
    }

    [Fact]
    public void Sample_config_file_is_valid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "onecheck.sample.json");
        var cfg = OneCheckConfig.Parse(File.ReadAllText(path));
        Assert.Equal(10, cfg.PingCount);
    }

    [Fact]
    public void Effective_targets_are_capped_at_ten_and_skip_blank_hosts()
    {
        var cfg = new OneCheckConfig();
        cfg.CustomTargets.Add(new CustomTarget { Name = "kosong", Host = " " });
        for (var i = 0; i < 12; i++)
            cfg.CustomTargets.Add(new CustomTarget { Host = $"10.0.0.{i}" });
        var effective = cfg.EffectiveCustomTargets();
        Assert.Equal(10, effective.Count);
        Assert.DoesNotContain(effective, t => t.Name == "kosong");
    }

    [Theory]
    [InlineData("Server Kasir=10.0.0.10", "Server Kasir", "10.0.0.10", null)]
    [InlineData("Portal=app.contoh.id:443", "Portal", "app.contoh.id", 443)]
    [InlineData("192.168.1.50", "", "192.168.1.50", null)]
    public void Command_line_target_parsing(string arg, string name, string host, int? port)
    {
        var cfg = new OneCheckConfig();
        cfg.ApplyCommandLine(["--target", arg]);
        Assert.Empty(cfg.CustomTargets);
        var t = Assert.Single(cfg.EffectiveCustomTargets());
        Assert.Equal(name, t.Name);
        Assert.Equal(host, t.Host);
        Assert.Equal(port, t.TcpPort);
    }

    [Theory]
    [InlineData(100, -50)]
    [InlineData(60, -70)]
    [InlineData(0, -100)]
    public void Dbm_estimate_from_quality(int quality, int dbm)
        => Assert.Equal(dbm, WifiCheck.EstimateDbmFromQuality(quality));

    [Theory]
    [InlineData(2_437_000u, "2.4 GHz")]
    [InlineData(5_180_000u, "5 GHz")]
    [InlineData(5_955_000u, "6 GHz")]
    public void Band_from_frequency(uint khz, string band)
        => Assert.Equal(band, WifiCheck.BandFromFrequencyKhz(khz));

    [Fact]
    public void Ping_summary_computes_loss_and_jitter()
    {
        var stats = PingCheck.Summarize("x", "8.8.8.8", 4, [10, 20, 10], null);
        Assert.Equal(25, stats.LossPercent);
        Assert.Equal(10, stats.MinMs);
        Assert.Equal(20, stats.MaxMs);
        Assert.Equal(13.3, stats.AvgMs);
        Assert.Equal(10, stats.JitterMs);
    }

    [Fact]
    public void Check_id_has_expected_format()
        => Assert.Matches(new Regex("^OC-[2-9A-HJKMNP-Z]{4}-[2-9A-HJKMNP-Z]{4}$"), DiagnosticRunner.NewCheckId());

    [Fact]
    public void Full_text_report_contains_key_facts()
    {
        var r = Healthy(Wifi());
        r.Wifi = Signal(-56) with { RxRateMbps = 1201, TxRateMbps = 864 };
        r.CustomTargets = [Custom("Server Kasir", reachable: true)];
        var dx = DiagnosisEngine.Evaluate(r, new ThresholdsConfig());
        var text = ReportFormatter.FullText(r, dx, "PortHub");

        Assert.Contains("OC-TEST-0001", text);
        Assert.Contains("NETWORK RUNS PERFECTLY", text);
        Assert.Contains("SSID KANTOR-5G", text);
        Assert.Contains("-56 dBm", text);
        Assert.Contains("RX 1201 Mbps / TX 864 Mbps", text);
        Assert.Contains("[Custom] Server Kasir", text);
        Assert.Contains("[Trace] 8.8.8.8", text);
    }

    [Fact]
    public void Config_save_roundtrip_keeps_settings_but_not_command_line_targets()
    {
        var dir = Directory.CreateTempSubdirectory("onecheck-test").FullName;
        try
        {
            var cfg = new OneCheckConfig();
            cfg.CustomTargets.Add(new CustomTarget { Name = "Server", Host = "10.0.0.10", Critical = true });
            cfg.ApplyCommandLine(["--target", "Temp=10.0.0.99"]);
            cfg.TechnicianMode.Pin = "1234";
            cfg.SaveToDirectory(dir);

            var (loaded, warning) = OneCheckConfig.LoadFromDirectory(dir);
            Assert.Null(warning);
            Assert.Equal("1234", loaded.TechnicianMode.Pin);
            var t = Assert.Single(loaded.CustomTargets);
            Assert.Equal("Server", t.Name);
            Assert.True(t.Critical);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Theory]
    [InlineData("""{"ip":"36.80.1.2","city":"Jakarta","country":"ID","org":"AS7713 PT Telekomunikasi Indonesia"}""", "36.80.1.2", "AS7713 PT Telekomunikasi Indonesia")]
    [InlineData("""{"status":"success","query":"110.136.5.6","isp":"PT Telkom","as":"AS7713 PT Telekomunikasi Indonesia","city":"Bandung","country":"Indonesia"}""", "110.136.5.6", "AS7713 PT Telekomunikasi Indonesia")]
    [InlineData("103.10.20.30\n", "103.10.20.30", null)]
    public void Public_ip_parsing(string body, string ip, string? isp)
    {
        var info = PublicIpCheck.Parse(body, "test");
        Assert.NotNull(info);
        Assert.Equal(ip, info.Ip);
        Assert.Equal(isp, info.Isp);
    }

    [Fact]
    public void Public_ip_parsing_rejects_garbage()
        => Assert.Null(PublicIpCheck.Parse("<html>login</html>", "test"));

    [Fact]
    public void Ntp_timestamp_parsing()
    {
        // 3913056000 detik sejak 1900-01-01 = 2024-01-01T00:00:00Z, fraction 0x80000000 = 0.5 detik
        byte[] bytes = [0xE9, 0x3C, 0x7F, 0x00, 0x80, 0x00, 0x00, 0x00];
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, 500, TimeSpan.Zero), ClockCheck.ParseNtpTimestamp(bytes));
    }

    [Theory]
    [InlineData(System.Net.NetworkInformation.NetworkInterfaceType.Unknown, "wg0", "WireGuard Tunnel", true)]
    [InlineData(System.Net.NetworkInformation.NetworkInterfaceType.Ppp, "Kantor VPN", "WAN Miniport (L2TP)", true)]
    [InlineData(System.Net.NetworkInformation.NetworkInterfaceType.Tunnel, "Teredo", "Teredo Tunneling Pseudo-Interface", false)]
    [InlineData(System.Net.NetworkInformation.NetworkInterfaceType.Ethernet, "Ethernet", "Realtek PCIe GbE", false)]
    public void Vpn_detection(System.Net.NetworkInformation.NetworkInterfaceType type, string name, string description, bool expected)
        => Assert.Equal(expected, ProxyVpnCheck.LooksLikeVpn(type, name, description, new OneCheckConfig().VpnKeywords));

    [Fact]
    public void Captive_portal_detection()
    {
        var redirect = new HttpProbe("http://x/", false, 302, 10, HttpErrorKind.UnexpectedResponse, null, "http://login/", null);
        Assert.Equal((true, "http://login/"), HttpCheck.DetectCaptivePortal(redirect));
        var wrongBody = redirect with { StatusCode = 200, RedirectLocation = null };
        Assert.True(HttpCheck.DetectCaptivePortal(wrongBody).Captive);
        var down = redirect with { StatusCode = null, Error = HttpErrorKind.Timeout };
        Assert.False(HttpCheck.DetectCaptivePortal(down).Captive);
    }

    [Fact]
    public void Compact_summary_fits_comfortably_in_a_qr_code()
    {
        var r = Healthy(Wifi());
        r.Wifi = Signal(-56);
        r.CustomTargets = Enumerable.Range(1, 10).Select(i => Custom($"Server Cabang {i}", reachable: i % 2 == 0)).ToList();
        var dx = DiagnosisEngine.Evaluate(r, new ThresholdsConfig());
        var compact = ReportFormatter.CompactSummary(r, dx);
        Assert.Contains("OC-TEST-0001", compact);
        Assert.Contains("SSID KANTOR-5G -56dBm", compact);
        Assert.Contains("FAIL Server Cabang 1 ", compact);
        Assert.DoesNotContain("Server Cabang 2", compact); // target yang OK tidak dimasukkan
        Assert.True(compact.Length < 450, $"panjang {compact.Length}");
    }

    [Fact]
    public void Compact_summary_for_typical_report_is_small()
    {
        var r = Healthy(Wifi());
        r.Wifi = Signal(-56);
        var compact = ReportFormatter.CompactSummary(r, DiagnosisEngine.Evaluate(r, new ThresholdsConfig()));
        Assert.True(compact.Length < 220, $"panjang {compact.Length}:\n{compact}");
    }

    [Fact]
    public void Estimated_dbm_is_marked()
        => Assert.Equal("≈-60 dBm", ReportFormatter.FormatDbm(Signal(-60) with { RssiIsEstimated = true }));
}
