# OneCheck by PortHub

Aplikasi desktop Windows untuk cek jaringan dengan satu klik, untuk pengguna awam. Spesifikasi lengkap ada di [SPEC.md](SPEC.md).

Status: **v1.0.1** — M1 & M2 selesai, M3 sebagian (ikon, branding, skrip rilis siap; menunggu sertifikat code signing dan uji multi-PC).

## Download

**[⬇ Download OneCheck.exe (versi terbaru)](https://github.com/protyzoa/onecheck-by-porthub.id/releases/latest/download/OneCheck.exe)** — satu file portable (±62 MB), tanpa install, tanpa admin. Windows 10 (1809+) / 11, 64-bit.

- Paket zip (exe + contoh `onecheck.sample.json` untuk teknisi): [OneCheck-1.0.1.zip](https://github.com/protyzoa/onecheck-by-porthub.id/releases/download/v1.0.1/OneCheck-1.0.1.zip)
- Semua versi & catatan rilis: [halaman Releases](https://github.com/protyzoa/onecheck-by-porthub.id/releases)

> ⚠️ Exe belum ditandatangani (code signing), jadi Windows SmartScreen bisa menampilkan *"Windows protected your PC"*. Klik **More info → Run anyway** untuk menjalankan.

## Struktur

| Folder | Isi |
|---|---|
| `src/OneCheck.Core` | Semua pemeriksaan (adapter, WiFi, ping, traceroute, DNS, target custom), mesin diagnosis, format laporan |
| `src/OneCheck.App` | UI WPF: tombol status besar + layar hasil |
| `tests/OneCheck.Tests` | Unit test mesin diagnosis & helper, plus smoke test jaringan sungguhan |
| `onecheck.sample.json` | Contoh konfigurasi (ganti nama jadi `onecheck.json` di samping `OneCheck.exe`) |

## Kebutuhan build

- Windows 10/11
- .NET 10 SDK

## Perintah

Build & jalankan (debug):

```powershell
dotnet run --project src/OneCheck.App
```

Unit test:

```powershell
dotnet test
```

Smoke test dengan jaringan sungguhan (mencetak laporan lengkap):

```powershell
$env:ONECHECK_LIVE = "1"; dotnet test --filter LiveSmokeTest --logger "console;verbosity=detailed"
```

Buat paket rilis (`dist\OneCheck-<versi>\OneCheck.exe` ±62 MB portable + `.zip`):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\release.ps1
```

Dengan code signing (butuh Windows SDK Signing Tools): tambahkan `-PfxPath C:\cert\porthub.pfx` (password ditanya) atau `-Thumbprint <sha1>` untuk sertifikat terpasang/token EV.

Ikon dibuat ulang dari logo dengan `scripts\make-icon.ps1` (sumber `image\Asset 9center.png`). Logo lengkap layar awal: `src\OneCheck.App\Assets\logo-full.png`.

Checklist uji di berbagai PC: [TESTING.md](TESTING.md).

## Target custom

- Lewat `onecheck.json` → `customTargets` (maks. 10).
- Lewat shortcut/command line: `OneCheck.exe --target "Server Kasir=10.0.0.10" --target "Portal=app.contoh.id:443"` (`host:port` = sekaligus cek TCP).

- Lewat **Mode Teknisi**: tekan `Ctrl+Shift+T` di aplikasi (opsional PIN). Bisa tambah/ubah/hapus target, tes langsung, atur nomor WhatsApp teknisi & PIN. Disimpan ke `onecheck.json` di samping `OneCheck.exe` (folder harus bisa ditulis; komentar di file lama hilang).

## Bahasa

Indonesia dan English. User memilih lewat **ID | EN** di header (diingat per komputer); bahasa awal dari `language` di `onecheck.json` (`id` / `en` / `auto`). Semua teks ada di `src/OneCheck.Core/Localization/Texts.cs` — tambah/ubah teks di sana, unit test akan gagal jika salah satu bahasa kosong atau placeholder `{0}` tidak cocok.

## Cakupan

- **M1:** info adapter (setara ipconfig), status WiFi (SSID, BSSID, dBm, RX/TX link rate dari Windows, standar, band, channel), ping gateway/publik/domain, traceroute paralel, DNS bawaan vs publik, target custom (ping + TCP), deteksi APIPA, info sistem, Simpan Gambar, Salin Teks, Kirim ke WhatsApp.
- **M2:** cek HTTP/HTTPS + captive portal (tombol "Buka Halaman Login"), IP publik & ISP, deteksi VPN/Proxy, cek jam komputer (header HTTP Date, cadangan NTP), mesin diagnosis lengkap (E01–E09, W01–W08), QR code ringkasan untuk teknisi, Mode Teknisi.
