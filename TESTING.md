# OneCheck — Checklist Uji Multi-PC (M3)

Tujuan: memastikan OneCheck berjalan benar di berbagai PC sebelum disebar ke pengguna.
Untuk setiap PC/skenario: jalankan `OneCheck.exe`, klik tombol, lalu klik **Salin Teks** dan tempel hasilnya ke kolom "Bukti" (atau simpan gambar). Kode yang diharapkan mengacu ke SPEC.md bagian 4.

## A. Matriks PC

Isi satu baris per PC yang diuji. Target minimal: 6 PC dengan kombinasi berbeda.

| # | Merek/model PC | Windows (versi + bahasa) | Koneksi | Kartu WiFi (merek/model) | User admin? | Hasil | Catatan |
|---|---|---|---|---|---|---|---|
| 1 | | Win 11 · ID | LAN | – | tidak | | |
| 2 | | Win 11 · EN | WiFi | Intel | tidak | | |
| 3 | | Win 10 · ID | WiFi | Realtek | tidak | | |
| 4 | | Win 10 · EN | WiFi | MediaTek / Qualcomm | tidak | | |
| 5 | | Win 11 | WiFi + VPN | | tidak | | |
| 6 | | Win 10/11 | LAN kantor (ping diblokir/proxy) | – | tidak | | |

## B. Cek dasar (setiap PC)

- [ ] Dapat dijalankan dengan **double-click** dari Desktop / folder Download, tanpa install, tanpa admin.
- [ ] Ikon OneCheck tampil di file `.exe`, taskbar, dan judul jendela.
- [ ] Peringatan SmartScreen: catat muncul/tidak (wajar muncul jika exe belum ditandatangani).
- [ ] Antivirus tidak memblokir/menghapus file (catat nama antivirus).
- [ ] Layar awal: logo, tombol abu-abu **Start Test**, pilihan **ID | EN**.
- [ ] Spinner berputar dan teks progres bergerak; seluruh tes selesai **≤ 45 detik**.
- [ ] Hasil muat satu layar pada resolusi **1366×768** dan pada **scaling 125% / 150%**.
- [ ] Ganti **ID ↔ EN** di layar hasil: semua teks berubah, tombol tetap English.
- [ ] **Simpan Gambar** membuat PNG di Desktop dan membuka folder.
- [ ] **Salin Teks** lalu tempel di Notepad/WhatsApp: laporan lengkap terbaca.
- [ ] **QR code** bisa di-scan dengan kamera HP dari **foto layar** (bukan screenshot) dan isinya terbaca.
- [ ] `Ctrl+Shift+T` membuka Mode Teknisi; tambah target, **Tes Target**, **Simpan** → `onecheck.json` terbuat di samping exe.

## C. WiFi (setiap PC WiFi)

Bandingkan dengan output `netsh wlan show interfaces` yang diambil pada saat yang sama.

- [ ] SSID dan BSSID sama.
- [ ] **Receive / Transmit rate** sama dengan netsh.
- [ ] Sinyal dBm masuk akal (tanpa tanda "≈" berarti dari driver; dengan "≈" berarti perkiraan).
- [ ] Band (2.4/5/6 GHz) dan channel sama dengan netsh.

## D. Skenario masalah (cukup di 1–2 PC)

| # | Cara membuat kondisi | Kode yang diharapkan | Warna | OK? |
|---|---|---|---|---|
| 1 | Cabut LAN & matikan WiFi | E01 | Merah | |
| 2 | WiFi jauh dari router (sinyal < −70 dBm) | W01 | Oranye | |
| 3 | Matikan DHCP di router / pakai IP manual tanpa gateway | E02 | Merah | |
| 4 | Cabut kabel WAN/internet di router | E04 | Merah | |
| 5 | Set DNS manual ke IP yang tidak ada (mis. 10.255.255.1) | E05 | Merah | |
| 6 | WiFi publik/hotel yang meminta login | W02 + tombol "Buka Halaman Login" | Oranye | |
| 7 | Set proxy palsu: Settings → Proxy → 10.9.9.9:8080 | W03 | Oranye | |
| 8 | Mundurkan jam Windows 1 hari (matikan "Set time automatically") | E09 atau W04 | Merah/Oranye | |
| 9 | VPN aktif dan berfungsi | VPN terdeteksi, hijau | Hijau | |
| 10 | Target custom kritis ke IP yang mati (Mode Teknisi) | W06 dengan nama target | Oranye | |
| 11 | Jaringan normal | OK | Hijau | |

## E. Branding per klien

- [ ] `onecheck.json` dengan `companyName` lain → header berubah "OneCheck by <nama>".
- [ ] `logoPath` menunjuk PNG klien di folder exe → logo layar awal berganti.
- [ ] `supportPhone` diisi → tombol **Kirim ke Teknisi** aktif dan membuka WhatsApp dengan laporan.
- [ ] `language: "en"` → aplikasi pertama kali terbuka dalam English.

## F. Hasil

| Tanggal | Penguji | Jumlah PC | Lulus | Masalah ditemukan |
|---|---|---|---|---|
| | | | | |
