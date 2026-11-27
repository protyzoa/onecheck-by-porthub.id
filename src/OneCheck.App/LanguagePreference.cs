using System.IO;
using OneCheck.Core.Localization;

namespace OneCheck.App;

/// <summary>
/// Mengingat bahasa yang dipilih user di komputer ini (%LOCALAPPDATA%\OneCheck\language.txt).
/// Terpisah dari onecheck.json agar folder .exe tidak perlu bisa ditulis.
/// </summary>
internal static class LanguagePreference
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneCheck", "language.txt");

    public static Language? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;
            var code = File.ReadAllText(FilePath).Trim();
            return code is "id" or "en" ? Texts.Parse(code) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(Language lang)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, Texts.Code(lang));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tidak fatal: pilihan bahasa hanya berlaku untuk sesi ini.
        }
    }
}
