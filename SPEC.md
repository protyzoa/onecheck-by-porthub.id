# OneCheck by PortHub — Spesifikasi Program

**Versi dokumen:** 0.4 (draft) · **Tanggal:** 26 November 2026

> **Perubahan v0.4:** Dua bahasa (Indonesia / English) dengan pilihan ID | EN di header. Lihat bagian 2.3.
>
> **Perubahan v0.3:** Antarmuka disederhanakan menjadi satu tombol status besar (abu-abu "Start Test" → spinner → hijau "Network Runs Perfectly" / oranye "Minor Network Problem" / merah "Major Network Problem"). Lihat bagian 2.
>
> **Perubahan v0.2:** Status WiFi (SSID, sinyal dBm, link rate dari Windows), cek HTTP/HTTPS + captive portal, IP publik + ISP, deteksi VPN/Proxy, dan cek jam komputer disetujui masuk v1. Ditambahkan fitur **Custom Target Ping**. Cek file hosts dipindah ke backlog.

---

## 1. Ringkasan

OneCheck adalah aplikasi desktop Windows untuk **cek jaringan otomatis dengan satu klik**. Pengguna awam menekan satu tombol besar, aplikasi menjalankan serangkaian pemeriksaan jaringan dasar, lalu menampilkan **satu layar hasil** yang:

1. Mudah dipahami pengguna (bahasa sehari-hari, warna hijau/kuning/merah).
2. Cukup lengkap untuk teknisi (IP, gateway, DNS, latency, hop traceroute, dll).
3. Muat dalam **satu foto/screenshot** sehingga bisa langsung dikirim ke teknisi.

### Target pengguna
- Pengguna akhir dengan pengetahuan komputer dasar (< 30%).
- Tidak paham istilah IP, DNS, ping, dsb.
- Mungkin hanya bisa memfoto layar dengan HP, bukan screenshot.

### Prinsip desain
| Prinsip | Artinya |
|---|---|
| **Satu klik** | Tidak ada pilihan/menu yang wajib diisi. |
| **Tanpa instalasi & tanpa admin** | Satu file `.exe`, bisa langsung dijalankan dari Desktop/USB/link download. |
| **Read-only (v1)** | Program hanya memeriksa, **tidak mengubah** setting jaringan apa pun. |
| **Bahasa manusia dulu, istilah teknis kemudian** | Hasil utama berupa kalimat sederhana; detail teknis tetap ada di layar yang sama tapi lebih kecil. |
| **Cepat** | Seluruh pemeriksaan selesai **≤ 45 detik** dalam kondisi normal. |

---

## 2. Antarmuka & Alur Pengguna

Antarmuka dibuat **sesederhana mungkin**: satu tombol besar di tengah yang juga berfungsi sebagai **indikator status**. Pengguna cukup melihat **warna tombol** untuk tahu kondisi jaringannya.

### 2.1 Status Tombol Utama

| Status | Warna tombol | Tulisan di tombol | Ikon | Bisa diklik? |
|---|---|---|---|---|
| **Idle** (awal) | Abu-abu `#9E9E9E` | **Start Test** | ▶ | Ya → mulai tes |
| **Running** | Abu-abu dengan **animasi loading berputar** | *(tidak ada tulisan / hanya spinner)* | ⟳ berputar | Tidak (dinonaktifkan) |
| **Normal** | Hijau `#2E7D32` | **Network Runs Perfectly** | ✔ | Ya → tes ulang |
| **Masalah minor** | Oranye `#EF6C00` | **Minor Network Problem** | ⚠ | Ya → tes ulang |
| **Masalah parah** | Merah `#C62828` | **Major Network Problem** | ✖ | Ya → tes ulang |

**Detail perilaku:**
- **Bentuk:** tombol besar bulat/rounded (± 320×320 px untuk lingkaran, atau 480×140 px untuk persegi panjang rounded) di tengah jendela. Tulisan putih, tebal, ≥ 24 pt.
- **Animasi loading:** cincin/arc berputar di sekeliling atau di dalam tombol (± 1 putaran/detik). Di bawah tombol tampil satu baris kecil berisi langkah yang sedang berjalan, mis. *"Memeriksa koneksi ke router… (3/9)"*, agar user tahu program tidak hang.
- **Transisi warna:** perubahan warna halus (fade ± 300 ms) dari abu-abu ke warna hasil.
- **Tes ulang:** setelah hasil keluar, tombol tetap bisa diklik untuk menjalankan tes lagi; di bawah tombol ada teks kecil *"Klik tombol untuk tes ulang"*.
- **Tidak hanya mengandalkan warna:** setiap status punya ikon dan tulisan berbeda, supaya pengguna buta warna dan foto layar hitam-putih/buram tetap terbaca.
- **Bahasa:** aplikasi tersedia dalam **Bahasa Indonesia** dan **English** (lihat 2.3). **Tulisan pada tombol status selalu English** di kedua bahasa (Start Test / Network Runs Perfectly / Minor Network Problem / Major Network Problem); teks lain di bawah tombol mengikuti bahasa yang dipilih.

### 2.2 Alur

