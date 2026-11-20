namespace NetSpeedBar.Core;

/// <summary>One-at-a-time quality scheduler with bounded exponential backoff.</summary>
public sealed class QualityMonitor(QualityProbe probe, QualityHistory history)
{
    public event EventHandler<QualitySample>? SampleAvailable;

    public async Task RunAsync(MonitorSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings.QualityEndpoint);
        var failures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var sample = await probe.ProbeAsync(settings.QualityEndpoint, settings.RequestTimeout, cancellationToken).ConfigureAwait(false);
            history.Add(sample);
            SampleAvailable?.Invoke(this, sample);
            failures = sample.Succeeded ? 0 : Math.Min(failures + 1, 5);
            var multiplier = 1 << failures;
            var delay = TimeSpan.FromTicks(Math.Min(settings.QualityInterval.Ticks * multiplier, TimeSpan.FromMinutes(10).Ticks));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
