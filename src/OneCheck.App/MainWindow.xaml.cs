using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using OneCheck.Core;
using OneCheck.Core.Diagnosis;
using OneCheck.Core.Localization;
using OneCheck.Core.Models;
using OneCheck.Core.Reporting;
// Window punya properti bernama Language, jadi enum bahasa diberi alias.
using UiLanguage = OneCheck.Core.Localization.Language;

namespace OneCheck.App;

public partial class MainWindow : Window
{
    private enum UiState { Idle, Running, Result }

    private static readonly Color Gray = Color.FromRgb(0x9E, 0x9E, 0x9E);
    private static readonly Color Green = Color.FromRgb(0x2E, 0x7D, 0x32);
    private static readonly Color Orange = Color.FromRgb(0xEF, 0x6C, 0x00);
    private static readonly Color Red = Color.FromRgb(0xC6, 0x28, 0x28);

    private readonly OneCheckConfig _config;
    private readonly SolidColorBrush _buttonBrush = new(Gray);
    private UiState _state = UiState.Idle;
    private Language _lang;
    private NetworkReport? _lastReport;
    private string _reportText = "";

    // Pesan di bawah tombol disimpan sebagai kunci + argumen agar bisa diterjemahkan ulang saat bahasa diganti.
    private string _statusKey = "ui.idle_hint";
    private object?[] _statusArgs = [];
    private RunProgress? _lastProgress;

    public MainWindow()
    {
        InitializeComponent();
        MainButton.Background = _buttonBrush;

        var (config, warning) = OneCheckConfig.LoadFromDirectory(AppContext.BaseDirectory);
        config.ApplyCommandLine(Environment.GetCommandLineArgs().Skip(1).ToList());
        _config = config;
        _lang = LanguagePreference.Load() ?? Texts.Parse(_config.Language);

        HeaderCompany.Text = $"by {_config.Branding.CompanyName}";
        Title = $"OneCheck by {_config.Branding.CompanyName}";
        ApplyCustomLogo();
        if (warning is not null)
            SetStatus("ui.config_warning", OneCheckConfig.FileName, warning);

        ApplyLanguage();
    }

    private string T(string key, params object?[] args) => Texts.Get(_lang, key, args);

