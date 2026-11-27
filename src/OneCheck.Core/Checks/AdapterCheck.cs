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

    /// <summary>
    /// Adapter utama = adapter yang benar-benar dipakai Windows untuk keluar ke internet, dan IP utama = alamat
    /// sumber yang dipilih Windows (bukan sekadar IP pertama). Penting untuk PC dengan banyak interface virtual
    /// atau satu adapter dengan beberapa IP.
    /// </summary>
    public static (AdapterInfo? Primary, IReadOnlyList<AdapterInfo> All) Run(string routeProbeTarget = "8.8.8.8")
    {
        var nics = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .ToList();

        var sourceIp = GetOutboundSourceIp(routeProbeTarget);
        var all = nics.Select(n => ToInfo(n, sourceIp)).ToList();

        // 1) Adapter yang memiliki IP sumber keluar (paling akurat).
        AdapterInfo? primary = sourceIp is null
            ? null
            : all.FirstOrDefault(a => a.IPv4 == sourceIp.ToString());

        // 2) Adapter dengan route terbaik ke internet.
        if (primary is null && GetBestInterfaceIndex() is { } idx)
        {
            var nic = nics.FirstOrDefault(n => GetIPv4Index(n) == idx);
            if (nic is not null)
                primary = all[nics.IndexOf(nic)];
        }

        // 3) Fallback: adapter fisik yang aktif dan punya IPv4, prioritas yang punya gateway.
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

    /// <summary>
    /// IP lokal yang akan dipakai Windows untuk mengirim paket ke <paramref name="target"/>. Memakai socket UDP
    /// yang di-connect (Windows memilih route & alamat sumber) tanpa mengirim paket apa pun.
    /// </summary>
    internal static IPAddress? GetOutboundSourceIp(string target)
    {
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Connect(target, 53);
            return (udp.Client.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch (SocketException)
        {
            return null; // tidak ada route (mis. kabel dicabut)
        }
    }

    /// <summary>
    /// Memilih IP utama dari beberapa IPv4 di satu adapter: IP sumber keluar dari Windows; jika tidak ada,
    /// IP yang satu subnet dengan gateway; jika tidak ada, IP non-APIPA pertama; terakhir IP pertama.
    /// </summary>
    internal static (IPAddress Address, int Prefix)? SelectPrimaryIPv4(
        IReadOnlyList<(IPAddress Address, int Prefix)> candidates, IPAddress? sourceIp, IPAddress? gateway)
    {
        if (candidates.Count == 0)
            return null;
        if (sourceIp is not null && candidates.FirstOrDefault(c => c.Address.Equals(sourceIp)) is { Address: not null } src)
            return src;
        if (gateway is not null && candidates.FirstOrDefault(c => IsInSubnet(c.Address, c.Prefix, gateway)) is { Address: not null } sameNet)
            return sameNet;
        return candidates.FirstOrDefault(c => !c.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal)) is { Address: not null } normal
            ? normal
            : candidates[0];
    }

    public static bool IsInSubnet(IPAddress address, int prefixLength, IPAddress other)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || other.AddressFamily != AddressFamily.InterNetwork
            || prefixLength is < 0 or > 32)
            return false;
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return (ToUInt32(address) & mask) == (ToUInt32(other) & mask);
    }

    private static uint ToUInt32(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }

    private static AdapterInfo ToInfo(NetworkInterface nic, IPAddress? sourceIp)
    {
        var props = nic.GetIPProperties();
        var ipv4List = props.UnicastAddresses
            .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(u => (u.Address, u.PrefixLength))
            .ToList();
        var gateway = props.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
        var ipv4 = SelectPrimaryIPv4(ipv4List, sourceIp, gateway);

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
            PrefixLength: ipv4?.Prefix,
            Gateway: gateway?.ToString(),
            DnsServers: props.DnsAddresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList(),
            DhcpEnabled: dhcp,
            DhcpServer: dhcpServer?.ToString(),
            SpeedMbps: nic.Speed > 0 ? nic.Speed / 1_000_000 : 0)
        {
            OtherIPv4 = ipv4List
                .Where(c => ipv4 is null || !c.Address.Equals(ipv4.Value.Address))
                .Select(c => $"{c.Address}/{c.PrefixLength}")
                .ToList(),
        };
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
