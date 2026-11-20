namespace NetSpeedBar.Core;

public sealed record AutomaticMeasurementDecision(bool Allowed, string Reason);

public static class AutomaticMeasurementGate
{
    public static AutomaticMeasurementDecision Decide(MonitorSettings settings, long activeTrafficBytesPerSecond, bool suppressingAppRunning, TimeSpan? latestHttpRoundTrip, TimeSpan? baselineHttpRoundTrip, DateTimeOffset? previousRun, DateTimeOffset now)
    {
        if (!settings.AutomaticLightweightEnabled) return new(false, "Automatic lightweight measurement is disabled.");
        if (suppressingAppRunning) return new(false, "A configured game or application is running.");
        if (activeTrafficBytesPerSecond >= settings.BusyTrafficThresholdBytesPerSecond) return new(false, "Other network traffic is above the configured threshold.");
        if (latestHttpRoundTrip is { } latest && baselineHttpRoundTrip is { } baseline && latest > TimeSpan.FromTicks((long)(baseline.Ticks * settings.LatencySuppressionMultiplier)))
            return new(false, "HTTP round-trip time is elevated.");
        if (previousRun is { } previous && now - previous < settings.LightweightBudget.MinimumInterval) return new(false, "Minimum re-measurement interval has not elapsed.");
        return new(true, "Measurement budget and network-impact safeguards permit a run.");
    }
}
