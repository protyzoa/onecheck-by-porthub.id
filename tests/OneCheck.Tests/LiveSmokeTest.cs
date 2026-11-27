using OneCheck.Core;
using OneCheck.Core.Diagnosis;
using OneCheck.Core.Models;
using OneCheck.Core.Reporting;
using Xunit.Abstractions;

namespace OneCheck.Tests;

/// <summary>
/// Menjalankan pemeriksaan sungguhan di mesin ini. Hanya jalan bila env ONECHECK_LIVE=1, agar unit test
/// biasa tidak bergantung pada jaringan.
/// </summary>
public class LiveSmokeTest(ITestOutputHelper output)
{
    [Fact]
    public async Task Run_real_checks()
    {
        if (Environment.GetEnvironmentVariable("ONECHECK_LIVE") != "1")
            return;

        var config = new OneCheckConfig();
        config.CustomTargets.Add(new CustomTarget { Name = "Cloudflare HTTPS", Host = "1.1.1.1", TcpPort = 443 });

        var report = await new DiagnosticRunner(config).RunAsync(
            new Progress<RunProgress>(p => output.WriteLine($"  progress {p.Completed}/{p.Total} {p.CurrentStep}")));
        var dx = DiagnosisEngine.Evaluate(report, config.Thresholds);

        output.WriteLine(ReportFormatter.FullText(report, dx, "PortHub"));
        Assert.NotNull(report.PrimaryAdapter);
        Assert.True(report.Duration < TimeSpan.FromSeconds(45), $"durasi {report.Duration}");
    }
}
