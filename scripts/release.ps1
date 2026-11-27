# Membuat paket rilis OneCheck: publish single-file, (opsional) code signing, lalu zip.
#
# Tanpa sertifikat (exe tidak ditandatangani, SmartScreen akan memberi peringatan):
#   powershell -ExecutionPolicy Bypass -File scripts\release.ps1
#
# Dengan sertifikat .pfx:
#   powershell -ExecutionPolicy Bypass -File scripts\release.ps1 -PfxPath C:\cert\porthub.pfx
#   (password diminta secara aman, tidak ditulis di command line)
#
# Dengan sertifikat yang sudah terpasang di Windows / token USB (EV) — pakai thumbprint:
#   powershell -ExecutionPolicy Bypass -File scripts\release.ps1 -Thumbprint 0123ABCD...
#
# Hasil: dist\OneCheck-<versi>\OneCheck.exe, onecheck.sample.json, dan dist\OneCheck-<versi>.zip
param(
    [string]$PfxPath,
    [string]$Thumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [string]$SignTool
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$csproj = Join-Path $root 'src\OneCheck.App\OneCheck.App.csproj'
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$outDir = Join-Path $root "dist\OneCheck-$version"

Write-Host "== Publish OneCheck $version =="
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
dotnet publish $csproj -c Release -o $outDir -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish gagal" }
$exe = Join-Path $outDir 'OneCheck.exe'

if ($PfxPath -or $Thumbprint) {
    if (-not $SignTool) {
        $SignTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $SignTool) { throw "signtool.exe tidak ditemukan. Instal 'Windows SDK Signing Tools' atau isi -SignTool <path>." }

    Write-Host "== Code signing dengan $SignTool =="
    $signArgs = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', '/d', 'OneCheck by PortHub')
    if ($PfxPath) {
        $secure = Read-Host "Password untuk $PfxPath" -AsSecureString
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
        $signArgs += @('/f', $PfxPath, '/p', $plain)
    }
    else {
        $signArgs += @('/sha1', $Thumbprint)
    }
    & $SignTool @signArgs $exe
    if ($LASTEXITCODE -ne 0) { throw "signtool sign gagal" }
    & $SignTool verify /pa /q $exe
    if ($LASTEXITCODE -ne 0) { throw "Verifikasi tanda tangan gagal" }
    Write-Host "Tanda tangan OK."
}
else {
    Write-Warning "Tanpa sertifikat: OneCheck.exe TIDAK ditandatangani. Windows SmartScreen akan menampilkan peringatan 'Windows protected your PC'."
}

$zip = "$outDir.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "== Selesai =="
Write-Host "  $exe ($sizeMb MB)"
Write-Host "  $zip"