```
[Buka OneCheck.exe]
        │
        ▼
┌───────────────────────────────┐
│ OneCheck by PortHub           │
│                               │
│        ╭─────────────╮        │
│        │ ▶ Start Test│        │  ← abu-abu
│        ╰─────────────╯        │
│                               │
└───────────────────────────────┘
        │ klik
        ▼
┌───────────────────────────────┐
│        ╭─────────────╮        │
│        │      ⟳      │        │  ← abu-abu, spinner berputar
│        ╰─────────────╯        │
│ Memeriksa koneksi ke router…  │
│ (3/9)                         │
└───────────────────────────────┘
        │ selesai
        ▼
┌───────────────────────────────┐
│ ╭───────────────────────────╮ │
│ │ ✔ Network Runs Perfectly  │ │  ← hijau / oranye / merah
│ ╰───────────────────────────╯ │
│ Klik tombol untuk tes ulang   │
│                               │
│ (ringkasan & detail hasil,    │
│  lihat bagian 5)              │
│ [Simpan Gambar] [Salin Teks]  │
│ [Kirim ke Teknisi]            │
└───────────────────────────────┘
```

Pada status **Idle** dan **Running**, layar hanya berisi tombol (dan teks progres) — tidak ada informasi lain yang membingungkan. Ringkasan dan detail baru muncul **setelah hasil keluar**.

### 2.3 Dua Bahasa (Indonesia / English)

- Pilihan **ID | EN** di pojok kanan atas header, selalu terlihat. Bahasa bisa diganti kapan saja, termasuk **setelah hasil keluar**: kesimpulan, checklist, ringkasan teknis, dan laporan teks langsung disusun ulang dalam bahasa baru tanpa tes ulang.
- **Bahasa awal:** pilihan terakhir user di komputer itu (disimpan di `%LOCALAPPDATA%\OneCheck\language.txt`); jika belum pernah memilih, mengikuti `language` di `onecheck.json` (`"id"` default, `"en"`, atau `"auto"` = ikut bahasa Windows).
- Yang diterjemahkan: semua teks layar, Mode Teknisi, dialog PIN, pesan error, laporan **Salin Teks / WhatsApp**.
- Yang **tidak** diterjemahkan: kode diagnosis (`E04`, `W02`, …), istilah teknis (IP, DNS, ping, dBm), dan isi **QR code** (format kode singkat netral bahasa untuk teknisi).
- `branding.supportText` di config: kosong = teks bawaan per bahasa; jika diisi, teks itu dipakai di kedua bahasa.
- Semua teks ada di satu tabel (`src/OneCheck.Core/Localization/Texts.cs`); unit test memastikan setiap teks punya kedua bahasa dan tidak ada kata Indonesia tersisa di mode English.

### 2.4 Penentuan Warna Tombol

Warna tombol ditentukan dari **hasil seluruh pemeriksaan** (lihat aturan di bagian 4):

| Warna | Kapan |
|---|---|
| 🟢 **Hijau** — Network Runs Perfectly | Semua pemeriksaan OK (kode `OK`). |
| 🟠 **Oranye** — Minor Network Problem | Ada minimal satu masalah kategori **minor** (kode `Wxx`) dan **tidak ada** masalah parah. Internet masih bisa dipakai, tapi lambat/tidak stabil/sebagian layanan bermasalah. |
| 🔴 **Merah** — Major Network Problem | Ada minimal satu masalah kategori **parah** (kode `Exx`). Internet tidak bisa dipakai sama sekali. |

Jika ada beberapa masalah sekaligus, **yang paling parah menentukan warna**, dan kesimpulan utama di bawah tombol mengikuti aturan pertama yang cocok di bagian 4. Pemeriksaan yang **gagal dijalankan** (error internal, bukan masalah jaringan) tidak memengaruhi warna, tetapi ditandai "tidak dapat dicek".

---

## 3. Daftar Pemeriksaan

Pemeriksaan disusun **berlapis** (dari yang paling dekat ke komputer hingga ke internet). Hasil tiap lapis dipakai untuk menyimpulkan di mana letak masalah.

### 3.1 Pemeriksaan inti (sesuai permintaan)

| # | Pemeriksaan | Setara perintah | Data yang diambil |
|---|---|---|---|
| C1 | **Info adapter jaringan** | `ipconfig /all` | Nama adapter, jenis (WiFi/LAN), status (Up/Down), MAC, IPv4, subnet mask, default gateway, DNS server, DHCP aktif/tidak, DHCP server, lease time, IPv6 (ringkas). **Adapter & IP utama = yang benar-benar dipakai Windows untuk keluar ke internet** (alamat sumber dari route ke 8.8.8.8), bukan sekadar adapter/IP pertama — penting untuk PC dengan banyak interface virtual atau satu adapter dengan beberapa IP. IP lain di adapter yang sama ditampilkan sebagai "+N IP lain"; gateway di luar subnet IP utama diberi tanda ⚠. |
| C2 | **Ping ke gateway (router)** | `ping <gateway> -n 10` | Terkirim/diterima, packet loss %, latency min/avg/max, jitter |
| C3 | **Ping ke internet (IP)** | `ping 8.8.8.8` & `ping 1.1.1.1` | Sama dengan C2 |
| C4 | **Ping ke internet (nama domain)** | `ping google.com` | Sama dengan C2 + apakah nama berhasil di-resolve |
| C5 | **Traceroute** | `tracert -d -h 15 -w 1000 8.8.8.8` | Daftar hop (no, IP, latency), hop terakhir yang merespons |
| C6 | **DNS lookup** | `nslookup google.com` | Resolve via DNS bawaan komputer **dan** via DNS publik (8.8.8.8) → bandingkan; waktu respons DNS |

### 3.2 Pemeriksaan tambahan v1 (disetujui)

