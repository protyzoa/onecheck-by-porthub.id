# Membuat ikon aplikasi (multi-ukuran .ico) dari logo perisai.
# Pemakaian:  powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1
# Sumber   :  image\Asset 9center.png   (perisai transparan)
# Hasil    :  src\OneCheck.App\Assets\onecheck.ico  (16, 20, 24, 32, 40, 48, 64, 128, 256 px)
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\image\Asset 9center.png'),
    [string]$Output = (Join-Path $PSScriptRoot '..\src\OneCheck.App\Assets\onecheck.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

# BITMAPINFOHEADER + piksel BGRA bottom-up + AND mask (semua 0, transparansi dari alpha).
function ConvertTo-Dib([System.Drawing.Bitmap]$bmp) {
    $n = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $maskRow = [int]([Math]::Ceiling($n / 32.0) * 4)
    $w.Write([UInt32]40); $w.Write([Int32]$n); $w.Write([Int32]($n * 2))
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
    $w.Write([UInt32]($n * $n * 4 + $maskRow * $n)); $w.Write([Int32]0); $w.Write([Int32]0)
    $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $n - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $n; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
        }
    }
    $w.Write((New-Object byte[] ($maskRow * $n)))
    $w.Flush()
    # Koma mencegah PowerShell memecah byte[] menjadi object[] saat dikembalikan.
    return , $ms.ToArray()
}
$src = [System.Drawing.Image]::FromFile((Resolve-Path $Source))
$frames = @()
try {
    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        # Logo dimuat di tengah kanvas persegi, margin kecil (lebih tipis di ukuran kecil agar tetap terbaca).
        $margin = if ($size -le 24) { 0 } else { [int]($size * 0.04) }
        $box = $size - 2 * $margin
        $scale = [Math]::Min($box / $src.Width, $box / $src.Height)
        $w = [int][Math]::Round($src.Width * $scale); $h = [int][Math]::Round($src.Height * $scale)
        $g.DrawImage($src, [int](($size - $w) / 2), [int](($size - $h) / 2), $w, $h)
        $g.Dispose()
        if ($size -ge 256) {
            # 256 px disimpan sebagai PNG (standar ikon Vista+).
            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $bytes = $ms.ToArray()
        }
        else {
            # Ukuran kecil disimpan sebagai DIB 32-bit (BMP) karena sebagian komponen Windows tidak membaca PNG kecil.
            [byte[]]$bytes = ConvertTo-Dib $bmp
        }
        $bmp.Dispose()
        $frames += , @{ Size = $size; Bytes = $bytes }
    }
}
finally { $src.Dispose() }

# Format ICO: header 6 byte, direktori 16 byte per gambar, lalu data PNG.
New-Item -ItemType Directory -Force (Split-Path $Output) | Out-Null
$fs = [System.IO.File]::Create($Output)
$bw = New-Object System.IO.BinaryWriter $fs
try {
    $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($f in $frames) {
        $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([UInt16]1); $bw.Write([UInt16]32)
        $bw.Write([UInt32]$f.Bytes.Length); $bw.Write([UInt32]$offset)
        $offset += $f.Bytes.Length
    }
    foreach ($f in $frames) { $bw.Write([byte[]]$f.Bytes) }
}
finally { $bw.Dispose() }
Write-Host "Ikon dibuat: $Output ($($frames.Count) ukuran)"
