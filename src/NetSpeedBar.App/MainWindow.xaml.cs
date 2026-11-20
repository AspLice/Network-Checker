using NetSpeedBar.Core;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace NetSpeedBar.App;

public partial class MainWindow : System.Windows.Window
{
    private readonly HttpClient _client = new();
    private readonly QualityHistory _history = new();
    private readonly NetworkActivitySampler _networkActivity = new();
    private readonly CancellationTokenSource _appCancellation = new();
    private readonly JsonSettingsStore _settingsStore;
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private CancellationTokenSource? _measurementCancellation;
    private CancellationTokenSource? _qualityMonitorCancellation;
    private CancellationTokenSource? _automaticSchedulerCancellation;
    private MonitorSettings _settings = MonitorSettings.Create();
    private bool _allowExit;
    public MainWindow()
    {
        InitializeComponent();
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetSpeedBar", "settings.json");
        _settingsStore = new JsonSettingsStore(settingsPath);
        _trayIcon = CreateTrayIcon();
        StatusText.Text = "初期化中…";
        Loaded += async (_, _) => await InitializeAsync();
    }
    private async Task InitializeAsync()
    {
        var exists = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetSpeedBar", "settings.json"));
        _settings = await _settingsStore.LoadAsync(_appCancellation.Token);
        if (!exists) await _settingsStore.SaveAsync(_settings, _appCancellation.Token);
        WindowsStartup.Apply(_settings.StartWithWindows);
        StatusText.Text = $"{_settings.Profile} モード: Cloudflare Speed Test を使用します。自動スループット測定は {(_settings.AutomaticLightweightEnabled ? "有効" : "無効")} です。";
        StartQualityMonitoring();
        StartAutomaticLightweightScheduler();
    }
    private void StartQualityMonitoring()
    {
        _qualityMonitorCancellation?.Cancel();
        _qualityMonitorCancellation?.Dispose();
        _qualityMonitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(_appCancellation.Token);
        var monitor = new QualityMonitor(new QualityProbe(_client), _history);
        monitor.SampleAvailable += (_, sample) => Dispatcher.Invoke(() => UpdateQuality(sample));
        _ = Task.Run(async () =>
        {
            try { await monitor.RunAsync(_settings, _qualityMonitorCancellation.Token); }
            catch (OperationCanceledException) when (_qualityMonitorCancellation.IsCancellationRequested) { }
        });
    }
    private void StartAutomaticLightweightScheduler()
    {
        _automaticSchedulerCancellation?.Cancel();
        _automaticSchedulerCancellation?.Dispose();
        if (!_settings.AutomaticLightweightEnabled) return;
        _automaticSchedulerCancellation = CancellationTokenSource.CreateLinkedTokenSource(_appCancellation.Token);
        _networkActivity.SampleBytesPerSecond(); // establishes an adapter-statistics baseline without measuring throughput.
        var scheduler = new AutomaticLightweightScheduler(
            new ThroughputProbe(_client),
            _history,
            _networkActivity.SampleBytesPerSecond,
            () => SuppressingProcessDetector.IsRunning(_settings.SuppressingProcessNames));
        scheduler.DecisionMade += (_, decision) => Dispatcher.Invoke(() => StatusText.Text = decision.Reason);
        scheduler.MeasurementCompleted += (_, result) => Dispatcher.Invoke(() =>
        {
            DownloadText.Text = $"↓ {result.MegabitsPerSecond:F1} Mbps（軽量推定）";
            DownloadHintText.Text = result.Note ?? "自動軽量測定による推定速度。";
            TimestampText.Text = $"自動測定: {result.MeasuredAt.LocalDateTime:G} / {result.PayloadBytes / 1024d / 1024d:F2} MB";
        });
        _ = Task.Run(async () =>
        {
            try { await scheduler.RunAsync(_settings, _automaticSchedulerCancellation.Token); }
            catch (OperationCanceledException) when (_automaticSchedulerCancellation.IsCancellationRequested) { }
        });
    }
    private void UpdateQuality(QualitySample sample)
    {
        QualityText.Text = sample.HttpRoundTrip is { } rtt ? $"{rtt.TotalMilliseconds:F0} ms (HTTP)" : sample.FailureReason ?? "失敗";
        TimestampText.Text = $"品質確認: {sample.MeasuredAt.LocalDateTime:G}";
        var stats = _history.GetStatistics();
        QualityStatsText.Text = $"成功率 {stats.SuccessRate:P0} / ジッター {(stats.Jitter?.TotalMilliseconds.ToString("F0") ?? "—")} ms";
        _trayIcon.Text = sample.HttpRoundTrip is { } trayRtt ? $"NetSpeedBar: {trayRtt.TotalMilliseconds:F0} ms (HTTP)" : "NetSpeedBar: 接続を確認中";
    }
    private async void QualityButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.QualityEndpoint is null) { StatusText.Text = "品質監視エンドポイントが未設定です。信頼できる測定サーバーを設定してください。"; return; }
        QualityButton.IsEnabled = false;
        try
        {
            var sample = await new QualityProbe(_client).ProbeAsync(_settings.QualityEndpoint, _settings.RequestTimeout, _appCancellation.Token);
            _history.Add(sample);
            UpdateQuality(sample);
        }
        finally { QualityButton.IsEnabled = true; }
    }
    private async void EstimateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.DownloadEndpoint is null) { StatusText.Text = "軽量測定サーバーが未設定です。許可されたサーバーを設定してください。"; return; }
        BeginMeasurement();
        try
        {
            var sample = await new ThroughputProbe(_client).DownloadAsync(_settings.DownloadEndpoint, MeasurementKind.LightweightEstimate, _settings.LightweightBudget, _measurementCancellation!.Token);
            DownloadText.Text = $"↓ {sample.MegabitsPerSecond:F1} Mbps（軽量推定）";
            DownloadHintText.Text = sample.Note ?? "軽量測定による推定速度。回線の最大速度ではありません。";
            TimestampText.Text = $"測定: {sample.MeasuredAt.LocalDateTime:G} / {sample.PayloadBytes / 1024d / 1024d:F2} MB";
        }
        catch (Exception ex) { StatusText.Text = $"測定不可: {ex.Message}"; }
        finally { EndMeasurement(); }
    }
    private async void FullButton_Click(object sender, RoutedEventArgs e)
    {
        BeginMeasurement();
        try
        {
            var result = await new FullSpeedTest(new ThroughputProbe(_client)).RunAsync(_settings, includeDownload: true, includeUpload: false, _measurementCancellation!.Token);
            var sample = result.Download!;
            DownloadText.Text = $"↓ {sample.MegabitsPerSecond:F1} Mbps（フル測定）";
            DownloadHintText.Text = sample.Note ?? "手動フル測定。通信量と時間の上限を適用しています。";
            TimestampText.Text = $"測定: {sample.MeasuredAt.LocalDateTime:G} / {sample.PayloadBytes / 1024d / 1024d:F2} MB";
        }
        catch (Exception ex) { StatusText.Text = $"測定不可: {ex.Message}"; }
        finally { EndMeasurement(); }
    }
    private void BeginMeasurement()
    {
        _measurementCancellation = CancellationTokenSource.CreateLinkedTokenSource(_appCancellation.Token);
        EstimateButton.IsEnabled = FullButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
    }
    private void EndMeasurement()
    {
        _measurementCancellation?.Dispose();
        _measurementCancellation = null;
        EstimateButton.IsEnabled = FullButton.IsEnabled = true;
        CancelButton.IsEnabled = false;
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e) => _measurementCancellation?.Cancel();
    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Settings is null) return;
        _settings = dialog.Settings;
        await _settingsStore.SaveAsync(_settings, _appCancellation.Token);
        WindowsStartup.Apply(_settings.StartWithWindows);
        StartQualityMonitoring();
        StartAutomaticLightweightScheduler();
        StatusText.Text = "設定を保存しました。監視スケジュールを再開しました。";
    }
    private System.Windows.Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("表示", null, (_, _) => Dispatcher.Invoke(ShowWindow));
        menu.Items.Add("終了", null, (_, _) => Dispatcher.Invoke(() => { _allowExit = true; Close(); }));
        return new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "NetSpeedBar", Visible = true, ContextMenuStrip = menu };
    }
    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowExit) { e.Cancel = true; Hide(); return; }
        _appCancellation.Cancel();
        _qualityMonitorCancellation?.Dispose();
        _automaticSchedulerCancellation?.Dispose();
        _trayIcon.Dispose();
        _client.Dispose();
        _appCancellation.Dispose();
    }
}