| # | Pemeriksaan | Kenapa perlu |
|---|---|---|
| A1 | **Status WiFi** — SSID, BSSID, sinyal **dBm**, link rate RX/TX dari Windows, standar (802.11n/ac/ax), band, channel. Detail di **3.3**. | Penyebab #1 keluhan "internet lemot" adalah sinyal WiFi lemah. Ping saja tidak menunjukkan ini. |
| A2 | **Cek HTTP/HTTPS + captive portal** — detail di **3.5**. | Ping bisa diblokir tapi web tetap jalan (atau sebaliknya). Ini yang paling mewakili "internet bisa dipakai". |
| A3 | **IP publik & nama ISP** — detail di **3.6**. | Teknisi langsung tahu user pakai ISP apa / sedang lewat VPN atau tidak. |
| A4 | **Deteksi VPN & Proxy** — detail di **3.7**. | VPN/proxy yang nyangkut sering membuat internet "mati" padahal jaringan normal. |
| A5 | **Cek jam komputer** — detail di **3.8**. | Jam yang salah membuat semua situs HTTPS error ("koneksi tidak aman"). Sering terjadi di PC yang baterai CMOS-nya habis. |
| A6 | **Custom Target Ping** — detail di **3.4**. | Membedakan "internet mati" dengan "hanya server/aplikasi/perangkat tertentu yang tidak bisa diakses". |
| A7 | **Deteksi IP APIPA (169.254.x.x)** / tidak ada gateway — turunan dari C1, tanpa pemeriksaan tambahan. | Tanda pasti komputer tidak dapat IP dari router (DHCP gagal). |
| A8 | **Info sistem singkat** — nama komputer, user, versi Windows, waktu cek, uptime. | Identitas laporan; uptime lama sering = "coba restart dulu". |

### 3.3 Detail Status WiFi

Hanya dijalankan jika adapter aktif adalah WiFi. Diambil lewat **Native WiFi API Windows (`wlanapi.dll`)**, bukan parsing `netsh`, agar hasil sama di Windows bahasa apa pun.

| Data | Sumber API | Keterangan |
|---|---|---|
| SSID | `WlanQueryInterface(wlan_intf_opcode_current_connection)` → `dot11Ssid` | Nama jaringan WiFi |
| BSSID | idem → `dot11Bssid` | MAC access point (berguna jika ada banyak AP dengan SSID sama) |
| **Sinyal (dBm)** | `WlanQueryInterface(wlan_intf_opcode_rssi)` → nilai RSSI dalam dBm | Nilai asli dari driver. **Fallback** jika driver tidak mendukung: `dBm ≈ (kualitas% / 2) − 100` dan diberi tanda "≈ (perkiraan)". |
| Kualitas sinyal (%) | `wlanSignalQuality` | Ditampilkan berdampingan dengan dBm |
| **Receive rate (Mbps)** | `wlanAssociationAttributes.ulRxRate` (kbps ÷ 1000) | Link rate yang dihitung Windows/driver dari **MCS index × lebar channel × jumlah spatial stream × guard interval** yang sedang dipakai — sama dengan "Receive rate" di `netsh wlan show interfaces`. |
| **Transmit rate (Mbps)** | `wlanAssociationAttributes.ulTxRate` (kbps ÷ 1000) | Sama dengan "Transmit rate" di `netsh`. |
| Standar radio | `dot11PhyType` | 802.11n (WiFi 4) / ac (WiFi 5) / ax (WiFi 6/6E) / be (WiFi 7) |
| Channel & band | `wlan_intf_opcode_channel_number` | Channel 1–14 → 2.4 GHz, 36–177 → 5 GHz, (6 GHz dari phy type 6E) |
| Keamanan | `dot11AuthAlgorithm` / `dot11CipherAlgorithm` | WPA2/WPA3, AES — info untuk teknisi |

> **Catatan MCS:** Windows tidak mengekspos nomor MCS index secara langsung ke aplikasi. Yang tersedia adalah **hasil kalkulasi rate dari MCS** (RX/TX rate di atas). Rate inilah yang ditampilkan, dengan label "Link rate (dari Windows)". Rate rendah pada sinyal kuat = indikasi interferensi atau perangkat lama.

**Kategori sinyal** (ambang konfigurabel):

| dBm | Label untuk user | Status |
|---|---|---|
| ≥ −50 | Sangat baik | 🟢 |
| −51 s/d −60 | Baik | 🟢 |
| −61 s/d −70 | Cukup | 🟡 |
| −71 s/d −80 | Lemah | 🟠 |
| < −80 | Sangat lemah | 🔴 |

Sinyal diambil **3 kali dengan jeda 1 detik**, yang ditampilkan adalah rata-rata (agar tidak tertipu fluktuasi sesaat).

### 3.4 Custom Target Ping

Memungkinkan teknisi/admin menambahkan target ping sendiri, misalnya server aplikasi, NAS, printer, CCTV, router cabang, atau server game/VoIP.

**Spesifikasi per target:**

| Field | Wajib | Contoh | Keterangan |
|---|---|---|---|
| `name` | ya | "Server Kasir" | Nama yang tampil ke user (bahasa awam) |
| `host` | ya | `10.0.0.10` / `app.porthub.id` | IP atau nama domain |
| `count` | tidak | 10 | Jumlah ping (default ikut global `pingCount`) |
| `maxLatencyMs` | tidak | 100 | Ambang "lambat" khusus target ini |
| `critical` | tidak | true | Jika gagal, jadi kesimpulan utama di layar hasil (lihat aturan W06) |
| `tcpPort` | tidak | 443 | Jika diisi, selain ICMP ping juga dicoba TCP connect ke port ini (berguna bila server memblokir ICMP) |

