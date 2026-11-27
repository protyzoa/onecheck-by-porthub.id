using System.Windows;
using System.Windows.Controls;
using OneCheck.Core.Localization;

namespace OneCheck.App;

/// <summary>Dialog kecil untuk memasukkan PIN Mode Teknisi.</summary>
internal static class PinDialog
{
    public static string? Ask(Window owner, Language lang)
    {
        var box = new PasswordBox { Width = 200, FontSize = 16, Padding = new Thickness(4), Margin = new Thickness(0, 8, 0, 12) };
        var ok = new Button { Content = Texts.Get(lang, "dlg.ok"), Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = Texts.Get(lang, "tech.cancel"), Width = 80, IsCancel = true };
        var dialog = new Window
        {
            Title = Texts.Get(lang, "tech.mode"),
            Owner = owner,
            Icon = owner.Icon,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = Texts.Get(lang, "tech.pin_prompt"), FontSize = 15 },
                    box,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } },
                },
            },
        };
        ok.Click += (_, _) => dialog.DialogResult = true;
        dialog.Loaded += (_, _) => box.Focus();
        return dialog.ShowDialog() == true ? box.Password : null;
    }
}
