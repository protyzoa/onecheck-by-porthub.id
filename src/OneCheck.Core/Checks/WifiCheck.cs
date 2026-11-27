using System.Runtime.InteropServices;
using System.Text;
using OneCheck.Core.Models;
using OneCheck.Core.Native;

namespace OneCheck.Core.Checks;

/// <summary>
/// Membaca status WiFi lewat Native WiFi API: SSID, BSSID, RSSI (dBm), RX/TX link rate (hasil kalkulasi MCS
/// oleh driver/Windows), standar radio, channel, band, keamanan.
/// </summary>
public static class WifiCheck
{
    public static async Task<WifiInfo?> RunAsync(string interfaceId, CancellationToken ct)
    {
        if (!Guid.TryParse(interfaceId, out var guid))
            return null;

        // Sinyal diambil 3x dengan jeda 1 detik lalu dirata-rata (spek 3.3).
        var samples = new List<WifiInfo>();
        for (var i = 0; i < 3; i++)
        {
            if (i > 0)
                await Task.Delay(1000, ct);
            var sample = await Task.Run(() => QueryOnce(guid), ct);
            if (sample is null)
                break;
            samples.Add(sample);
        }
        if (samples.Count == 0)
            return null;

        var last = samples[^1];
        var rssiSamples = samples.Where(s => s.RssiDbm.HasValue).Select(s => s.RssiDbm!.Value).ToList();
        return last with
        {
            RssiDbm = rssiSamples.Count > 0 ? (int)Math.Round(rssiSamples.Average()) : null,
            SignalQualityPercent = (int)Math.Round(samples.Average(s => s.SignalQualityPercent)),
        };
    }

    /// <summary>Perkiraan dBm dari kualitas sinyal Windows (0% = -100 dBm, 100% = -50 dBm).</summary>
    public static int EstimateDbmFromQuality(int qualityPercent)
        => (int)Math.Round(Math.Clamp(qualityPercent, 0, 100) / 2.0 - 100);

    public static string? BandFromFrequencyKhz(uint khz) => khz switch
    {
        >= 2_400_000 and < 2_500_000 => "2.4 GHz",
        >= 5_150_000 and < 5_925_000 => "5 GHz",
        >= 5_925_000 and <= 7_125_000 => "6 GHz",
        _ => null,
    };

    /// <summary>Tebakan band dari nomor channel (dipakai bila frekuensi tidak tersedia).</summary>
    public static string? BandFromChannel(int channel) => channel switch
    {
        >= 1 and <= 14 => "2.4 GHz",
        >= 32 and <= 177 => "5 GHz",
        _ => null,
    };