**Cara menambah target:**
1. **File config** `onecheck.json` (bagian `customTargets`) — untuk distribusi massal oleh teknisi.
2. **Mode Teknisi** di aplikasi — dibuka dengan `Ctrl+Shift+T` (tersembunyi dari user awam), opsional dilindungi PIN. Berisi tabel untuk tambah/ubah/hapus target dan tombol "Tes Sekarang". Disimpan ke `onecheck.json`.
3. **Parameter command line** (opsional): `OneCheck.exe --target "Server Kasir=10.0.0.10"` — bisa dipasang di shortcut Desktop.

**Batasan:** maksimal **10 target** custom agar total waktu tetap ≤ 45 detik. Semua target di-ping **paralel**.

**Tampilan hasil:** setiap target muncul sebagai baris tersendiri di checklist (mis. "✅ Server Kasir") dan di ringkasan teknis (`Server Kasir 10.0.0.10 : loss 0%, avg 4 ms`).

### 3.5 Detail Cek HTTP/HTTPS & Captive Portal

| Langkah | Target default | Penilaian |
|---|---|---|
| HTTP | `http://www.msftconnecttest.com/connecttest.txt` | Respons harus `200` dengan isi persis `Microsoft Connect Test` |
| HTTPS | `https://www.google.com/generate_204` | Respons harus `204` |
| HTTPS cadangan | `https://cp.cloudflare.com/generate_204` | Dipakai jika Google diblokir |

- **Captive portal terdeteksi** jika HTTP mengembalikan redirect (301/302/307) atau isi yang berbeda dari yang diharapkan → tampilkan URL tujuan redirect & tombol **"Buka Halaman Login"** (membuka browser).
- **Error HTTPS dibedakan**: timeout, DNS gagal, koneksi ditolak, **error sertifikat** (dikaitkan dengan cek jam & proxy).
- Waktu respons (ms) dicatat untuk masing-masing.
- Request HTTP **tidak mengikuti redirect** secara otomatis (agar redirect captive portal terlihat).

### 3.6 Detail IP Publik & ISP

- Sumber utama: `https://ipinfo.io/json` (memberi IP, kota, negara, `org` = nomor AS + nama ISP, mis. "AS7713 PT Telekomunikasi Indonesia").
- Cadangan: `https://api.ipify.org` (hanya IP) + `http://ip-api.com/json` (ISP).
- Semua URL konfigurabel; jika semua gagal tampilkan "tidak dapat dicek".
- Jika VPN aktif, beri catatan: "IP publik ini milik VPN, bukan ISP".

### 3.7 Detail Deteksi VPN & Proxy

