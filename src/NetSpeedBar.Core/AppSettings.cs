namespace NetSpeedBar.Core;

public sealed record MeasurementBudget(long MaxBytes, TimeSpan MaxDuration, int MaxConcurrentConnections, TimeSpan MinimumInterval)
{
    public void Validate()
    {
        if (MaxBytes <= 0 || MaxDuration <= TimeSpan.Zero || MaxConcurrentConnections is < 1 or > 4 || MinimumInterval < TimeSpan.Zero)
            throw new InvalidOperationException("Invalid measurement budget.");
    }
}

public sealed record MonitorSettings(
    Uri? QualityEndpoint,
    Uri? DownloadEndpoint,
    Uri? UploadEndpoint,
    Profile Profile,
    TimeSpan QualityInterval,
    TimeSpan RequestTimeout,
    MeasurementBudget LightweightBudget,
    MeasurementBudget FullSpeedBudget,
    bool AutomaticLightweightEnabled,
    long BusyTrafficThresholdBytesPerSecond,
    double LatencySuppressionMultiplier,
    IReadOnlyList<string> SuppressingProcessNames,
    bool StartWithWindows)
{
    public static MonitorSettings Create(Profile profile = Profile.DataSaver) => profile switch
    {
        Profile.Balanced => Default(profile, TimeSpan.FromMinutes(30), true),
        Profile.Diagnostics => Default(profile, TimeSpan.FromMinutes(5), true) with { QualityInterval = TimeSpan.FromSeconds(10) },
        _ => Default(Profile.DataSaver, TimeSpan.FromMinutes(30), false)
    };

    private static MonitorSettings Default(Profile profile, TimeSpan automaticInterval, bool automatic) => new(
        // Cloudflare's public speedtest engine documents these endpoints. Automatic
        // throughput remains opt-in; callers still apply the local byte/time budget.
        QualityEndpoint: new Uri("https://speed.cloudflare.com/__down?bytes=0"),
        DownloadEndpoint: new Uri("https://speed.cloudflare.com/__down"),
        UploadEndpoint: new Uri("https://speed.cloudflare.com/__up"), profile,
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2),
        new MeasurementBudget(2 * 1024 * 1024, TimeSpan.FromSeconds(3), 1, automaticInterval),
        new MeasurementBudget(200 * 1024 * 1024, TimeSpan.FromSeconds(30), 4, TimeSpan.Zero),
        automatic, 256 * 1024, 2.0, new[] { "FiveM", "FiveM_GTAProcess" }, false);
}