    /// <summary>
    /// Branding per klien: jika branding.logoPath di onecheck.json menunjuk file gambar (relatif ke folder .exe),
    /// logo besar di layar awal diganti dengan logo tersebut. Gagal dimuat → tetap logo OneCheck.
    /// </summary>
    private void ApplyCustomLogo()
    {
        var path = _config.Branding.LogoPath;
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!Path.IsPathRooted(path))
            path = Path.Combine(AppContext.BaseDirectory, path);
        if (!File.Exists(path))
            return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            BrandLogo.Source = image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or ArgumentException)
        {
            // logo bawaan tetap dipakai
        }
    }

    // ---------- Bahasa ----------

    private void LangId_Click(object sender, RoutedEventArgs e) => SwitchLanguage(UiLanguage.Id);

    private void LangEn_Click(object sender, RoutedEventArgs e) => SwitchLanguage(UiLanguage.En);

    private void SwitchLanguage(Language lang)
    {
        if (lang == _lang)
            return;
        _lang = lang;
        LanguagePreference.Save(lang);
        ApplyLanguage();
    }

    /// <summary>Menerapkan bahasa ke semua teks, termasuk menyusun ulang hasil tes terakhir.</summary>
    private void ApplyLanguage()
    {
        App.CurrentLanguage = _lang;
        var selected = (Brush)FindResource("TextPrimary");
        var muted = (Brush)FindResource("TextMuted");
        LangIdButton.Foreground = _lang == UiLanguage.Id ? selected : muted;
        LangEnButton.Foreground = _lang == UiLanguage.En ? selected : muted;
        LangIdButton.FontWeight = _lang == UiLanguage.Id ? FontWeights.Bold : FontWeights.Normal;
        LangEnButton.FontWeight = _lang == UiLanguage.En ? FontWeights.Bold : FontWeights.Normal;
        LangPanel.ToolTip = T("ui.lang_tip");

        ScanText.Text = T("ui.scan");
        OpenPortalButton.Content = "🌐  " + T("action.portal");
        SaveImageButton.Content = "📷  " + T("action.save");
        CopyTextButton.Content = "📋  " + T("action.copy");
        SendButton.Content = "📤  " + T("action.send");
        UpdateSendButton();

        switch (_state)
        {
            case UiState.Idle:
                ButtonLabel.Text = T("btn.start");
                AutomationName(ButtonLabel.Text);
                break;
            case UiState.Result when _lastReport is not null:
                RenderResult(_lastReport);
                break;
        }

        if (_state == UiState.Running && _lastProgress is not null)
            ShowProgress(_lastProgress);
        else
            ProgressText.Text = T(_statusKey, _statusArgs);
    }

    private void SetStatus(string key, params object?[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        ProgressText.Text = T(key, args);
    }

    private void UpdateSendButton()
    {
        var hasPhone = !string.IsNullOrWhiteSpace(_config.Branding.SupportPhone);
        SendButton.IsEnabled = hasPhone;
        SendButton.ToolTip = hasPhone ? null : T("ui.no_phone");
    }

    // ---------- Tes ----------

    private async void MainButton_Click(object sender, RoutedEventArgs e)
    {
        if (_state == UiState.Running)
            return;
        await RunTestAsync();
    }

    private async Task RunTestAsync()
    {
        SetRunning();
        var progress = new Progress<RunProgress>(ShowProgress);

        NetworkReport report;
        try
        {
            report = await Task.Run(() => new DiagnosticRunner(_config).RunAsync(progress));
        }
        catch (Exception ex)
        {
            SetIdle();
            SetStatus("ui.cannot_run", ex.Message);
            return;
        }

        _lastReport = report;
        _state = UiState.Result;
        _lastProgress = null;
        StopSpinner();
        RenderResult(report);
        SetStatus("ui.retest");
    }

    private void ShowProgress(RunProgress p)
    {
        _lastProgress = p;
        ProgressText.Text = p.Completed >= p.Total
            ? T("ui.finishing")
            : T("ui.progress", T(p.CurrentStep), p.Completed + 1, p.Total);
    }

    // ---------- Status tombol ----------

    private void SetIdle()
    {
        _state = UiState.Idle;
        StopSpinner();
        AnimateButton(Gray);
        ButtonIcon.Text = "▶";
        ButtonLabel.Text = T("btn.start");
        MainButton.IsHitTestVisible = true;
    }

    private void SetRunning()
    {
        _state = UiState.Running;
        _lastProgress = null;
        MainButton.IsHitTestVisible = false;
        AutomationName("Testing");
        AnimateButton(Gray);
        ButtonContent.Visibility = Visibility.Collapsed;
        Spinner.Visibility = Visibility.Visible;
        SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });

        SetButtonSize(large: true);
        BrandLogo.Visibility = Visibility.Visible;
        ResultCard.Visibility = Visibility.Collapsed;
        ActionBar.Visibility = Visibility.Collapsed;
        HeaderMeta.Visibility = Visibility.Collapsed;
        System.Windows.Controls.Grid.SetRowSpan(ButtonArea, 2);
        ButtonArea.VerticalAlignment = VerticalAlignment.Center;
        SetStatus("ui.starting");
    }

    /// <summary>Menampilkan hasil dalam bahasa aktif. Dipanggil lagi saat bahasa diganti.</summary>
    private void RenderResult(NetworkReport report)
    {
        var dx = DiagnosisEngine.Evaluate(report, _config.Thresholds, _lang);

        var (color, icon) = dx.Overall switch
        {
            Severity.Ok => (Green, "✔"),
            Severity.Minor => (Orange, "⚠"),
            _ => (Red, "✖"),
        };
        AnimateButton(color);
        ButtonIcon.Text = icon;
        ButtonLabel.Text = ReportFormatter.OverallLabel(dx.Overall, _lang);
        AutomationName(ButtonLabel.Text);
        MainButton.IsHitTestVisible = true;

        // Kesimpulan
        FindingCode.Text = $"[{dx.Primary.Code}]";
        FindingMessage.Text = dx.Primary.Message;
        var advice = dx.Primary.Advice ?? "";
        if (dx.Overall != Severity.Ok)
            advice = (advice + " " + SupportText()).Trim();
        FindingAdvice.Text = advice;
        FindingAdvice.Visibility = advice.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        var others = dx.All.Where(f => f != dx.Primary).Select(f => $"[{f.Code}] {f.Message}").ToList();
        OtherFindings.Text = others.Count > 0 ? T("ui.also") + string.Join("  ·  ", others) : "";
        OtherFindings.Visibility = others.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var unknownSuffix = T("ui.unknown_suffix");
        ChecklistList.ItemsSource = dx.Checklist.Select(i => ChecklistRow.From(i, unknownSuffix)).ToList();
        SummaryList.ItemsSource = ReportFormatter.SummaryLines(report, _lang).Select(l => new { l.Key, l.Value }).ToList();

        HeaderMeta.Text = T("ui.check_id", report.CheckId) + "    " + ReportFormatter.FormatTimestamp(report.StartedAt);
        QrImage.Source = QrRenderer.Render(ReportFormatter.CompactSummary(report, dx));
        OpenPortalButton.Visibility = report.Http?.CaptivePortal == true ? Visibility.Visible : Visibility.Collapsed;

        HeaderMeta.Visibility = Visibility.Visible;
        // Tombol mengecil dan pindah ke baris atas, kartu hasil di bawahnya (tidak saling menimpa).
        SetButtonSize(large: false);
        BrandLogo.Visibility = Visibility.Collapsed;
        System.Windows.Controls.Grid.SetRowSpan(ButtonArea, 1);
        ButtonArea.VerticalAlignment = VerticalAlignment.Top;
        ResultCard.Visibility = Visibility.Visible;
        ActionBar.Visibility = Visibility.Visible;

        _reportText = ReportFormatter.FullText(report, dx, _config.Branding.CompanyName, _lang);
    }

    private string SupportText()
        => string.IsNullOrWhiteSpace(_config.Branding.SupportText) ? T("support.default") : _config.Branding.SupportText;

    private void AnimateButton(Color to)
        => _buttonBrush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(to, TimeSpan.FromMilliseconds(300)) { FillBehavior = FillBehavior.HoldEnd });

    private void StopSpinner()
    {
        SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        Spinner.Visibility = Visibility.Collapsed;
        ButtonContent.Visibility = Visibility.Visible;
    }

    private void AutomationName(string name)
        => System.Windows.Automation.AutomationProperties.SetName(MainButton, name);

    /// <summary>Besar saat idle/running; lebih kecil di layar hasil agar semua hasil muat satu layar.</summary>
    private void SetButtonSize(bool large)
    {
        MainButton.Height = large ? 130 : 96;
        ButtonLabel.FontSize = ButtonIcon.FontSize = large ? 34 : 30;
    }

    // ---------- Mode Teknisi ----------

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.T
            || System.Windows.Input.Keyboard.Modifiers != (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
            return;
        e.Handled = true;
        if (!_config.TechnicianMode.Enabled || _state == UiState.Running)
            return;

        if (!string.IsNullOrEmpty(_config.TechnicianMode.Pin))
        {
            var pin = PinDialog.Ask(this, _lang);
            if (pin is null)
                return;
            if (pin != _config.TechnicianMode.Pin)
            {
                MessageBox.Show(this, T("ui.pin_wrong"), T("tech.mode"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var window = new TechnicianWindow(_config, _lang) { Owner = this };
        if (window.ShowDialog() == true)
        {
            UpdateSendButton();
            SetStatus("ui.settings_saved");
        }
    }

    // ---------- Tombol aksi ----------

    private void Warn(string message) => MessageBox.Show(this, message, "OneCheck", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void OpenPortal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_config.Targets.CaptivePortalOpenUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Warn(T("ui.browser_failed", ex.Message));
        }
    }

    private void SaveImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var width = (int)Math.Ceiling(RootGrid.ActualWidth * dpi.DpiScaleX);
            var height = (int)Math.Ceiling(RootGrid.ActualHeight * dpi.DpiScaleY);
            var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(RootGrid);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"OneCheck_{_lastReport?.CheckId}.png");
            using (var fs = File.Create(path))
                encoder.Save(fs);

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            SetStatus("ui.saved", Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Warn(T("ui.save_failed", ex.Message));
        }
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        // Clipboard kadang sedang dipakai aplikasi lain; coba beberapa kali.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(_reportText);
                SetStatus("ui.copied");
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(100);
            }
        }
        Warn(T("ui.copy_failed"));
    }

    private void Send_Click(object sender, RoutedEventArgs e)
    {
        var phone = new string(_config.Branding.SupportPhone.Where(char.IsDigit).ToArray());
        if (phone.Length == 0)
            return;
        var text = _reportText.Length > 3000 ? _reportText[..3000] + "\n…" : _reportText;
        try
        {
            Process.Start(new ProcessStartInfo($"https://wa.me/{phone}?text={Uri.EscapeDataString(text)}") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Warn(T("ui.wa_failed", ex.Message));
        }
    }

    private sealed record ChecklistRow(string Icon, Brush Brush, string Label)
    {
        private static readonly Brush OkBrush = Frozen(Green);
        private static readonly Brush WarnBrush = Frozen(Orange);
        private static readonly Brush FailBrush = Frozen(Red);
        private static readonly Brush UnknownBrush = Frozen(Gray);

        public static ChecklistRow From(ChecklistItem item, string unknownSuffix) => item.Status switch
        {
            ItemStatus.Ok => new("✔", OkBrush, item.Label),
            ItemStatus.Warn => new("⚠", WarnBrush, item.Label),
            ItemStatus.Fail => new("✖", FailBrush, item.Label),
            _ => new("?", UnknownBrush, item.Label + unknownSuffix),
        };

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
