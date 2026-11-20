using System.Diagnostics;

namespace NetSpeedBar.Core;

public sealed class QualityProbe(HttpClient client)
{
    public async Task<QualitySample> ProbeAsync(Uri endpoint, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(started);
            return response.IsSuccessStatusCode
                ? new(DateTimeOffset.UtcNow, elapsed, true, ConnectionState.Healthy)
                : new(DateTimeOffset.UtcNow, elapsed, false, ConnectionState.Degraded, $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(DateTimeOffset.UtcNow, null, false, ConnectionState.Offline, "Timed out");
        }
        catch (HttpRequestException ex)
        {
            return new(DateTimeOffset.UtcNow, null, false, ConnectionState.Offline, ex.Message);
        }
    }
}