    private static WifiInfo? QueryOnce(Guid guid)
    {
        if (WlanApi.WlanOpenHandle(2, IntPtr.Zero, out _, out var handle) != 0)
            return null;
        try
        {
            if (WlanApi.WlanQueryInterface(handle, ref guid, WlanApi.OpcodeCurrentConnection, IntPtr.Zero,
                    out var size, out var conn, out _) != 0 || conn == IntPtr.Zero)
                return null;

            string ssid;
            byte[] bssid;
            int phy, quality, auth, cipher;
            bool secured;
            double rxMbps, txMbps;
            try
            {
                if (size < 604 || Marshal.ReadInt32(conn, WlanApi.CaState) != WlanApi.InterfaceStateConnected)
                    return null;

                var ssidLen = Math.Clamp(Marshal.ReadInt32(conn, WlanApi.CaSsidLength), 0, 32);
                var ssidBytes = new byte[ssidLen];
                Marshal.Copy(conn + WlanApi.CaSsid, ssidBytes, 0, ssidLen);
                ssid = Encoding.UTF8.GetString(ssidBytes);

                bssid = new byte[6];
                Marshal.Copy(conn + WlanApi.CaBssid, bssid, 0, 6);

                phy = Marshal.ReadInt32(conn, WlanApi.CaPhyType);
                quality = Marshal.ReadInt32(conn, WlanApi.CaSignalQuality);
                // ulRxRate/ulTxRate dalam kbps
                rxMbps = (uint)Marshal.ReadInt32(conn, WlanApi.CaRxRate) / 1000.0;
                txMbps = (uint)Marshal.ReadInt32(conn, WlanApi.CaTxRate) / 1000.0;
                secured = Marshal.ReadInt32(conn, WlanApi.CaSecurityEnabled) != 0;
                auth = Marshal.ReadInt32(conn, WlanApi.CaAuthAlgorithm);
                cipher = Marshal.ReadInt32(conn, WlanApi.CaCipherAlgorithm);
            }
            finally
            {
                WlanApi.WlanFreeMemory(conn);
            }

            int? channel = QueryInt(handle, guid, WlanApi.OpcodeChannelNumber);
            int? rssi = QueryInt(handle, guid, WlanApi.OpcodeRssi);
            if (rssi is >= 0 or < -120)
                rssi = null;

            var (bssRssi, freqKhz) = QueryBss(handle, guid, bssid);
            var estimated = false;
            if (rssi is null && bssRssi is not null)
                rssi = bssRssi;
            if (rssi is null)
            {
                rssi = EstimateDbmFromQuality(quality);
                estimated = true;
            }

            var band = freqKhz is { } f ? BandFromFrequencyKhz(f) : null;
            band ??= channel is { } ch ? BandFromChannel(ch) : null;

            var security = secured ? $"{WlanApi.AuthName(auth)}-{WlanApi.CipherName(cipher)}" : "Open";

            return new WifiInfo(
                Ssid: ssid,
                Bssid: string.Join(':', bssid.Select(b => b.ToString("x2"))),
                RssiDbm: rssi,
                RssiIsEstimated: estimated,
                SignalQualityPercent: quality,
                RxRateMbps: rxMbps,
                TxRateMbps: txMbps,
                PhyType: WlanApi.PhyTypeName(phy),
                Channel: channel,
                Band: band,
                Security: security);
        }
        finally
        {
            WlanApi.WlanCloseHandle(handle, IntPtr.Zero);
        }
    }

    private static int? QueryInt(IntPtr handle, Guid guid, uint opcode)
    {
        if (WlanApi.WlanQueryInterface(handle, ref guid, opcode, IntPtr.Zero, out var size, out var data, out _) != 0
            || data == IntPtr.Zero)
            return null;
        try
        {
            return size >= 4 ? Marshal.ReadInt32(data) : null;
        }
        finally
        {
            WlanApi.WlanFreeMemory(data);
        }
    }

    /// <summary>Mencari entri BSS yang BSSID-nya sama dengan AP yang tersambung untuk RSSI & frekuensi.</summary>
    private static (int? Rssi, uint? FrequencyKhz) QueryBss(IntPtr handle, Guid guid, byte[] bssid)
    {
        try
        {
            if (WlanApi.WlanGetNetworkBssList(handle, ref guid, IntPtr.Zero, 1, false, IntPtr.Zero, out var list) != 0
                || list == IntPtr.Zero)
                return (null, null);
            try
            {
                var total = (uint)Marshal.ReadInt32(list, 0);
                var count = Marshal.ReadInt32(list, 4);
                var entryBssid = new byte[6];
                for (var i = 0; i < count; i++)
                {
                    var offset = WlanApi.BssListHeader + i * WlanApi.BssEntrySize;
                    if (offset + WlanApi.BssEntrySize > total)
                        break;
                    var entry = list + offset;
                    Marshal.Copy(entry + WlanApi.BeBssid, entryBssid, 0, 6);
                    if (!entryBssid.AsSpan().SequenceEqual(bssid))
                        continue;
                    var rssi = Marshal.ReadInt32(entry, WlanApi.BeRssi);
                    var freq = (uint)Marshal.ReadInt32(entry, WlanApi.BeCenterFrequencyKhz);
                    return (rssi is < 0 and > -120 ? rssi : null, freq > 0 ? freq : null);
                }
                return (null, null);
            }
            finally
            {
                WlanApi.WlanFreeMemory(list);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return (null, null);
        }
    }
}
