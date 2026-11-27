using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using OneCheck.Core.Checks;
using OneCheck.Core.Localization;
using OneCheck.Core.Models;

namespace OneCheck.App;

/// <summary>Mode Teknisi: kelola target custom, nomor WhatsApp, dan PIN. Disimpan ke onecheck.json.</summary>
public partial class TechnicianWindow : Window
{
    private readonly OneCheckConfig _config;
    private readonly Language _lang;
    private readonly ObservableCollection<TargetRow> _rows;

    public TechnicianWindow(OneCheckConfig config, Language lang)
    {
        InitializeComponent();
        _config = config;
        _lang = lang;

        Title = T("tech.title");
        HeadingText.Text = T("tech.heading");
        HintText.Text = T("tech.hint");
        string[] headers = ["tech.col.name", "tech.col.host", "tech.col.port", "tech.col.count", "tech.col.max", "tech.col.critical", "tech.col.result"];
        for (var i = 0; i < headers.Length; i++)
            TargetGrid.Columns[i].Header = T(headers[i]);
        AddButton.Content = T("tech.add");
        RemoveButton.Content = T("tech.remove");
        TestButton.Content = T("tech.test");
        PhoneLabel.Text = T("tech.phone");
        PinLabel.Text = T("tech.pin");
        PinBox.ToolTip = T("tech.pin_tip");
        CancelButton.Content = T("tech.cancel");
        SaveButton.Content = T("tech.save");

        _rows = new ObservableCollection<TargetRow>(config.CustomTargets.Select(TargetRow.From));
        _rows.CollectionChanged += (_, _) => UpdateAddButton();
        TargetGrid.ItemsSource = _rows;
        PhoneBox.Text = config.Branding.SupportPhone;
        PinBox.Text = config.TechnicianMode.Pin;
        UpdateAddButton();
    }

    private string T(string key, params object?[] args) => Texts.Get(_lang, key, args);

    private void UpdateAddButton() => AddButton.IsEnabled = _rows.Count < OneCheckConfig.MaxCustomTargets;

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var row = new TargetRow();
        _rows.Add(row);
        TargetGrid.SelectedItem = row;
        TargetGrid.ScrollIntoView(row);
        TargetGrid.CurrentCell = new System.Windows.Controls.DataGridCellInfo(row, TargetGrid.Columns[0]);
        TargetGrid.BeginEdit();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (TargetGrid.SelectedItem is TargetRow row)
            _rows.Remove(row);
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        TargetGrid.CommitEdit();
        if (!TryBuildTargets(out var targets))
            return;
        TestButton.IsEnabled = false;
        try
        {
            var valid = _rows.Where(r => !r.IsBlank).ToList();
            foreach (var row in valid)
                row.Result = T("tech.testing");
            var results = await Task.WhenAll(targets.Select(t =>
                Task.Run(() => CustomTargetCheck.RunAsync(t, Math.Min(_config.PingCount, 4), _config.PingTimeoutMs, CancellationToken.None))));
            for (var i = 0; i < valid.Count; i++)
            {
                var r = results[i];
                var ping = r.Ping.AllLost
                    ? T("tech.ping_failed")
                    : T("tech.ping_ok", $"{r.Ping.AvgMs:0}", $"{r.Ping.LossPercent:0}");
                var tcp = r.TcpOk is { } ok ? " · " + T(ok ? "tech.tcp_ok" : "tech.tcp_failed") : "";
                valid[i].Result = (r.Reachable ? "✔ " : "✖ ") + ping + tcp;
            }
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        TargetGrid.CommitEdit();
        if (!TryBuildTargets(out var targets))
            return;

        _config.CustomTargets = targets;
        _config.Branding.SupportPhone = new string(PhoneBox.Text.Where(char.IsDigit).ToArray());
        _config.TechnicianMode.Pin = PinBox.Text.Trim();
        try
        {
            _config.SaveToDirectory(AppContext.BaseDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, T("tech.save_failed", OneCheckConfig.FileName, ex.Message),
                T("tech.mode"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        DialogResult = true;
    }

    private bool TryBuildTargets(out List<CustomTarget> targets)
    {
        targets = [];
        foreach (var (row, index) in _rows.Select((r, i) => (r, i + 1)))
        {
            if (row.IsBlank)
                continue;
            if (!row.TryToTarget(out var target, out var error))
            {
                MessageBox.Show(this, T("tech.row_error", index, T(error!)), T("tech.mode"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            targets.Add(target!);
        }
        return true;
    }

    /// <summary>Baris tabel; angka disimpan sebagai teks agar kolom boleh kosong dan validasi terjadi saat simpan.</summary>
    public sealed class TargetRow : INotifyPropertyChanged
    {
        private string _result = "";

        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public string TcpPort { get; set; } = "";
        public string Count { get; set; } = "";
        public string MaxLatencyMs { get; set; } = "";
        public bool Critical { get; set; }

        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }

        public bool IsBlank => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Host);

        public static TargetRow From(CustomTarget t) => new()
        {
            Name = t.Name,
            Host = t.Host,
            TcpPort = t.TcpPort?.ToString() ?? "",
            Count = t.Count?.ToString() ?? "",
            MaxLatencyMs = t.MaxLatencyMs?.ToString() ?? "",
            Critical = t.Critical,
        };

        public bool TryToTarget(out CustomTarget? target, out string? error)
        {
            target = null;
            error = null;
            if (string.IsNullOrWhiteSpace(Host) || Host.Trim().Contains(' '))
                return Fail("tech.err.host", out error);
            if (!TryInt(TcpPort, 1, 65535, out var port))
                return Fail("tech.err.port", out error);
            if (!TryInt(Count, 1, 50, out var count))
                return Fail("tech.err.count", out error);
            if (!TryInt(MaxLatencyMs, 1, 60000, out var max))
                return Fail("tech.err.max", out error);

            target = new CustomTarget
            {
                Name = Name.Trim(),
                Host = Host.Trim(),
                TcpPort = port,
                Count = count,
                MaxLatencyMs = max,
                Critical = Critical,
            };
            return true;
        }

        private static bool Fail(string message, out string? error)
        {
            error = message;
            return false;
        }

        private static bool TryInt(string text, int min, int max, out int? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            if (!int.TryParse(text.Trim(), out var v) || v < min || v > max)
                return false;
            value = v;
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