| Item | Cara deteksi |
|---|---|
| **VPN aktif** | Adapter berstatus Up dengan tipe `Ppp` / `Tunnel`, atau deskripsi adapter mengandung kata kunci umum (WireGuard, OpenVPN, TAP-Windows, Wintun, FortiClient, Cisco AnyConnect, GlobalProtect, ZeroTier, Tailscale, dsb. — daftar konfigurabel). Juga cek apakah **default route (0.0.0.0/0)** lewat adapter tersebut. |
| **Proxy user (WinINET)** | Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings` → `ProxyEnable`, `ProxyServer`, `AutoConfigURL` (PAC) |
| **Proxy sistem (WinHTTP)** | `WinHttpGetDefaultProxyConfiguration` (setara `netsh winhttp show proxy`) |
| **Auto-detect (WPAD)** | `WinHttpGetIEProxyConfigForCurrentUser` → `fAutoDetect` |

Ditampilkan: nama VPN + apakah semua trafik lewat VPN; alamat proxy/PAC jika ada.

### 3.8 Detail Cek Jam Komputer

- Bandingkan jam lokal (UTC) dengan header `Date` dari respons HTTPS (dari langkah 3.5, tanpa request tambahan).
- Cadangan: query **NTP** ke `time.windows.com` / `pool.ntp.org` (UDP 123).
- Tampilkan juga **zona waktu** Windows (WIB/WITA/WIT).
- Selisih > 5 menit → peringatan `W04`; selisih > 24 jam → hampir pasti HTTPS akan error.

### 3.9 Backlog (v2 / belum disetujui)

| # | Pemeriksaan | Kenapa perlu |
|---|---|---|
| B1 | **Cek file hosts** — ada entri tidak standar? | Malware/aplikasi bisa membelokkan situs tertentu. |
| B2 | **Cek port penting** (TCP 80, 443, 53 ke internet) | Mendeteksi firewall yang memblokir. |
| B3 | **Speed test ringan** (download ±10 MB dari CDN) | Keluhan "lemot" butuh angka. Opsional karena menambah waktu & kuota. |
| B4 | **Uji MTU** | Masalah MTU (PPPoE/VPN) bikin sebagian situs tidak terbuka. |
| B5 | **Riwayat hasil cek** (10 terakhir, lokal) | Teknisi bisa lihat apakah masalah berulang. |
| B6 | **Perbaikan cepat 1 klik** — flush DNS, renew IP, reset adapter | Mengubah sistem & sebagian butuh admin, jadi dipisah dari v1. |

---

## 4. Logika Diagnosis (Kesimpulan Otomatis)

Setelah semua pemeriksaan selesai, program menentukan **satu kesimpulan utama** dengan aturan berurutan (aturan pertama yang cocok = penyebab utama):

Kolom **Tombol**: 🔴 = Major Network Problem (merah), 🟠 = Minor Network Problem (oranye), 🟢 = Network Runs Perfectly (hijau).

| Urutan | Kondisi | Tombol | Pesan untuk pengguna | Kode |
|---|---|---|---|---|
| 1 | Tidak ada adapter yang aktif / kabel dicabut / WiFi off | 🔴 | "Komputer tidak tersambung ke jaringan. Periksa kabel LAN atau nyalakan WiFi." | `E01` |
| 2 | WiFi tersambung tapi sinyal < −70 dBm (sinyal < −80 dBm **dan** ping gateway loss > 20% → naik jadi 🔴 `E06`) | 🟠 | "Sinyal WiFi lemah. Coba mendekat ke router." | `W01` |
| 3 | IP 169.254.x.x atau tidak ada gateway | 🔴 | "Komputer tidak mendapat alamat dari router. Coba matikan-nyalakan router." | `E02` |
| 4 | Ping gateway gagal 100% | 🔴 | "Komputer tidak bisa berkomunikasi dengan router." | `E03` |
| 5 | Gateway OK, ping IP internet gagal, HTTPS gagal | 🔴 | "Router tersambung, tapi internet dari penyedia (ISP) tidak tersedia." | `E04` |
| 6 | Ping IP internet OK, DNS bawaan gagal, DNS publik OK | 🔴 | "Internet ada, tapi pencarian nama situs (DNS) bermasalah." | `E05` |
| 7 | HTTP diarahkan ke halaman lain (captive portal) | 🟠 | "Jaringan ini meminta login. Buka browser untuk login WiFi." | `W02` |
| 8 | Proxy/VPN aktif dan HTTPS gagal | 🟠 | "Ada VPN/Proxy aktif yang mungkin mengganggu koneksi." | `W03` |
| 9 | Jam sistem selisih > 5 menit | 🟠 | "Jam komputer tidak sesuai. Ini bisa membuat situs tidak bisa dibuka." | `W04` |
| 9b | Jam selisih > 5 menit **dan** HTTPS gagal karena sertifikat | 🔴 | "Jam komputer salah sehingga website aman (HTTPS) tidak bisa dibuka." | `E09` |
| 9c | Ping & DNS OK, HTTPS gagal, bukan captive portal / VPN / proxy | 🔴 | "Internet tersambung, tapi website tidak bisa dibuka." (varian: sertifikat ditolak) | `E08` |
| 10 | Custom target bertanda `critical` gagal, internet OK | 🟠 | "Internet normal, tapi **<nama target>** tidak bisa dihubungi." | `W06` |
| 11 | Semua OK tapi packet loss > 5% atau latency internet > 150 ms (packet loss > 30% → naik jadi 🔴 `E07`) | 🟠 | "Internet tersambung tapi tidak stabil / lambat." | `W05` |
| 11b | Link rate WiFi < 20 Mbps padahal sinyal ≥ −60 dBm | 🟠 | "Sinyal WiFi kuat tapi kecepatan rendah, kemungkinan ada gangguan atau terlalu banyak pengguna." | `W07` |
| 11c | Custom target non-critical gagal / lambat | 🟠 | "Internet normal, tapi **<nama target>** lambat / tidak merespons." | `W08` |
| 12 | Semua OK | 🟢 | "Koneksi internet normal." | `OK` |

- Kode (`E01`, `W03`, dst.) ditampilkan di layar hasil agar teknisi cepat paham walau fotonya buram.
- **Internet dianggap tersambung** jika ping ke internet **atau** HTTPS berhasil, karena sebagian jaringan kantor memblokir ping.
- Jika **captive portal** terdeteksi, aturan `E03`/`E04`/`E08` tidak dipakai (gejala dari portal), sehingga kesimpulannya `W02`.
- Ambang batas (sinyal, latency, packet loss) dibuat **konfigurabel**.

---

## 5. Layar Hasil (Desain untuk di-Foto)

Seluruh hasil **harus muat di satu layar tanpa scroll** pada resolusi minimum **1366×768**. Font besar, kontras tinggi, latar terang.

```
┌──────────────────────────────────────────────────────────────────────┐
│ OneCheck by PortHub        ID Cek: OC-7F3K-92QD    26/11/2026 14:32  │
├──────────────────────────────────────────────────────────────────────┤
│            ╭──────────────────────────────────────────╮              │
│            │       ✖  Major Network Problem           │  ← MERAH     │
│            ╰──────────────────────────────────────────╯              │
│                    Klik tombol untuk tes ulang                       │
│  [E04] Router tersambung, tapi internet dari ISP tidak tersedia.     │
│  Saran: Matikan router 30 detik lalu nyalakan lagi.                  │
│  Jika masih gagal, hubungi teknisi dan kirim foto layar ini.         │
├───────────────────────┬──────────────────────────────────────────────┤
│ ✅ Kartu jaringan      │ Komputer : PC-KASIR-01 · Win 11 23H2         │
│ ✅ WiFi: Baik          │ Adapter  : Wi-Fi (Intel AX201) · UP          │
│ ✅ Router              │ SSID     : KANTOR-5G (BSSID a4:2b:..:1f)     │
│ ❌ Internet            │ Sinyal   : -56 dBm (88%) · 802.11ax · 5GHz ch36
│ ❌ Nama situs (DNS)    │ Link rate: RX 1201 / TX 864 Mbps (Windows)   │
│ ❌ Buka website        │ IP       : 192.168.1.23/24  (DHCP)           │
│ ✅ VPN/Proxy: tidak ada│ Gateway  : 192.168.1.1   ping 2ms  loss 0%   │
│ ✅ Jam komputer        │ DNS      : 192.168.1.1, 8.8.8.8              │
│ ✅ Server Kasir        │ Ping 8.8.8.8 / 1.1.1.1 : loss 100% / 100%    │
│ ❌ Portal Kantor       │ DNS google.com : gagal (timeout)             │
│   ┌────────┐          │ HTTP / HTTPS   : gagal / gagal · portal: -   │
│   │QR CODE │          │ IP Publik/ISP  : - (tidak bisa dicek)        │
│   │        │          │ Traceroute     : berhenti di hop 1 (192.168.1.1)
│   └────────┘          │ Server Kasir 10.0.0.10   : loss 0%, 4 ms     │
│                       │ Portal Kantor app.porthub: loss 100%         │
│                       │ Jam: selisih - · WIB · Uptime 12 hari        │
├───────────────────────┴──────────────────────────────────────────────┤
│      [📷 Simpan Gambar]   [📋 Salin Teks]   [📤 Kirim ke Teknisi]      │
└──────────────────────────────────────────────────────────────────────┘
```

### Elemen penting
1. **ID Cek** unik (mis. `OC-7F3K-92QD`) — memudahkan referensi tiket.
2. **Tombol status** (hijau/oranye/merah, lihat 2.1) di posisi paling atas dan paling mencolok, diikuti **kesimpulan + kode + saran** dalam bahasa awam. Pada status hijau, kesimpulan cukup "Koneksi internet normal." tanpa saran.
3. **Checklist ikon** (kiri) — untuk pengguna.
4. **Ringkasan teknis** (kanan) — untuk teknisi.
5. **QR Code** berisi ringkasan hasil dalam format ringkas (JSON terkompresi/teks). Teknisi cukup scan dari foto → dapat data lengkap tanpa harus membaca foto yang buram. *(Sangat disarankan, karena foto layar dari HP sering miring/blur.)*
6. Tab/tombol **"Detail Teknis"** (opsional, tidak wajib dibuka user) berisi output lengkap: semua adapter, semua hop traceroute, semua hasil ping per paket.

### Tombol aksi
| Tombol | Fungsi |
|---|---|
| **Simpan Gambar** | Simpan screenshot jendela hasil sebagai PNG ke Desktop (`OneCheck_OC-7F3K-92QD.png`) dan buka foldernya. |
| **Salin Teks** | Salin laporan teks lengkap ke clipboard (siap di-paste ke WhatsApp/email). |
| **Kirim ke Teknisi** | v1: buka WhatsApp Web/Desktop dengan teks laporan terisi (`https://wa.me/<nomor>?text=...`) — nomor dari config. v2: upload ke server/helpdesk & tampilkan nomor tiket. |
| *(Tes ulang)* | Tidak ada tombol terpisah — klik **tombol status utama** untuk mengulang tes (kembali ke status Running). |

