using Microsoft.Win32;
using OneCheck.Core.Models;

namespace OneCheck.Core.Checks;

public static class SystemInfoCheck
{
    public static SystemInfo Run() => new(
        ComputerName: Environment.MachineName,
        UserName: Environment.UserName,
        OsName: GetOsName(),
        Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64),
        TimeZoneLabel: TimeZoneLabel(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)));

    public static string TimeZoneLabel(TimeSpan offset) => offset.TotalHours switch
    {
        7 => "WIB",
        8 => "WITA",
        9 => "WIT",
        _ => "UTC" + (offset < TimeSpan.Zero ? "-" : "+") + offset.ToString(@"hh\:mm"),
    };

    private static string GetOsName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = key?.GetValue("ProductName") as string ?? "Windows";
            var display = key?.GetValue("DisplayVersion") as string ?? "";
            // ProductName masih "Windows 10" di Windows 11, koreksi berdasarkan nomor build.
            if (Environment.OSVersion.Version.Build >= 22000)
                product = product.Replace("Windows 10", "Windows 11", StringComparison.Ordinal);
            return $"{product} {display}".Trim();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return Environment.OSVersion.VersionString;
        }
    }
}
