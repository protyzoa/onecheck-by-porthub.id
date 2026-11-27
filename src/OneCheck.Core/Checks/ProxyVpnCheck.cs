using System.Net.NetworkInformation;
using System.Net.Sockets;
using OneCheck.Core.Native;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>Deteksi VPN aktif dan setting proxy Windows (user/IE, PAC, auto-detect, WinHTTP).</summary>
public static class ProxyVpnCheck
{
    // Adapter tunnel bawaan Windows yang bukan VPN.
    private static readonly string[] IgnoredTunnels = ["Teredo", "ISATAP", "6to4", "IP-HTTPS", "Loopback", "Kernel Debug"];

    public static ProxyVpnInfo Run(IReadOnlyList<string> vpnKeywords)
    {
        var (vpnName, allTraffic) = DetectVpn(vpnKeywords);
        var (userProxy, pac, autoDetect) = ReadUserProxy();
        return new ProxyVpnInfo(vpnName is not null, vpnName, allTraffic, userProxy, pac, autoDetect, ReadWinHttpProxy());
    }

    internal static bool LooksLikeVpn(NetworkInterfaceType type, string name, string description, IReadOnlyList<string> keywords)
    {
        if (IgnoredTunnels.Any(k => description.Contains(k, StringComparison.OrdinalIgnoreCase)
                                 || name.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (type is NetworkInterfaceType.Ppp)
            return true;
        return keywords.Any(k => description.Contains(k, StringComparison.OrdinalIgnoreCase)
                              || name.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static (string? Name, bool AllTraffic) DetectVpn(IReadOnlyList<string> keywords)
    {
        var bestIndex = AdapterCheck.GetBestInterfaceIndex();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            if (!LooksLikeVpn(nic.NetworkInterfaceType, nic.Name, nic.Description, keywords))
                continue;
            var props = nic.GetIPProperties();
            if (!props.UnicastAddresses.Any(u => u.Address.AddressFamily == AddressFamily.InterNetwork))
                continue;
            int? index = null;
            try { index = props.GetIPv4Properties()?.Index; } catch (NetworkInformationException) { }
            return ($"{nic.Name} ({nic.Description})", index is not null && index == bestIndex);
        }
        return (null, false);
    }

    private static (string? Proxy, string? Pac, bool AutoDetect) ReadUserProxy()
    {
        try
        {
            var cfg = new WinHttp.IeProxyConfig();
            if (!WinHttp.WinHttpGetIEProxyConfigForCurrentUser(ref cfg))
                return (null, null, false);
            var pac = WinHttp.TakeString(cfg.AutoConfigUrl);
            var proxy = WinHttp.TakeString(cfg.Proxy);
            WinHttp.TakeString(cfg.ProxyBypass);
            return (proxy, pac, cfg.AutoDetect);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return (null, null, false);
        }
    }

    private static string? ReadWinHttpProxy()
    {
        try
        {
            var info = new WinHttp.ProxyInfo();
            if (!WinHttp.WinHttpGetDefaultProxyConfiguration(ref info))
                return null;
            var proxy = WinHttp.TakeString(info.Proxy);
            WinHttp.TakeString(info.ProxyBypass);
            return info.AccessType == WinHttp.AccessTypeNamedProxy ? proxy : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }
}
