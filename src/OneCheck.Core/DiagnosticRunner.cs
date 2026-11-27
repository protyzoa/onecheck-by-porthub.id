using System.Security.Cryptography;
using OneCheck.Core.Checks;
using OneCheck.Core.Models;

namespace OneCheck.Core;

/// <summary>CurrentStep berisi kunci teks (mis. "step.dns"); terjemahkan dengan Texts.Get.</summary>
public sealed record RunProgress(int Completed, int Total, string CurrentStep);

/// <summary>
/// Menjalankan seluruh pemeriksaan. Tahap 1 (kartu jaringan) berjalan dulu karena hasilnya (gateway, DNS)
/// dibutuhkan tahap 2; tahap 2 berjalan paralel. Satu pemeriksaan gagal tidak menghentikan yang lain.
/// </summary>
public sealed class DiagnosticRunner(OneCheckConfig config)
{
    private sealed class Step(string name)
    {
        public string Name { get; } = name;
        public bool Done { get; set; }
    }

    public async Task<NetworkReport> RunAsync(IProgress<RunProgress>? progress, CancellationToken ct = default)
    {
        var report = new NetworkReport { CheckId = NewCheckId(), StartedAt = DateTimeOffset.Now };
        var steps = new List<Step>();
        var gate = new object();

        void Report()
        {
            lock (gate)
            {
                var current = steps.FirstOrDefault(s => !s.Done)?.Name ?? "step.finishing";
                progress?.Report(new RunProgress(steps.Count(s => s.Done), steps.Count, current));
            }
        }

        Step AddStep(string name)
        {
            lock (gate)
            {
                var existing = steps.FirstOrDefault(s => s.Name == name);
                if (existing is not null)
                    return existing;
                var step = new Step(name);
                steps.Add(step);
                return step;
            }
        }

        // Daftar langkah (kunci teks Texts "step.*", diterjemahkan oleh UI), urutan berlapis.
        var stAdapter = AddStep("step.adapter");
        var stWifi = AddStep("step.wifi");
        var stGateway = AddStep("step.gateway");
        var stInternet = AddStep("step.internet");
        var stDns = AddStep("step.dns");
        var stTrace = AddStep("step.trace");
        var stHttp = AddStep("step.http");
        var stPublicIp = AddStep("step.publicip");
        var stProxy = AddStep("step.proxy");
        var stClock = AddStep("step.clock");
        var customTargets = config.EffectiveCustomTargets();
        var stCustom = customTargets.Count > 0 ? AddStep("step.custom") : null;
        Report();

        // ---- Tahap 1 ----
        await SafeAsync(report, "system", () => { report.System = SystemInfoCheck.Run(); return Task.CompletedTask; });
        await SafeAsync(report, "adapter", () =>
        {
            var (primary, all) = AdapterCheck.Run();
            report.PrimaryAdapter = primary;
            report.AllAdapters = all;
            return Task.CompletedTask;
        });
        Complete(stAdapter);

        var adapter = report.PrimaryAdapter;
        var pingCount = Math.Clamp(config.PingCount, 1, 50);
        var timeout = Math.Clamp(config.PingTimeoutMs, 200, 5000);
        var dnsDomain = config.Targets.DnsTestDomain;

        // ---- Tahap 2 (paralel) ----
        var tasks = new List<Task>();

        if (adapter is { Kind: AdapterKind.Wifi })
            tasks.Add(Track(stWifi, SafeAsync(report, "wifi", async () => report.Wifi = await WifiCheck.RunAsync(adapter.Id, ct))));
        else
            RemoveStep(stWifi);

        if (adapter?.Gateway is { } gw)
            tasks.Add(Track(stGateway, SafeAsync(report, "ping_gateway", async () =>
                report.PingGateway = await PingCheck.RunAsync("Router", gw, pingCount, timeout, ct))));
        else
            Complete(stGateway);

        var publicPings = config.Targets.PingPublic.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        var publicResults = new PingStats?[publicPings.Count];
        var internetTasks = publicPings.Select((t, i) => SafeAsync(report, $"ping_{t}", async () =>
            publicResults[i] = await PingCheck.RunAsync(t, t, pingCount, timeout, ct))).ToList();
        internetTasks.Add(SafeAsync(report, "ping_domain", async () =>
            report.PingDomain = await PingCheck.RunAsync(dnsDomain, dnsDomain, pingCount, timeout, ct)));
        tasks.Add(Track(stInternet, Task.WhenAll(internetTasks)));

        var systemDns = adapter?.DnsServers is { Count: > 0 } ds
            ? ds
            : report.AllAdapters.Where(a => a.IsUp).SelectMany(a => a.DnsServers).Distinct().ToList();
        tasks.Add(Track(stDns, Task.WhenAll(
            SafeAsync(report, "dns_system", async () => report.DnsSystem = await DnsCheck.QueryAsync(systemDns, dnsDomain, ct)),
            SafeAsync(report, "dns_public", async () =>
                report.DnsPublic = await DnsCheck.QueryAsync([config.Targets.DnsPublicServer], dnsDomain, ct)))));

        tasks.Add(Track(stTrace, SafeAsync(report, "traceroute", async () =>
            report.Trace = await TracerouteCheck.RunAsync(config.Targets.TraceTarget, Math.Clamp(config.TraceMaxHops, 1, 30), timeout, ct))));

        var httpTask = Track(stHttp, SafeAsync(report, "http", async () =>
            report.Http = await HttpCheck.RunAsync(config.Targets, ct)));
        tasks.Add(httpTask);
        // Jam memakai header Date dari cek HTTPS, jadi menunggu cek HTTP selesai.
        tasks.Add(Track(stClock, SafeAsync(report, "clock", async () =>
        {
            await httpTask;
            report.Clock = await ClockCheck.RunAsync(report.Http, config.Targets.NtpServers, ct);
        })));
        tasks.Add(Track(stPublicIp, SafeAsync(report, "public_ip", async () =>
            report.PublicIp = await PublicIpCheck.RunAsync(config.Targets.PublicIpUrls, ct))));
        tasks.Add(Track(stProxy, SafeAsync(report, "proxy_vpn", () =>
            Task.Run(() => report.ProxyVpn = ProxyVpnCheck.Run(config.VpnKeywords), ct))));

        if (stCustom is not null)
        {
            var customResults = new CustomTargetResult?[customTargets.Count];
            tasks.Add(Track(stCustom, Task.WhenAll(customTargets.Select((t, i) => SafeAsync(report, $"custom_{t.DisplayName}", async () =>
                customResults[i] = await CustomTargetCheck.RunAsync(t, pingCount, timeout, ct))))
                .ContinueWith(_ => report.CustomTargets = customResults.OfType<CustomTargetResult>().ToList(),
                    ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)));
        }

        await Task.WhenAll(tasks);
        report.PingPublic = publicResults.OfType<PingStats>().ToList();
        report.FinishedAt = DateTimeOffset.Now;
        progress?.Report(new RunProgress(steps.Count, steps.Count, "step.finishing"));
        return report;

        void Complete(Step s)
        {
            lock (gate) s.Done = true;
            Report();
        }

        void RemoveStep(Step s)
        {
            lock (gate) steps.Remove(s);
            Report();
        }

        async Task Track(Step s, Task task)
        {
            try { await task; }
            finally { Complete(s); }
        }
    }

    private static async Task SafeAsync(NetworkReport report, string key, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (report.InternalErrors)
                report.InternalErrors[key] = ex.Message;
        }
    }

    /// <summary>ID unik untuk referensi tiket, mis. OC-7F3K-92QD (tanpa huruf yang mirip: 0/O, 1/I/L).</summary>
    public static string NewCheckId()
    {
        const string alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return $"OC-{chars[..4]}-{chars[4..]}";
    }
}
