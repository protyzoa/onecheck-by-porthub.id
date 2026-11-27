using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace OneCheck.App;

internal static class QrRenderer
{
    /// <summary>QR dengan koreksi error M (tahan ±15% kerusakan, cukup untuk foto layar agak buram).</summary>
    public static BitmapImage Render(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M, forceUtf8: true);
        var png = new PngByteQRCode(data).GetGraphic(8);

        var image = new BitmapImage();
        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