---

## 6. Kebutuhan Non-Fungsional

| Aspek | Kebutuhan |
|---|---|
| **OS** | Windows 10 (1809+) dan Windows 11, 64-bit. (Windows 7/8 tidak didukung.) |
| **Distribusi** | Satu file `OneCheck.exe` portable, ukuran target < 80 MB, tanpa installer, tanpa perlu install runtime. |
| **Hak akses** | Berjalan sebagai user biasa, **tanpa admin** (v1). |
| **Waktu eksekusi** | ≤ 45 detik normal; ≤ 90 detik pada kondisi terburuk (semua timeout). Pemeriksaan independen dijalankan **paralel**. |
| **Bahasa** | Bahasa Indonesia (default) dan English, bisa diganti langsung di aplikasi (lihat 2.3). |
| **Aksesibilitas** | Font min. 14 pt untuk kesimpulan 24 pt+, ikon + warna (jangan hanya warna, untuk buta warna). |
| **Keandalan** | Satu pemeriksaan gagal/crash tidak boleh menghentikan pemeriksaan lain; tampilkan "tidak dapat dicek". |
| **Privasi** | Tidak mengirim data ke mana pun kecuali: target ping/DNS/HTTP yang sudah ditentukan dan layanan IP publik. Tidak ada telemetri di v1. |
| **Keamanan** | File `.exe` di-**code-sign** agar tidak diblokir Windows SmartScreen / antivirus (penting untuk user awam yang akan ketakutan melihat peringatan). |

---

## 7. Rekomendasi Teknologi

### Pilihan utama: **C# / .NET 10 (LTS) + WPF**, publish *self-contained single-file*

Alasan:
- Native Windows, tampilan rapi, ringan, startup cepat.
- Bisa jadi satu `.exe` tanpa install runtime.
- API bawaan .NET sudah mencakup sebagian besar kebutuhan **tanpa perlu mem-parsing output command**:

| Kebutuhan | Implementasi yang disarankan |
|---|---|
| ipconfig | `System.Net.NetworkInformation.NetworkInterface` (IP, gateway, DNS, DHCP, status) |
| ping | `System.Net.NetworkInformation.Ping` |
| traceroute | `Ping.SendPingAsync` dengan `PingOptions.Ttl` naik 1..15 (traceroute buatan sendiri, lebih cepat dari `tracert`) |
| nslookup ke DNS tertentu | Library `DnsClient.NET` (bisa query ke server DNS spesifik, mis. 8.8.8.8) |
| HTTP/HTTPS, IP publik, jam server | `HttpClient` |
| Status WiFi (SSID, dBm, RX/TX rate) | Native WiFi API (`wlanapi.dll`) via P/Invoke — `WlanOpenHandle`, `WlanEnumInterfaces`, `WlanQueryInterface` (opcode `current_connection`, `rssi`, `channel_number`). Lihat 3.3. |
| Custom target TCP | `TcpClient.ConnectAsync` dengan timeout |
| NTP (cadangan cek jam) | Query SNTP sederhana via `UdpClient` |
| Proxy | Registry `HKCU\...\Internet Settings` + `WinHttpGetIEProxyConfigForCurrentUser` + `WinHttpGetDefaultProxyConfiguration` |
| QR Code | Library `QRCoder` |
| Screenshot jendela | `RenderTargetBitmap` (WPF) |

