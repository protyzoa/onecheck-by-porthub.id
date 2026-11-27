using System.Text.RegularExpressions;
using OneCheck.Core.Diagnosis;
using OneCheck.Core.Localization;
using OneCheck.Core.Models;
using OneCheck.Core.Reporting;
using static OneCheck.Tests.TestReports;

namespace OneCheck.Tests;

public class LocalizationTests
{
    private static readonly Regex Placeholder = new(@"\{(\d+)\}");

    // Kata Indonesia yang tidak boleh muncul di teks English (dengan batas kata).
    private static readonly Regex IndonesianWords = new(
        @"\b(tidak|gagal|GAGAL|Komputer|Waktu|Hasil|Sinyal|dari|hari|menit|detik|jam|sesuai|diblokir|berhenti|sampai|tujuan|acuan|durasi|Jaringan|Periksa|Matikan|Coba|teknisi)\b");

    [Fact]
    public void Every_text_has_both_languages_with_matching_placeholders()
    {
        foreach (var (key, (id, en)) in Texts.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(id), $"{key}: ID kosong");
            Assert.False(string.IsNullOrWhiteSpace(en), $"{key}: EN kosong");
            var idPh = Placeholder.Matches(id).Select(m => m.Value).Distinct().Order().ToList();
            var enPh = Placeholder.Matches(en).Select(m => m.Value).Distinct().Order().ToList();
            Assert.True(idPh.SequenceEqual(enPh), $"{key}: placeholder beda ({string.Join(",", idPh)} vs {string.Join(",", enPh)})");
        }
    }

    [Fact]
    public void English_texts_contain_no_indonesian_words()
    {
        foreach (var (key, (_, en)) in Texts.All)
            Assert.False(IndonesianWords.IsMatch(en), $"{key}: \"{en}\"");
    }

    public static TheoryData<string> Scenarios() => new() { "healthy", "down", "captive", "wifi", "clock", "proxy", "custom" };

    private static NetworkReport Build(string scenario)
    {
        var r = Healthy(Wifi());
        r.Wifi = Signal(-56);
        switch (scenario)
        {
            case "down":
                InternetDown(r);
                r.PingGateway = Lost("192.168.1.1");
                r.Trace = new TraceResult("8.8.8.8", [new(1, null, null), new(2, null, null)], false);
                break;
            case "captive":
                InternetDown(r);
                r.Http = new HttpCheckResult(HttpOk() with { Success = false, StatusCode = 302, Error = HttpErrorKind.UnexpectedResponse },
                    HttpsFail(HttpErrorKind.Certificate) with { ErrorMessage = "err.cert" }, true, "http://login.test/");
                break;
            case "wifi":
                r.Wifi = Signal(-84, rate: 6);
                r.PingGateway = Ok("192.168.1.1", 40, received: 6);
                break;
            case "clock":
                var now = DateTimeOffset.UtcNow;
                r.Clock = new ClockInfo(now, now.AddDays(2), "NTP");
                r.Http = r.Http! with { Https = HttpsFail(HttpErrorKind.Certificate) with { ErrorMessage = "err.cert" } };
                break;
            case "proxy":
                r.ProxyVpn = NoProxy() with { UserProxy = "10.0.0.1:8080", VpnActive = true, VpnName = "WireGuard (wg0)", VpnAllTraffic = true };
                r.Http = r.Http! with { Https = HttpsFail(HttpErrorKind.Proxy) with { ErrorMessage = "err.proxy" } };
                break;
            case "custom":
                r.CustomTargets = [Custom("Server", reachable: false, critical: true), Custom("Printer", reachable: false),
                    Custom("Portal", reachable: true, maxLatency: 10, avg: 50), Custom("Web", reachable: false, tcpPort: 443, tcpOk: true)];
                r.PingPublic = [Ok("8.8.8.8", 200, received: 8), Ok("1.1.1.1", 210, received: 9)];
                break;
        }
        return r;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void English_report_has_no_indonesian_words(string scenario)
    {
        var r = Build(scenario);
        var dx = DiagnosisEngine.Evaluate(r, new ThresholdsConfig(), Language.En);
        var texts = new List<string> { ReportFormatter.FullText(r, dx, "PortHub", Language.En), ReportFormatter.OverallLabel(dx.Overall, Language.En) };
        texts.AddRange(ReportFormatter.SummaryLines(r, Language.En).Select(l => $"{l.Key} {l.Value}"));
        texts.AddRange(dx.Checklist.Select(c => c.Label));
        texts.AddRange(dx.All.Select(f => $"{f.Message} {f.Advice}"));

        foreach (var text in texts)
        {
            var match = IndonesianWords.Match(text);
            Assert.False(match.Success, $"[{scenario}] kata \"{match.Value}\" di: {text}");
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Same_diagnosis_codes_in_both_languages(string scenario)
    {
        var r = Build(scenario);
        var id = DiagnosisEngine.Evaluate(r, new ThresholdsConfig(), Language.Id);
        var en = DiagnosisEngine.Evaluate(r, new ThresholdsConfig(), Language.En);
        Assert.Equal(id.Overall, en.Overall);
        Assert.Equal(id.All.Select(f => f.Code), en.All.Select(f => f.Code));
        Assert.Equal(id.Checklist.Select(c => c.Status), en.Checklist.Select(c => c.Status));
    }

    [Theory]
    [InlineData(Language.Id)]
    [InlineData(Language.En)]
    public void Button_labels_are_always_english(Language lang)
    {
        Assert.Equal("Network Runs Perfectly", ReportFormatter.OverallLabel(Severity.Ok, lang));
        Assert.Equal("Minor Network Problem", ReportFormatter.OverallLabel(Severity.Minor, lang));
        Assert.Equal("Major Network Problem", ReportFormatter.OverallLabel(Severity.Major, lang));
        Assert.Equal("Start Test", Texts.Get(lang, "btn.start"));
    }

    [Fact]
    public void English_messages_read_naturally()
    {
        var r = Healthy();
        InternetDown(r);
        var dx = DiagnosisEngine.Evaluate(r, new ThresholdsConfig(), Language.En);
        Assert.Equal("E04", dx.Primary.Code);
        Assert.Equal("The router is connected, but internet from the provider (ISP) is not available.", dx.Primary.Message);

        Assert.Equal("1 day fast", DiagnosisEngine.FormatSkew(TimeSpan.FromHours(25), Language.En));
        Assert.Equal("20 minutes behind", DiagnosisEngine.FormatSkew(TimeSpan.FromMinutes(-20), Language.En));
        Assert.Equal("tertinggal 20 menit", DiagnosisEngine.FormatSkew(TimeSpan.FromMinutes(-20), Language.Id));
    }

    [Fact]
    public void Error_keys_are_translated_and_raw_messages_pass_through()
    {
        Assert.Equal("name not found (DNS)", Texts.Error(Language.En, "err.dns_not_found"));
        Assert.Equal("nama tidak ditemukan (DNS)", Texts.Error(Language.Id, "err.dns_not_found"));
        Assert.Equal("Some OS message", Texts.Error(Language.En, "Some OS message"));
        Assert.Null(Texts.Error(Language.En, null));
    }

    [Theory]
    [InlineData("id", Language.Id)]
    [InlineData("EN", Language.En)]
    [InlineData("english", Language.En)]
    [InlineData(null, Language.Id)]
    [InlineData("xx", Language.Id)]
    public void Language_parsing(string? code, Language expected) => Assert.Equal(expected, Texts.Parse(code));
}
