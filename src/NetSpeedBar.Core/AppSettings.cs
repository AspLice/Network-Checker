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
    IReadOnlyList<string> SuppressingProcessNames)
{
    public static MonitorSettings Create(Profile profile = Profile.DataSaver) => profile switch
    {
        Profile.Balanced => Default(profile, TimeSpan.FromMinutes(30), true),
        Profile.Diagnostics => Default(profile, TimeSpan.FromMinutes(5), true) with { QualityInterval = TimeSpan.FromSeconds(10) },
        _ => Default(Profile.DataSaver, TimeSpan.FromMinutes(30), false)
    };

    private static MonitorSettings Default(Profile profile, TimeSpan automaticInterval, bool automatic) => new(
        QualityEndpoint: null, DownloadEndpoint: null, UploadEndpoint: null, profile,
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2),
        new MeasurementBudget(2 * 1024 * 1024, TimeSpan.FromSeconds(3), 1, automaticInterval),
        new MeasurementBudget(200 * 1024 * 1024, TimeSpan.FromSeconds(30), 4, TimeSpan.Zero),
        automatic, 256 * 1024, 2.0, Array.Empty<string>());
}