> ⚠️ **Penting:** Hindari mem-parsing teks output `ipconfig`/`ping`/`tracert`, karena teksnya berbeda tergantung bahasa Windows (Indonesia/Inggris/dll). Gunakan API di atas.

### Alternatif
| Opsi | Kelebihan | Kekurangan |
|---|---|---|
| Python + PySide6/Tkinter + PyInstaller | Cepat dibuat | `.exe` besar, startup lambat, sering dianggap virus oleh antivirus |
| Electron/Tauri | UI web modern | Electron sangat besar; Tauri butuh Rust; akses API jaringan Windows lebih repot |
| PowerShell + WinForms | Tanpa build | Eksekusi script sering diblokir policy, tampilan terbatas |

---

## 8. Konfigurasi

File `onecheck.json` opsional di folder yang sama dengan `.exe` (jika tidak ada → pakai default yang tertanam). Ini memungkinkan satu program dipakai banyak klien PortHub dengan branding/target berbeda.

```json
{
  "branding": {
    "companyName": "PortHub",
    "logoPath": "logo-klien.png",   // logo layar awal, relatif ke folder exe; kosong = logo OneCheck
    "supportPhone": "62812xxxxxxx",
    "supportText": "Hubungi IT Support PortHub"
  },
  "targets": {
    "pingPublic": ["8.8.8.8", "1.1.1.1"],
    "dnsTestDomain": "google.com",
    "dnsPublicServer": "8.8.8.8",
    "httpCheckUrl": "http://www.msftconnecttest.com/connecttest.txt",
    "httpCheckExpectedBody": "Microsoft Connect Test",
    "httpsCheckUrls": ["https://www.google.com/generate_204", "https://cp.cloudflare.com/generate_204"],
    "captivePortalOpenUrl": "http://www.msftconnecttest.com/redirect",
    "traceTarget": "8.8.8.8",
    "publicIpUrls": ["https://ipinfo.io/json", "http://ip-api.com/json", "https://api.ipify.org"],
    "ntpServers": ["time.windows.com", "pool.ntp.org"]
  },
  "customTargets": [
    { "name": "Server Kasir",  "host": "10.0.0.10", "critical": true },
    { "name": "Portal Kantor", "host": "app.porthub.id", "tcpPort": 443, "maxLatencyMs": 100 },
    { "name": "Printer Lt.2",  "host": "192.168.1.50", "count": 4 }
  ],
  "technicianMode": { "enabled": true, "pin": "" },
  "vpnKeywords": ["WireGuard", "OpenVPN", "TAP-Windows", "Wintun", "FortiClient",
                  "AnyConnect", "GlobalProtect", "ZeroTier", "Tailscale"],
  "thresholds": {
    "wifiSignalDbm": { "excellent": -50, "good": -60, "fair": -70, "weak": -80 },
    "wifiLowRateMbps": 20,
    "highLatencyMs": 150,
    "packetLossWarnPercent": 5,
    "packetLossMajorPercent": 30,
    "gatewayLossMajorPercent": 20,
    "clockSkewWarnMinutes": 5
  },
  "pingCount": 10,
  "traceMaxHops": 15,
  "language": "id"   // "id" | "en" | "auto"
}
```

---

## 9. Format Laporan Teks (untuk "Salin Teks")

```
=== OneCheck by PortHub ===
ID Cek   : OC-7F3K-92QD
Waktu    : 26/11/2026 14:32:10
Komputer : PC-KASIR-01 (Windows 11 23H2), uptime 12 hari
Hasil    : [E04] Router tersambung, tapi internet dari ISP tidak tersedia

[Adapter] Wi-Fi (Intel AX201) UP, MAC 3C:xx:xx:xx:xx:xx
[WiFi] SSID KANTOR-5G, BSSID a4:2b:xx:xx:xx:1f, WPA2-AES
  Sinyal -56 dBm (88%, Baik), 802.11ax, 5 GHz ch 36
  Link rate RX 1201 Mbps / TX 864 Mbps (dari Windows)
  IPv4 192.168.1.23/24 (DHCP dari 192.168.1.1)
  Gateway 192.168.1.1 | DNS 192.168.1.1, 8.8.8.8
[Ping] 192.168.1.1 : 10/10, loss 0%, avg 2 ms
[Ping] 8.8.8.8     : 0/10, loss 100%
[Ping] 1.1.1.1     : 0/10, loss 100%
[DNS ] google.com via 192.168.1.1 : TIMEOUT | via 8.8.8.8 : TIMEOUT
[HTTP] connecttest : GAGAL | generate_204 : GAGAL | captive portal: tidak
[IP Publik] tidak dapat dicek
[Trace] 8.8.8.8 : 1) 192.168.1.1 2ms  2) *  3) *  ...
[Custom] Server Kasir 10.0.0.10      : 10/10, loss 0%, avg 4 ms
[Custom] Portal Kantor app.porthub.id: 0/10, loss 100% | TCP 443: GAGAL
[Custom] Printer Lt.2 192.168.1.50   : 4/4, loss 0%, avg 3 ms
[Proxy/VPN] VPN: tidak ada | Proxy: tidak ada | WPAD: off
[Jam] 14:32:10 WIB, selisih tidak dapat dicek
```

---

## 10. Struktur Modul (Usulan)

