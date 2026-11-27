using System.Runtime.InteropServices;

namespace OneCheck.Core.Native;

/// <summary>P/Invoke WinHTTP untuk membaca setting proxy Windows (user/IE dan sistem/WinHTTP).</summary>
internal static class WinHttp
{
    private const string Dll = "winhttp.dll";
    public const int AccessTypeNamedProxy = 3;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct IeProxyConfig
    {
        [MarshalAs(UnmanagedType.Bool)] public bool AutoDetect;
        public IntPtr AutoConfigUrl;
        public IntPtr Proxy;
        public IntPtr ProxyBypass;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ProxyInfo
    {
        public int AccessType;
        public IntPtr Proxy;
        public IntPtr ProxyBypass;
    }

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinHttpGetIEProxyConfigForCurrentUser(ref IeProxyConfig config);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinHttpGetDefaultProxyConfiguration(ref ProxyInfo info);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr mem);

    /// <summary>Membaca string LPWSTR milik WinHTTP lalu membebaskan memorinya.</summary>
    public static string? TakeString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;
        var s = Marshal.PtrToStringUni(ptr);
        GlobalFree(ptr);
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
