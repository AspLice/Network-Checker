using NetSpeedBar.Core;
using System.Net.Http;
using System.Windows;

namespace NetSpeedBar.App;

public partial class MainWindow : System.Windows.Window
{
    private readonly HttpClient _client = new();
    private MonitorSettings _settings = MonitorSettings.Create();
    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = "省通信モード: 自動スループット測定は無効です。測定先は設定ファイルで明示的に指定してください。";
    }
    private async void QualityButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.QualityEndpoint is null) { StatusText.Text = "品質監視エンドポイントが未設定です。信頼できる測定サーバーを設定してください。"; return; }
        QualityButton.IsEnabled = false;
        try
        {
            var sample = await new QualityProbe(_client).ProbeAsync(_settings.QualityEndpoint, _settings.RequestTimeout, CancellationToken.None);
            QualityText.Text = sample.HttpRoundTrip is { } rtt ? $"{rtt.TotalMilliseconds:F0} ms (HTTP)" : sample.FailureReason ?? "失敗";
            TimestampText.Text = $"品質確認: {sample.MeasuredAt.LocalDateTime:G}";
        }
        finally { QualityButton.IsEnabled = true; }
    }
    private async void EstimateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.DownloadEndpoint is null) { StatusText.Text = "軽量測定サーバーが未設定です。許可されたサーバーを設定してください。"; return; }
        EstimateButton.IsEnabled = false;
        try
        {
            var sample = await new ThroughputProbe(_client).DownloadAsync(_settings.DownloadEndpoint, MeasurementKind.LightweightEstimate, _settings.LightweightBudget, CancellationToken.None);
            DownloadText.Text = $"↓ {sample.MegabitsPerSecond:F1} Mbps（軽量推定）";
            TimestampText.Text = $"測定: {sample.MeasuredAt.LocalDateTime:G} / {sample.PayloadBytes / 1024d / 1024d:F2} MB";
        }
        catch (Exception ex) { StatusText.Text = $"測定不可: {ex.Message}"; }
        finally { EstimateButton.IsEnabled = true; }
    }
}