```
OneCheck/
├── OneCheck.App/            # UI WPF (layar mulai, progres, hasil)
├── OneCheck.Core/
│   ├── Checks/              # satu class per pemeriksaan, implement ICheck
│   │   ├── AdapterCheck.cs
│   │   ├── WifiCheck.cs
│   │   ├── PingCheck.cs
│   │   ├── TracerouteCheck.cs
│   │   ├── DnsCheck.cs
│   │   ├── HttpCheck.cs
│   │   ├── PublicIpCheck.cs
│   │   ├── ProxyVpnCheck.cs
│   │   ├── ClockCheck.cs
│   │   └── CustomTargetCheck.cs
│   ├── Native/              # P/Invoke wlanapi.dll, winhttp.dll
│   ├── Diagnosis/           # rule engine (bagian 4) → kesimpulan + kode
│   ├── Reporting/           # laporan teks, QR payload, PNG
│   └── Config/              # load onecheck.json + default
└── OneCheck.Tests/          # unit test rule engine & parsing
```

Kontrak tiap pemeriksaan:
```csharp
interface ICheck {
    string Id { get; }            // "ping_gateway"
    string DisplayName { get; }   // "Koneksi ke router"
    Task<CheckResult> RunAsync(CheckContext ctx, CancellationToken ct);
}
// CheckResult: Status (Ok/Warn/Fail/Skipped/Error), Summary, Details (key-value), RawLog
```

---

## 11. Tahapan Pengerjaan

| Fase | Isi | Hasil |
|---|---|---|
| **M1 — Inti** ✅ selesai | C1–C6 + A1 (WiFi), A7, A8; config file + custom target (A6) dari config; layar mulai/progres/hasil sederhana; laporan teks + simpan PNG | Bisa dipakai internal untuk uji |
| **M2 — Diagnosis & UX** ✅ selesai | A2–A5; Mode Teknisi untuk custom target; rule engine bagian 4; desain layar hasil final; QR code | **Rilis v1** ke pengguna |
| **M3 — Distribusi** 🟡 sebagian | ✅ Ikon & logo OneCheck (warna brand navy `#144467` / cyan `#00AEEF`), metadata exe (versi 1.0.0), branding per klien (`companyName`, `logoPath`, `supportPhone`, `supportText`, `language`), skrip rilis `scripts/release.ps1` (publish + code signing + zip), checklist uji `TESTING.md`. ⏳ Menunggu: sertifikat code signing, uji di berbagai PC sesuai `TESTING.md` | `.exe` siap sebar |
| **M4 — v2** | Backlog B1–B6 + upload ke helpdesk | Rilis v2 |

---

## 12. Skenario Uji Minimum

| # | Skenario | Hasil yang diharapkan |
|---|---|---|
| 1 | Kabel LAN dicabut & WiFi off | `E01` |
| 2 | Tersambung WiFi jauh dari router | `W01` |
| 3 | DHCP router dimatikan | `E02` |
| 4 | Kabel WAN router dicabut | `E04` |
| 5 | DNS diset manual ke IP tidak valid | `E05` |
| 6 | WiFi publik dengan halaman login | `W02` |
| 7 | Proxy palsu diset di Windows | `W03` |
| 8 | Jam Windows dimundurkan 1 hari | `W04` |
| 9 | Jaringan normal | `OK`, selesai ≤ 45 detik |
| 10 | Windows berbahasa Inggris vs Indonesia | Hasil identik |
| 11 | Dijalankan sebagai user non-admin | Semua pemeriksaan v1 berjalan |
| 12 | Bandingkan dBm & RX/TX rate dengan `netsh wlan show interfaces` di 3+ merek kartu WiFi (Intel, Realtek, MediaTek/Qualcomm) | Rate sama persis; dBm sama (atau ditandai "≈" jika fallback) |
| 13 | VPN aktif (WireGuard/OpenVPN) | Terdeteksi, nama VPN tampil, catatan IP publik milik VPN |
| 14 | Custom target critical dimatikan | `W06` dengan nama target |
| 15 | Custom target memblokir ICMP tapi port TCP terbuka | Ditandai OK via TCP, catatan "ping diblokir" |
| 16 | 10 custom target, sebagian timeout | Total waktu tetap ≤ 45 detik |
| 17 | PC pakai kabel LAN (tanpa WiFi) | Bagian WiFi disembunyikan, diganti "LAN · 1 Gbps" |

---

## 13. Hal yang Perlu Diputuskan

1. **Cara kirim ke teknisi di v1:** cukup foto + salin teks, atau langsung buka WhatsApp ke nomor support?
2. **Apakah ada server/helpdesk PortHub** yang nanti bisa menerima laporan otomatis (untuk v2)?
3. **Custom target default** yang perlu dibawa per klien (server aplikasi, printer, dsb.)?
4. **Mode Teknisi:** perlu PIN atau cukup shortcut tersembunyi?
5. **Branding:** satu versi umum, atau versi khusus per klien?
6. **Code signing certificate:** sudah punya atau perlu dibeli (± USD 200–400/tahun)? Skrip `scripts/release.ps1` sudah siap memakai file `.pfx` atau sertifikat terpasang/token USB (`-Thumbprint`). Sejak perubahan SmartScreen oleh Microsoft, sertifikat **EV tidak lagi** memberi reputasi instan: OV maupun EV sama-sama membangun reputasi dari jumlah unduhan. Rekomendasi: **OV atas nama badan usaha PortHub dengan cloud signing** (lihat diskusi pemilihan sertifikat).
7. **Layanan IP publik/ISP:** ipinfo.io gratis dibatasi ±50.000 request/bulan; perlu token berbayar jika pengguna banyak, atau pakai server PortHub sendiri?
