using System.Diagnostics;
using System.Net.Sockets;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>Ping ke target buatan teknisi, opsional ditambah TCP connect ke port tertentu.</summary>
public static class CustomTargetCheck
{
    public static async Task<CustomTargetResult> RunAsync(CustomTarget target, int defaultCount, int timeoutMs, CancellationToken ct)
    {
        var pingTask = PingCheck.RunAsync(target.DisplayName, target.Host, target.Count ?? defaultCount, timeoutMs, ct);
        Task<(bool Ok, long Ms)>? tcpTask = target.TcpPort is { } port ? TcpConnectAsync(target.Host, port, 3000, ct) : null;

        var ping = await pingTask;
        if (tcpTask is null)
            return new CustomTargetResult(target, ping, null, null);
        var (ok, ms) = await tcpTask;
        return new CustomTargetResult(target, ping, ok, ok ? ms : null);
    }

    internal static async Task<(bool Ok, long Ms)> TcpConnectAsync(string host, int port, int timeoutMs, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        using var client = new TcpClient();
        var sw = Stopwatch.StartNew();
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            return (true, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return (false, sw.ElapsedMilliseconds);
        }
    }
}
