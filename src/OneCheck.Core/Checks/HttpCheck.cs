using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>
/// Cek akses web seperti browser (memakai proxy sistem): HTTP untuk deteksi captive portal, HTTPS untuk
/// "internet bisa dipakai". Redirect tidak diikuti agar halaman login WiFi terlihat.
/// </summary>
public static class HttpCheck
{
    public const string UserAgent = "OneCheck/0.2";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static HttpClient CreateClient(bool followRedirects)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = followRedirects,
            UseProxy = true,
            ConnectTimeout = Timeout,
            PooledConnectionLifetime = TimeSpan.FromSeconds(30),
            AutomaticDecompression = DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = Timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return client;
    }

    public static async Task<HttpCheckResult> RunAsync(TargetsConfig targets, CancellationToken ct)
    {
        using var client = CreateClient(followRedirects: false);

        var httpTask = ProbeAsync(client, targets.HttpCheckUrl,
            (status, body) => status == 200 && body.Trim() == targets.HttpCheckExpectedBody, ct);

        HttpProbe? https = null;
        foreach (var url in targets.HttpsCheckUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
        {
            https = await ProbeAsync(client, url, (status, _) => status == 204, ct);
            if (https.Success)
                break;
        }

        var http = await httpTask;
        var (captive, portalUrl) = DetectCaptivePortal(http);
        return new HttpCheckResult(http, https, captive, portalUrl);
    }

    /// <summary>
    /// Captive portal = probe HTTP terjawab (koneksi berhasil) tetapi dengan redirect atau isi yang lain.
    /// </summary>
    internal static (bool Captive, string? Url) DetectCaptivePortal(HttpProbe http)
    {
        if (http.Success || http.StatusCode is null)
            return (false, null);
        if (http.StatusCode is >= 300 and < 400)
            return (true, http.RedirectLocation);
        if (http.StatusCode == 200 && http.Error == HttpErrorKind.UnexpectedResponse)
            return (true, null);
        return (false, null);
    }

    private static async Task<HttpProbe> ProbeAsync(HttpClient client, string url, Func<int, string, bool> isOk, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
            sw.Stop();
            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(ct);
            if (body.Length > 512)
                body = body[..512];
            var location = response.Headers.Location is { } loc
                ? (loc.IsAbsoluteUri ? loc : new Uri(new Uri(url), loc)).ToString()
                : null;
            var ok = isOk(status, body);
            return new HttpProbe(url, ok, status, sw.ElapsedMilliseconds,
                ok ? HttpErrorKind.None : HttpErrorKind.UnexpectedResponse,
                ok ? null : "err.unexpected",
                location, response.Headers.Date);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            var kind = ex.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => HttpErrorKind.Dns,
                HttpRequestError.SecureConnectionError => HttpErrorKind.Certificate,
                HttpRequestError.ProxyTunnelError => HttpErrorKind.Proxy,
                HttpRequestError.ConnectionError => HttpErrorKind.ConnectionFailed,
                _ => HttpErrorKind.Other,
            };
            return new HttpProbe(url, false, null, sw.ElapsedMilliseconds, kind, Describe(kind, ex), null, null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new HttpProbe(url, false, null, sw.ElapsedMilliseconds, HttpErrorKind.Timeout, "err.timeout", null, null);
        }
    }

    private static string Describe(HttpErrorKind kind, Exception ex) => kind switch
    {
        // Kunci teks (diterjemahkan saat ditampilkan, lihat Texts.Error).
        HttpErrorKind.Dns => "err.dns_not_found",
        HttpErrorKind.Certificate => "err.cert",
        HttpErrorKind.Proxy => "err.proxy",
        HttpErrorKind.ConnectionFailed => "err.conn",
        _ => ex.Message,
    };
}
