using System.Net;
using System.Text.Json;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>IP publik dan nama ISP. Layanan dicoba berurutan sampai ada yang menjawab.</summary>
public static class PublicIpCheck
{
    public static async Task<PublicIpInfo?> RunAsync(IReadOnlyList<string> urls, CancellationToken ct)
    {
        using var client = HttpCheck.CreateClient(followRedirects: true);
        PublicIpInfo? ipOnly = null;
        foreach (var url in urls.Where(u => !string.IsNullOrWhiteSpace(u)))
        {
            try
            {
                var body = await client.GetStringAsync(url, ct);
                var info = Parse(body, new Uri(url).Host);
                if (info is null)
                    continue;
                if (info.Isp is not null)
                    return info;
                ipOnly ??= info; // simpan, tetap coba layanan berikutnya untuk nama ISP
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                // coba layanan berikutnya
            }
        }
        return ipOnly;
    }

    /// <summary>
    /// Mengurai JSON ipinfo.io (ip, org, city, country), ip-api.com (query, isp, as, city, country),
    /// atau teks polos berisi alamat IP (ipify).
    /// </summary>
    internal static PublicIpInfo? Parse(string body, string source)
    {
        body = body.Trim();
        if (IPAddress.TryParse(body, out var plain))
            return new PublicIpInfo(plain.ToString(), null, null, null, source);
        if (!body.StartsWith('{'))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var ip = Str(root, "ip") ?? Str(root, "query");
            if (ip is null || !IPAddress.TryParse(ip, out _))
                return null;
            var isp = Str(root, "org") ?? CombineAsIsp(Str(root, "as"), Str(root, "isp"));
            return new PublicIpInfo(ip, isp, Str(root, "city"), Str(root, "country"), source);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? CombineAsIsp(string? asField, string? isp)
    {
        // ip-api: "as": "AS7713 PT Telekomunikasi Indonesia", "isp": "PT Telkom Indonesia"
        if (asField is not null)
            return asField;
        return isp;
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;
}
