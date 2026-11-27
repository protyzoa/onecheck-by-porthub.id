using System.Runtime.InteropServices;

namespace OneCheck.Core.Native;

/// <summary>
/// P/Invoke minimal ke Native WiFi API (wlanapi.dll). Struktur dibaca per-offset agar tidak bergantung
/// pada layout struct managed. Offset mengikuti definisi wlanapi.h (x64 &amp; x86 sama karena tidak ada pointer).
/// </summary>
internal static class WlanApi
{
    private const string Dll = "wlanapi.dll";

    public const uint OpcodeCurrentConnection = 7;
    public const uint OpcodeChannelNumber = 8;
    public const uint OpcodeRssi = 0x10000102;

    public const int InterfaceStateConnected = 1;

    [DllImport(Dll)]
    public static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr clientHandle);

    [DllImport(Dll)]
    public static extern uint WlanCloseHandle(IntPtr clientHandle, IntPtr reserved);

    [DllImport(Dll)]
    public static extern uint WlanEnumInterfaces(IntPtr clientHandle, IntPtr reserved, out IntPtr interfaceList);

    [DllImport(Dll)]
    public static extern uint WlanQueryInterface(IntPtr clientHandle, ref Guid interfaceGuid, uint opCode, IntPtr reserved,
        out uint dataSize, out IntPtr data, out int opcodeValueType);

    [DllImport(Dll)]
    public static extern uint WlanGetNetworkBssList(IntPtr clientHandle, ref Guid interfaceGuid, IntPtr dot11Ssid,
        int dot11BssType, [MarshalAs(UnmanagedType.Bool)] bool securityEnabled, IntPtr reserved, out IntPtr bssList);

    [DllImport(Dll)]
    public static extern void WlanFreeMemory(IntPtr memory);

    // ---- WLAN_INTERFACE_INFO_LIST ----
    // DWORD dwNumberOfItems; DWORD dwIndex; WLAN_INTERFACE_INFO InterfaceInfo[]
    // WLAN_INTERFACE_INFO = GUID (16) + WCHAR[256] (512) + WLAN_INTERFACE_STATE (4) = 532
    public const int InterfaceInfoSize = 532;
    public const int InterfaceListHeader = 8;

    // ---- WLAN_CONNECTION_ATTRIBUTES (604 byte) ----
    public const int CaState = 0;
    public const int CaSsidLength = 520;
    public const int CaSsid = 524;
    public const int CaBssid = 560;
    public const int CaPhyType = 568;
    public const int CaSignalQuality = 576;
    public const int CaRxRate = 580;
    public const int CaTxRate = 584;
    public const int CaSecurityEnabled = 588;
    public const int CaAuthAlgorithm = 596;
    public const int CaCipherAlgorithm = 600;

    // ---- WLAN_BSS_LIST / WLAN_BSS_ENTRY (360 byte) ----
    public const int BssListHeader = 8;
    public const int BssEntrySize = 360;
    public const int BeBssid = 40;
    public const int BeRssi = 56;
    public const int BeCenterFrequencyKhz = 92;

    public static string PhyTypeName(int phy) => phy switch
    {
        4 => "802.11a",
        5 => "802.11b",
        6 => "802.11g",
        7 => "802.11n (WiFi 4)",
        8 => "802.11ac (WiFi 5)",
        9 => "802.11ad",
        10 => "802.11ax (WiFi 6)",
        11 => "802.11be (WiFi 7)",
        _ => "unknown",
    };

    public static string AuthName(int auth) => auth switch
    {
        1 => "Open",
        2 => "Shared",
        3 => "WPA-Enterprise",
        4 => "WPA-Personal",
        6 => "WPA2-Enterprise",
        7 => "WPA2-Personal",
        8 or 11 => "WPA3-Enterprise",
        9 => "WPA3-Personal",
        10 => "OWE",
        _ => $"auth {auth}",
    };

    public static string CipherName(int cipher) => cipher switch
    {
        0 => "none",
        1 or 5 or 0x100 => "WEP",
        2 => "TKIP",
        4 => "AES",
        8 or 9 => "GCMP",
        _ => $"cipher {cipher}",
    };
}
