using NetSpeedBar.Core;
using System.Globalization;
using System.Windows;

namespace NetSpeedBar.App;

public partial class SettingsWindow : Window
{
    private readonly MonitorSettings _original;
    public MonitorSettings? Settings { get; private set; }
    public SettingsWindow(MonitorSettings settings)
    {
        _original = settings;
        InitializeComponent();
        ProfileBox.SelectedIndex = (int)settings.Profile;
        AutomaticBox.IsChecked = settings.AutomaticLightweightEnabled;
        StartupBox.IsChecked = settings.StartWithWindows;
        QualityEndpointBox.Text = settings.QualityEndpoint?.AbsoluteUri ?? "";
        DownloadEndpointBox.Text = settings.DownloadEndpoint?.AbsoluteUri ?? "";
        UploadEndpointBox.Text = settings.UploadEndpoint?.AbsoluteUri ?? "";
        TrafficThresholdBox.Text = (settings.BusyTrafficThresholdBytesPerSecond / 1024d).ToString(CultureInfo.InvariantCulture);
        ProcessNamesBox.Text = string.Join(", ", settings.SuppressingProcessNames);
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(QualityEndpointBox.Text, UriKind.Absolute, out var quality) || !Uri.TryCreate(DownloadEndpointBox.Text, UriKind.Absolute, out var download) || !Uri.TryCreate(UploadEndpointBox.Text, UriKind.Absolute, out var upload) || !double.TryParse(TrafficThresholdBox.Text, CultureInfo.InvariantCulture, out var threshold) || threshold < 0)
        {
            System.Windows.MessageBox.Show("URL と通信量しきい値を確認してください。", "NetSpeedBar");
            return;
        }
        var profile = (Profile)ProfileBox.SelectedIndex;
        var automatic = AutomaticBox.IsChecked == true;
        var profileDefaults = MonitorSettings.Create(profile);
        Settings = _original with
        {
            QualityEndpoint = quality,
            DownloadEndpoint = download,
            UploadEndpoint = upload,
            Profile = profile,
            QualityInterval = profileDefaults.QualityInterval,
            LightweightBudget = profileDefaults.LightweightBudget,
            AutomaticLightweightEnabled = automatic,
            BusyTrafficThresholdBytesPerSecond = checked((long)(threshold * 1024)),
            SuppressingProcessNames = ProcessNamesBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            StartWithWindows = StartupBox.IsChecked == true
        };
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
