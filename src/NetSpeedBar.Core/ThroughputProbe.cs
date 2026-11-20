using System.Diagnostics;

namespace NetSpeedBar.Core;

public sealed class ThroughputProbe(HttpClient client)
{
    public async Task<ThroughputSample> DownloadAsync(Uri endpoint, MeasurementKind kind, MeasurementBudget budget, CancellationToken cancellationToken)
    {
        budget.Validate();
        using var timed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timed.CancelAfter(budget.MaxDuration);
        var started = Stopwatch.GetTimestamp();
        long read = 0;
        try
        {
            using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, timed.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(timed.Token).ConfigureAwait(false);
            var buffer = new byte[32 * 1024];
            while (read < budget.MaxBytes)
            {
                var allowed = (int)Math.Min(buffer.Length, budget.MaxBytes - read);
                var count = await body.ReadAsync(buffer.AsMemory(0, allowed), timed.Token).ConfigureAwait(false);
                if (count == 0) break;
                read += count;
            }
            var elapsed = Stopwatch.GetElapsedTime(started);
            return new(kind, DateTimeOffset.UtcNow, read, elapsed, ThroughputCalculator.MegabitsPerSecond(read, elapsed), endpoint, true,
                read >= budget.MaxBytes ? "Payload budget reached; this is an estimate, not line capacity." : null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            return new(kind, DateTimeOffset.UtcNow, read, elapsed, elapsed > TimeSpan.Zero ? ThroughputCalculator.MegabitsPerSecond(read, elapsed) : 0, endpoint, false, "Cancelled");
        }
    }
}
