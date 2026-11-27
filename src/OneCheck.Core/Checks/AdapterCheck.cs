using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

/// <summary>Setara ipconfig /all, memakai API .NET (tidak bergantung bahasa Windows).</summary>
public static class AdapterCheck
{
    private static readonly string[] VirtualKeywords =
        ["Hyper-V", "VMware", "VirtualBox", "vEthernet", "Loopback", "Bluetooth", "WAN Miniport", "Npcap"];

    public static (AdapterInfo? Primary, IReadOnlyList<AdapterInfo> All) Run()
    {
        var nics = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .ToList();

        var all = nics.Select(ToInfo).ToList();
        var bestIndex = GetBestInterfaceIndex();

        AdapterInfo? primary = null;
        if (bestIndex is { } idx)
        {
            var nic = nics.FirstOrDefault(n => GetIPv4Index(n) == idx);
            if (nic is not null)
                primary = all[nics.IndexOf(nic)];
        }

        // Fallback: adapter fisik yang aktif dan punya IPv4, prioritas yang punya gateway.
        primary ??= all
            .Where(a => a.IsUp && a.IPv4 is not null)
            .OrderByDescending(a => a.HasGateway)
            .ThenBy(a => IsVirtual(a) ? 1 : 0)
            .ThenBy(a => a.Kind == AdapterKind.Other ? 1 : 0)
            .FirstOrDefault();

        return (primary, all);
    }

    public static bool IsVirtual(AdapterInfo a)
        => VirtualKeywords.Any(k => a.Description.Contains(k, StringComparison.OrdinalIgnoreCase)
                                 || a.Name.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static AdapterInfo ToInfo(NetworkInterface nic)
    {
        var props = nic.GetIPProperties();
        var ipv4 = props.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
        var gateway = props.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));

        bool dhcp = false;
        try { dhcp = props.GetIPv4Properties()?.IsDhcpEnabled ?? false; } catch (NetworkInformationException) { }

        var dhcpServer = props.DhcpServerAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

        var kind = nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => AdapterKind.Wifi,
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => AdapterKind.Ethernet,
            _ => AdapterKind.Other,
        };

        var mac = nic.GetPhysicalAddress().GetAddressBytes();

        return new AdapterInfo(
            Id: nic.Id,
            Name: nic.Name,
            Description: nic.Description,
            Kind: kind,
            IsUp: nic.OperationalStatus == OperationalStatus.Up,
            MacAddress: mac.Length == 0 ? "-" : string.Join(':', mac.Select(b => b.ToString("X2"))),
            IPv4: ipv4?.Address.ToString(),
            PrefixLength: ipv4?.PrefixLength,
            Gateway: gateway?.ToString(),
            DnsServers: props.DnsAddresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList(),
            DhcpEnabled: dhcp,
            DhcpServer: dhcpServer?.ToString(),
            SpeedMbps: nic.Speed > 0 ? nic.Speed / 1_000_000 : 0);
    }

    private static int? GetIPv4Index(NetworkInterface nic)
    {
        try { return nic.GetIPProperties().GetIPv4Properties()?.Index; }
        catch (NetworkInformationException) { return null; }
    }

    /// <summary>Interface yang dipakai Windows untuk mencapai internet (route ke 8.8.8.8).</summary>
    internal static int? GetBestInterfaceIndex()
    {
        try
        {
            var dest = BitConverter.ToUInt32(IPAddress.Parse("8.8.8.8").GetAddressBytes(), 0);
            return GetBestInterface(dest, out var index) == 0 ? (int)index : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("iphlpapi.dll")]
    private static extern int GetBestInterface(uint destAddr, out uint bestIfIndex);
}
