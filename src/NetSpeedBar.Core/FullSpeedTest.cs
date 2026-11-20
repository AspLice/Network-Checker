namespace NetSpeedBar.Core;

/// <summary>Manual-only orchestration. Callers must never invoke it from an automatic schedule.</summary>
public sealed class FullSpeedTest(ThroughputProbe probe)
{
    public async Task<MeasurementResult> RunAsync(MonitorSettings settings, bool includeDownload, bool includeUpload, CancellationToken cancellationToken)
    {
        if (!includeDownload && !includeUpload) throw new ArgumentException("Select download or upload.");
        ThroughputSample? download = null;
        ThroughputSample? upload = null;
        if (includeDownload)
        {
            ArgumentNullException.ThrowIfNull(settings.DownloadEndpoint);
            download = await probe.DownloadAsync(settings.DownloadEndpoint, MeasurementKind.FullSpeed, settings.FullSpeedBudget, cancellationToken).ConfigureAwait(false);
        }
        if (includeUpload && !cancellationToken.IsCancellationRequested)
        {
            ArgumentNullException.ThrowIfNull(settings.UploadEndpoint);
            upload = await probe.UploadAsync(settings.UploadEndpoint, MeasurementKind.FullSpeed, settings.FullSpeedBudget, cancellationToken).ConfigureAwait(false);
        }
        return new(null, download, upload, (download?.PayloadBytes ?? 0) + (upload?.PayloadBytes ?? 0), cancellationToken.IsCancellationRequested);
    }
}
