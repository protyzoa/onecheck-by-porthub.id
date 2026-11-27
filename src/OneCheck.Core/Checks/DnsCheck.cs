using System.Diagnostics;
using System.Net;
using DnsClient;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>Setara nslookup &lt;domain&gt; [server]: query A record ke server DNS tertentu.</summary>
public static class DnsCheck
{
    public static async Task<DnsLookupResult> QueryAsync(IReadOnlyList<string> servers, string domain, CancellationToken ct)
    {
        var endpoints = servers
            .Select(s => IPAddress.TryParse(s, out var ip) ? new IPEndPoint(ip, 53) : null)
            .OfType<IPEndPoint>()
            .ToArray();
        var label = servers.Count == 0 ? "-" : string.Join(", ", servers);
        if (endpoints.Length == 0)
            return new DnsLookupResult(label, domain, false, [], 0, "err.no_dns_server");

        var client = new LookupClient(new LookupClientOptions(endpoints)
        {
            Timeout = TimeSpan.FromSeconds(2),
            Retries = 1,
            UseCache = false,
            ThrowDnsErrors = false,
            ContinueOnDnsError = true,
            ContinueOnEmptyResponse = true,
        });

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.QueryAsync(domain, QueryType.A, cancellationToken: ct);
            sw.Stop();
            var addresses = response.Answers.ARecords().Select(a => a.Address.ToString()).ToList();
            var server = response.NameServer?.Address ?? label;
            if (response.HasError)
                return new DnsLookupResult(server, domain, false, [], sw.ElapsedMilliseconds, response.ErrorMessage);
            if (addresses.Count == 0)
                return new DnsLookupResult(server, domain, false, [], sw.ElapsedMilliseconds, "err.no_answer");
            return new DnsLookupResult(server, domain, true, addresses, sw.ElapsedMilliseconds, null);
        }
        catch (DnsResponseException ex)
        {
            sw.Stop();
            var msg = ex.Code == DnsResponseCode.ConnectionTimeout ? "err.timeout" : ex.Message;
            return new DnsLookupResult(label, domain, false, [], sw.ElapsedMilliseconds, msg);
        }
    }
}
