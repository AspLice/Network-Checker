namespace NetSpeedBar.Core;

/// <summary>Runs only opt-in lightweight estimates and re-evaluates safeguards before every request.</summary>
public sealed class AutomaticLightweightScheduler(ThroughputProbe probe, QualityHistory history, Func<long> trafficBytesPerSecond, Func<bool> suppressingApplicationRunning)
{
    public event EventHandler<AutomaticMeasurementDecision>? DecisionMade;
    public event EventHandler<ThroughputSample>? MeasurementCompleted;

    public async Task RunAsync(MonitorSettings settings, CancellationToken cancellationToken)
    {
        DateTimeOffset? previousRun = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var stats = history.GetStatistics();
            var baseline = history.GetStatistics(excludeLatest: true);
            var latest = history.Samples.LastOrDefault()?.HttpRoundTrip;
            var decision = AutomaticMeasurementGate.Decide(
                settings,
                trafficBytesPerSecond(),
                suppressingApplicationRunning(),
                latest,
                baseline.MedianHttpRoundTrip,
                previousRun,
                DateTimeOffset.UtcNow);
            DecisionMade?.Invoke(this, decision);
            if (decision.Allowed && settings.DownloadEndpoint is not null)
            {
                var result = await probe.DownloadAsync(settings.DownloadEndpoint, MeasurementKind.LightweightEstimate, settings.LightweightBudget, cancellationToken).ConfigureAwait(false);
                previousRun = DateTimeOffset.UtcNow;
                MeasurementCompleted?.Invoke(this, result);
            }
            await Task.Delay(settings.LightweightBudget.MinimumInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
