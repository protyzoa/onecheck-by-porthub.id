using System.Windows;
using System.Windows.Threading;
using OneCheck.Core.Localization;

namespace OneCheck.App;

public partial class App : Application
{
    /// <summary>Bahasa aktif, dipakai juga untuk pesan error tak terduga.</summary>
    public static Language CurrentLanguage { get; set; } = Language.Id;

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(Texts.Get(CurrentLanguage, "ui.crash", e.Exception.Message),
            "OneCheck", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
