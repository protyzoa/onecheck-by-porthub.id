using System.Globalization;

namespace OneCheck.Core.Localization;

public enum Language { Id, En }

/// <summary>
/// Semua teks yang dilihat pengguna, dalam Bahasa Indonesia dan English. Placeholder {0}, {1}, ... harus sama
/// di kedua bahasa (dicek oleh unit test). Kunci yang tidak ada dikembalikan apa adanya.
/// </summary>
public static class Texts
{
    public static string Get(Language lang, string key, params object?[] args)
    {
        if (!Table.TryGetValue(key, out var pair))
            return key;
        var template = lang == Language.En ? pair.En : pair.Id;
        return args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
    }

    public static bool Has(string key) => Table.ContainsKey(key);

    /// <summary>Teks error dari pemeriksaan: kunci "err.*" diterjemahkan, teks lain (pesan sistem) apa adanya.</summary>
    public static string? Error(Language lang, string? error)
        => error is not null && error.StartsWith("err.", StringComparison.Ordinal) && Has(error) ? Get(lang, error) : error;

    /// <summary>"id" / "en" / "auto" (ikut bahasa Windows). Nilai lain → Indonesia.</summary>
    public static Language Parse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "en" or "english" => Language.En,
        "auto" => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "id" ? Language.Id : Language.En,
        _ => Language.Id,
    };

    public static string Code(Language lang) => lang == Language.En ? "en" : "id";

    /// <summary>Jumlah + satuan waktu, mis. "3 hari" / "3 days" / "1 day".</summary>
    public static string Duration(Language lang, int n, string unit)
        => Get(lang, $"unit.{unit}{(n == 1 ? "1" : "N")}", n);

    internal static IReadOnlyDictionary<string, (string Id, string En)> All => Table;

    private static readonly Dictionary<string, (string Id, string En)> Table = new()
    {
        // ---------- Tombol status ----------
        // Sengaja selalu English di kedua bahasa (keputusan produk).
        ["btn.start"] = ("Start Test", "Start Test"),
        ["overall.ok"] = ("Network Runs Perfectly", "Network Runs Perfectly"),
        ["overall.minor"] = ("Minor Network Problem", "Minor Network Problem"),
        ["overall.major"] = ("Major Network Problem", "Major Network Problem"),

        // ---------- Diagnosis ----------
        ["dx.ok.msg"] = ("Koneksi internet normal.", "Internet connection is normal."),
        ["dx.E01.msg"] = ("Komputer tidak tersambung ke jaringan.", "This computer is not connected to any network."),
        ["dx.E01.adv"] = ("Periksa kabel LAN atau nyalakan WiFi, lalu tes ulang.", "Check the LAN cable or turn on WiFi, then test again."),
        ["dx.E02.msg"] = ("Komputer tidak mendapat alamat dari router.", "This computer did not get an address from the router."),
        ["dx.E02.adv"] = ("Matikan router 30 detik lalu nyalakan lagi. Jika memakai kabel, cabut lalu pasang kembali.",
            "Turn the router off for 30 seconds, then on again. If you use a cable, unplug it and plug it back in."),
        ["dx.E03.msg"] = ("Komputer tidak bisa berkomunikasi dengan router.", "This computer cannot communicate with the router."),
        ["dx.E03.adv"] = ("Matikan router 30 detik lalu nyalakan lagi, lalu tes ulang.", "Turn the router off for 30 seconds, then on again, and test again."),
        ["dx.E04.msg"] = ("Router tersambung, tapi internet dari penyedia (ISP) tidak tersedia.",
            "The router is connected, but internet from the provider (ISP) is not available."),
        ["dx.E04.adv"] = ("Matikan router 30 detik lalu nyalakan lagi. Jika masih gagal, kemungkinan gangguan dari ISP.",
            "Turn the router off for 30 seconds, then on again. If it still fails, the ISP probably has an outage."),
        ["dx.E05.msg"] = ("Internet ada, tapi pencarian nama situs (DNS) bermasalah.", "Internet is available, but website name lookup (DNS) is failing."),
        ["dx.E05.adv_local"] = ("Server DNS di router/komputer tidak menjawab. Matikan-nyalakan router atau hubungi teknisi.",
            "The DNS server on the router/computer is not answering. Restart the router or contact a technician."),
        ["dx.E05.adv_all"] = ("Semua server DNS tidak menjawab, kemungkinan diblokir. Hubungi teknisi.",
            "No DNS server is answering; DNS may be blocked. Contact a technician."),
        ["dx.E06.msg"] = ("Sinyal WiFi sangat lemah ({0} dBm) sehingga koneksi ke router sering putus.",
            "WiFi signal is very weak ({0} dBm), so the connection to the router keeps dropping."),
        ["dx.E06.adv"] = ("Pindah lebih dekat ke router atau gunakan kabel LAN.", "Move closer to the router or use a LAN cable."),
        ["dx.E07.msg"] = ("Internet sangat tidak stabil (paket hilang {0}%).", "Internet is very unstable ({0}% packet loss)."),
        ["dx.E07.adv"] = ("Matikan-nyalakan router. Jika berlanjut, hubungi teknisi.", "Restart the router. If it continues, contact a technician."),
        ["dx.E08.msg"] = ("Internet tersambung, tapi website tidak bisa dibuka.", "Internet is connected, but websites cannot be opened."),
        ["dx.E08.msg_cert"] = ("Internet tersambung, tapi sertifikat keamanan website ditolak.",
            "Internet is connected, but website security certificates are being rejected."),
        ["dx.E08.adv"] = ("Kemungkinan diblokir firewall atau antivirus. Hubungi teknisi.", "Probably blocked by a firewall or antivirus. Contact a technician."),
        ["dx.E08.adv_cert"] = ("Kemungkinan antivirus/firewall memeriksa koneksi HTTPS. Hubungi teknisi.",
            "An antivirus/firewall is probably inspecting HTTPS connections. Contact a technician."),
        ["dx.E09.msg"] = ("Jam komputer salah ({0}) sehingga website aman (HTTPS) tidak bisa dibuka.",
            "The computer clock is wrong ({0}), so secure websites (HTTPS) cannot be opened."),
        ["dx.E09.adv"] = ("Perbaiki tanggal & jam: klik kanan jam di taskbar → Adjust date and time → Sync now.",
            "Fix the date & time: right-click the clock on the taskbar → Adjust date and time → Sync now."),
        ["dx.W01.msg"] = ("Sinyal WiFi lemah ({0} dBm).", "WiFi signal is weak ({0} dBm)."),
        ["dx.W01.adv"] = ("Coba pindah lebih dekat ke router.", "Try moving closer to the router."),
        ["dx.W02.msg"] = ("Jaringan ini meminta login sebelum bisa memakai internet.", "This network asks you to log in before you can use the internet."),
        ["dx.W02.adv"] = ("Klik tombol \"{0}\" lalu login WiFi, kemudian tes ulang.", "Click the \"{0}\" button, log in to the WiFi, then test again."),
        ["dx.W03.msg"] = ("Ada {0} aktif yang mungkin mengganggu koneksi.", "An active {0} may be interfering with the connection."),
        ["dx.W03.adv_vpn"] = ("Matikan aplikasi VPN lalu tes ulang.", "Turn off the VPN app, then test again."),
        ["dx.W03.adv_proxy"] = ("Matikan proxy: Settings → Network & internet → Proxy, lalu tes ulang.",
            "Turn off the proxy: Settings → Network & internet → Proxy, then test again."),
        ["dx.W04.msg"] = ("Jam komputer tidak sesuai ({0}). Ini bisa membuat situs tidak bisa dibuka.",
            "The computer clock is off ({0}). This can stop websites from opening."),
        ["dx.W04.adv"] = ("Klik kanan jam di taskbar → Adjust date and time → Sync now.", "Right-click the clock on the taskbar → Adjust date and time → Sync now."),
        ["dx.W05.msg_loss"] = ("Internet tersambung tapi tidak stabil (paket hilang {0}%).", "Internet is connected but unstable ({0}% packet loss)."),
        ["dx.W05.msg_slow"] = ("Internet tersambung tapi lambat (respons {0} ms).", "Internet is connected but slow ({0} ms response)."),
        ["dx.W05.adv"] = ("Kurangi pemakaian berat (download/streaming) atau matikan-nyalakan router.",
            "Reduce heavy use (downloads/streaming) or restart the router."),
        ["dx.W06.msg"] = ("Internet normal, tapi {0} tidak bisa dihubungi.", "Internet is normal, but {0} cannot be reached."),
        ["dx.W06.adv"] = ("Beri tahu teknisi bahwa server/perangkat ini tidak merespons.", "Tell your technician that this server/device is not responding."),
        ["dx.W07.msg"] = ("Sinyal WiFi kuat tapi kecepatan rendah ({0} Mbps).", "WiFi signal is strong but the speed is low ({0} Mbps)."),
        ["dx.W07.adv"] = ("Kemungkinan ada gangguan atau terlalu banyak pengguna. Coba matikan-nyalakan WiFi.",
            "There may be interference or too many users. Try turning WiFi off and on."),
        ["dx.W08.down"] = ("Internet normal, tapi {0} tidak merespons.", "Internet is normal, but {0} is not responding."),
        ["dx.W08.slow"] = ("{0} lambat ({1} ms).", "{0} is slow ({1} ms)."),

        ["skew.fast"] = ("lebih cepat {0}", "{0} fast"),
        ["skew.slow"] = ("tertinggal {0}", "{0} behind"),
        ["unit.day1"] = ("{0} hari", "{0} day"),
        ["unit.dayN"] = ("{0} hari", "{0} days"),
        ["unit.hour1"] = ("{0} jam", "{0} hour"),
        ["unit.hourN"] = ("{0} jam", "{0} hours"),
        ["unit.minute1"] = ("{0} menit", "{0} minute"),
        ["unit.minuteN"] = ("{0} menit", "{0} minutes"),
        ["unit.second1"] = ("{0} detik", "{0} second"),
        ["unit.secondN"] = ("{0} detik", "{0} seconds"),

        ["signal.excellent"] = ("Sangat baik", "Excellent"),
        ["signal.good"] = ("Baik", "Good"),
        ["signal.fair"] = ("Cukup", "Fair"),
        ["signal.weak"] = ("Lemah", "Weak"),
        ["signal.veryweak"] = ("Sangat lemah", "Very weak"),

        // ---------- Checklist ----------
        ["chk.nic"] = ("Kartu jaringan", "Network adapter"),
        ["chk.wifi"] = ("WiFi: {0}", "WiFi: {0}"),
        ["chk.wifi_plain"] = ("WiFi", "WiFi"),
        ["chk.router"] = ("Router", "Router"),
        ["chk.internet"] = ("Internet", "Internet"),
        ["chk.dns"] = ("Nama situs (DNS)", "Website names (DNS)"),
        ["chk.web"] = ("Buka website", "Open websites"),
        ["chk.web_login"] = ("Buka website (perlu login)", "Open websites (login needed)"),
        ["chk.noproxy"] = ("VPN/Proxy: tidak ada", "VPN/Proxy: none"),
        ["chk.vpn"] = ("VPN aktif", "VPN active"),
        ["chk.proxy"] = ("Proxy aktif", "Proxy active"),
        ["chk.clock"] = ("Jam komputer", "Computer clock"),

        // ---------- Ringkasan teknis (layar hasil) ----------
        ["sum.computer"] = ("Komputer", "Computer"),
        ["sum.adapter"] = ("Adapter", "Adapter"),
        ["sum.wifi"] = ("WiFi", "WiFi"),
        ["sum.linkrate"] = ("Link rate", "Link rate"),
        ["sum.ipdns"] = ("IP / DNS", "IP / DNS"),
        ["sum.gateway"] = ("Gateway", "Gateway"),
        ["sum.ping_internet"] = ("Ping internet", "Ping internet"),
        ["sum.ping"] = ("Ping {0}", "Ping {0}"),
        ["sum.dns"] = ("DNS {0}", "DNS {0}"),
        ["sum.trace"] = ("Traceroute", "Traceroute"),
        ["sum.http"] = ("HTTP / HTTPS", "HTTP / HTTPS"),
        ["sum.publicip"] = ("IP Publik/ISP", "Public IP/ISP"),
        ["sum.vpnproxy"] = ("VPN / Proxy", "VPN / Proxy"),
        ["sum.clock"] = ("Jam", "Clock"),

        ["val.failed"] = ("GAGAL", "FAILED"),
        ["val.none_connected"] = ("tidak ada yang tersambung", "none connected"),
        ["val.manual"] = ("manual", "manual"),
        ["val.cannot_check"] = ("- (tidak bisa dicek)", "- (could not check)"),
        ["val.all_traffic"] = ("(semua trafik)", "(all traffic)"),
        ["val.vpn"] = ("VPN {0}", "VPN {0}"),
        ["val.vpn_none"] = ("VPN tidak ada", "No VPN"),
        ["val.proxy"] = ("Proxy {0}", "Proxy {0}"),
        ["val.proxy_none"] = ("Proxy tidak ada", "No proxy"),
        ["val.clock_ok"] = ("jam sesuai", "clock correct"),
        ["val.clock_skew"] = ("jam {0}", "clock {0}"),
        ["val.clock_unknown"] = ("selisih jam tidak dapat dicek", "clock offset could not be checked"),
        ["val.ping_blocked"] = ("(ping diblokir)", "(ping blocked)"),
        ["val.vpn_owned"] = ("(milik VPN, bukan ISP)", "(belongs to the VPN, not the ISP)"),
        ["trace.reached"] = ("sampai tujuan dalam {0} hop", "reached target in {0} hops"),
        ["trace.stopped"] = ("berhenti di hop {0} ({1})", "stopped at hop {0} ({1})"),
        ["trace.none"] = ("tidak ada hop yang merespons", "no hop responded"),

        // ---------- Laporan teks ----------
        ["rep.check_id"] = ("ID Cek", "Check ID"),
        ["rep.time"] = ("Waktu", "Time"),
        ["rep.duration"] = ("durasi {0} detik", "took {0} s"),
        ["rep.computer"] = ("Komputer", "Computer"),
        ["rep.status"] = ("Status", "Status"),
        ["rep.result"] = ("Hasil", "Result"),
        ["rep.no_adapter"] = ("tidak ada adapter yang tersambung", "no adapter connected"),
        ["rep.signal"] = ("Sinyal", "Signal"),
        ["rep.from_windows"] = ("(dari Windows)", "(from Windows)"),
        ["rep.dhcp_from"] = ("(DHCP dari {0})", "(DHCP from {0})"),
        ["rep.not_reached"] = ("(tidak sampai tujuan)", "(target not reached)"),
        ["rep.yes"] = ("YA", "YES"),
        ["rep.no"] = ("tidak", "no"),
        ["rep.public_ip"] = ("IP Publik", "Public IP"),
        ["rep.cannot_check"] = ("tidak dapat dicek", "could not be checked"),
        ["rep.clock"] = ("Jam", "Clock"),
        ["rep.reference"] = ("acuan {0}", "reference {0}"),
        ["rep.internal"] = ("Tidak dapat dicek", "Could not check"),

        // ---------- Error dari pemeriksaan ----------
        ["err.timeout"] = ("timeout", "timeout"),
        ["err.dns_not_found"] = ("nama tidak ditemukan (DNS)", "name not found (DNS)"),
        ["err.host_unreachable"] = ("host tidak terjangkau", "host unreachable"),
        ["err.net_unreachable"] = ("jaringan tidak terjangkau", "network unreachable"),
        ["err.ttl"] = ("TTL habis", "TTL expired"),
        ["err.no_dns_server"] = ("tidak ada server DNS", "no DNS server"),
        ["err.no_answer"] = ("tidak ada jawaban", "no answer"),
        ["err.cert"] = ("sertifikat keamanan ditolak", "security certificate rejected"),
        ["err.proxy"] = ("proxy menolak koneksi", "proxy refused the connection"),
        ["err.conn"] = ("koneksi gagal", "connection failed"),
        ["err.unexpected"] = ("jawaban tidak sesuai (HTTP {0})", "unexpected response (HTTP {0})"),

        // ---------- Langkah pemeriksaan (teks progres) ----------
        ["step.adapter"] = ("Memeriksa kartu jaringan", "Checking network adapter"),
        ["step.wifi"] = ("Memeriksa sinyal WiFi", "Checking WiFi signal"),
        ["step.gateway"] = ("Memeriksa koneksi ke router", "Checking connection to router"),
        ["step.internet"] = ("Memeriksa koneksi ke internet", "Checking internet connection"),
        ["step.dns"] = ("Memeriksa pencarian nama situs (DNS)", "Checking website name lookup (DNS)"),
        ["step.trace"] = ("Memeriksa jalur ke internet", "Checking route to the internet"),
        ["step.http"] = ("Memeriksa akses website", "Checking website access"),
        ["step.publicip"] = ("Memeriksa IP publik & ISP", "Checking public IP & ISP"),
        ["step.proxy"] = ("Memeriksa VPN & proxy", "Checking VPN & proxy"),
        ["step.clock"] = ("Memeriksa jam komputer", "Checking computer clock"),
        ["step.custom"] = ("Memeriksa server/perangkat khusus", "Checking custom servers/devices"),
        ["step.finishing"] = ("Menyusun hasil", "Preparing results"),

        // ---------- Aplikasi ----------
        ["support.default"] = ("Hubungi teknisi dan kirim foto layar ini.", "Contact your technician and send a photo of this screen."),
        ["ui.idle_hint"] = ("Tekan tombol untuk memeriksa koneksi internet Anda.", "Press the button to check your internet connection."),
        ["ui.starting"] = ("Memulai pemeriksaan…", "Starting checks…"),
        ["ui.progress"] = ("{0}… ({1}/{2})", "{0}… ({1}/{2})"),
        ["ui.finishing"] = ("Menyusun hasil…", "Preparing results…"),
        ["ui.retest"] = ("Klik tombol untuk tes ulang", "Click the button to test again"),
        ["ui.cannot_run"] = ("Tes tidak dapat dijalankan: {0}", "The test could not run: {0}"),
        ["ui.check_id"] = ("ID Cek: {0}", "Check ID: {0}"),
        ["ui.also"] = ("Juga ditemukan: ", "Also found: "),
        ["ui.unknown_suffix"] = (" (tidak dapat dicek)", " (could not check)"),
        ["ui.scan"] = ("Scan untuk teknisi", "Scan for technician"),
        ["ui.saved"] = ("Gambar disimpan di Desktop: {0}", "Image saved to Desktop: {0}"),
        ["ui.save_failed"] = ("Gambar tidak bisa disimpan: {0}", "Could not save the image: {0}"),
        ["ui.copied"] = ("Laporan disalin. Tempel (Ctrl+V) di WhatsApp atau email ke teknisi.",
            "Report copied. Paste it (Ctrl+V) into WhatsApp or an email to your technician."),
        ["ui.copy_failed"] = ("Gagal menyalin laporan. Coba lagi.", "Could not copy the report. Please try again."),
        ["ui.wa_failed"] = ("WhatsApp tidak bisa dibuka: {0}", "Could not open WhatsApp: {0}"),
        ["ui.browser_failed"] = ("Browser tidak bisa dibuka: {0}", "Could not open the browser: {0}"),
        ["ui.no_phone"] = ("Nomor teknisi belum diatur (Mode Teknisi: Ctrl+Shift+T).", "Technician number is not set (Technician Mode: Ctrl+Shift+T)."),
        ["ui.settings_saved"] = ("Pengaturan disimpan. Klik tombol untuk menjalankan tes.", "Settings saved. Click the button to run the test."),
        ["ui.pin_wrong"] = ("PIN salah.", "Wrong PIN."),
        ["ui.crash"] = ("Terjadi kesalahan pada OneCheck. Silakan tutup lalu buka kembali aplikasi.\n\nDetail: {0}",
            "Something went wrong in OneCheck. Please close and reopen the app.\n\nDetails: {0}"),
        ["ui.config_warning"] = ("{0} tidak bisa dibaca ({1}). Memakai setting bawaan.", "{0} could not be read ({1}). Using default settings."),
        ["ui.lang_tip"] = ("Ganti bahasa", "Change language"),
        ["action.portal"] = ("Buka Halaman Login", "Open Login Page"),
        ["action.save"] = ("Simpan Gambar", "Save Image"),
        ["action.copy"] = ("Salin Teks", "Copy Text"),
        ["action.send"] = ("Kirim ke Teknisi", "Send to Technician"),

        // ---------- Mode Teknisi ----------
        ["tech.mode"] = ("Mode Teknisi", "Technician Mode"),
        ["tech.title"] = ("Mode Teknisi — OneCheck", "Technician Mode — OneCheck"),
        ["tech.heading"] = ("Target custom (maks. 10)", "Custom targets (max. 10)"),
        ["tech.hint"] = ("Host boleh IP atau nama domain. Port TCP opsional (untuk server yang memblokir ping). Kritis = jika gagal, jadi kesimpulan utama di layar hasil.",
            "Host can be an IP or a domain name. TCP port is optional (for servers that block ping). Critical = if it fails, it becomes the main conclusion on the result screen."),
        ["tech.col.name"] = ("Nama", "Name"),
        ["tech.col.host"] = ("Host / IP", "Host / IP"),
        ["tech.col.port"] = ("Port TCP", "TCP port"),
        ["tech.col.count"] = ("Jml ping", "Pings"),
        ["tech.col.max"] = ("Batas ms", "Limit ms"),
        ["tech.col.critical"] = ("Kritis", "Critical"),
        ["tech.col.result"] = ("Hasil tes", "Test result"),
        ["tech.add"] = ("＋ Tambah Target", "＋ Add Target"),
        ["tech.remove"] = ("Hapus Terpilih", "Remove Selected"),
        ["tech.test"] = ("▶ Tes Target", "▶ Test Targets"),
        ["tech.phone"] = ("WhatsApp teknisi (62…):", "Technician WhatsApp (62…):"),
        ["tech.pin"] = ("PIN Mode Teknisi:", "Technician Mode PIN:"),
        ["tech.pin_tip"] = ("Kosongkan jika tidak perlu PIN", "Leave empty for no PIN"),
        ["tech.cancel"] = ("Batal", "Cancel"),
        ["tech.save"] = ("💾 Simpan", "💾 Save"),
        ["tech.testing"] = ("menguji…", "testing…"),
        ["tech.ping_failed"] = ("ping gagal", "ping failed"),
        ["tech.ping_ok"] = ("ping OK {0} ms, loss {1}%", "ping OK {0} ms, loss {1}%"),
        ["tech.tcp_ok"] = ("TCP OK", "TCP OK"),
        ["tech.tcp_failed"] = ("TCP gagal", "TCP failed"),
        ["tech.save_failed"] = ("Pengaturan dipakai untuk sesi ini, tetapi tidak bisa disimpan ke {0}:\n{1}\n\nPindahkan OneCheck.exe ke folder yang bisa ditulis (mis. Desktop) agar pengaturan tersimpan permanen.",
            "Settings are used for this session, but could not be saved to {0}:\n{1}\n\nMove OneCheck.exe to a writable folder (e.g. Desktop) so settings are saved permanently."),
        ["tech.row_error"] = ("Baris {0}: {1}", "Row {0}: {1}"),
        ["tech.err.host"] = ("Host / IP wajib diisi tanpa spasi.", "Host / IP is required and cannot contain spaces."),
        ["tech.err.port"] = ("Port TCP harus angka 1–65535 atau dikosongkan.", "TCP port must be a number 1–65535 or empty."),
        ["tech.err.count"] = ("Jumlah ping harus angka 1–50 atau dikosongkan.", "Pings must be a number 1–50 or empty."),
        ["tech.err.max"] = ("Batas ms harus angka positif atau dikosongkan.", "Limit ms must be a positive number or empty."),
        ["tech.pin_prompt"] = ("Masukkan PIN:", "Enter PIN:"),
        ["dlg.ok"] = ("OK", "OK"),
    };
}
