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
            var requestUri = AddCloudflarePayloadLimit(endpoint, budget.MaxBytes);
            using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, timed.Token).ConfigureAwait(false);
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
            return new(kind, DateTimeOffset.UtcNow, read, elapsed, ThroughputCalculator.MegabitsPerSecond(read, elapsed), requestUri, true,
                read >= budget.MaxBytes ? "Payload budget reached; this is an estimate, not line capacity." : null);
        }
        catch (OperationCanceledException)
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            var note = cancellationToken.IsCancellationRequested ? "Cancelled" : "Measurement time budget reached";
            return new(kind, DateTimeOffset.UtcNow, read, elapsed, elapsed > TimeSpan.Zero ? ThroughputCalculator.MegabitsPerSecond(read, elapsed) : 0, endpoint, false, note);
        }
    }

    private static Uri AddCloudflarePayloadLimit(Uri endpoint, long maxBytes)
    {
        if (!string.Equals(endpoint.Host, "speed.cloudflare.com", StringComparison.OrdinalIgnoreCase) || endpoint.AbsolutePath != "/__down")
            return endpoint;
        var separator = string.IsNullOrEmpty(endpoint.Query) ? "?" : "&";
        return new Uri($"{endpoint.AbsoluteUri}{separator}bytes={maxBytes}");
    }

    public async Task<ThroughputSample> UploadAsync(Uri endpoint, MeasurementKind kind, MeasurementBudget budget, CancellationToken cancellationToken)
    {
        budget.Validate();
        using var timed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timed.CancelAfter(budget.MaxDuration);
        var started = Stopwatch.GetTimestamp();
        long sent = 0;
        try
        {
            await using var payload = new GeneratedPayloadStream(budget.MaxBytes, count => sent += count);
            using var content = new StreamContent(payload);
            content.Headers.ContentLength = budget.MaxBytes;
            using var response = await client.PostAsync(endpoint, content, timed.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var elapsed = Stopwatch.GetElapsedTime(started);
            return new(kind, DateTimeOffset.UtcNow, sent, elapsed, ThroughputCalculator.MegabitsPerSecond(sent, elapsed), endpoint, true,
                "Manual upload measurement completed.");
        }
        catch (OperationCanceledException)
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            var note = cancellationToken.IsCancellationRequested ? "Cancelled" : "Measurement time budget reached";
            return new(kind, DateTimeOffset.UtcNow, sent, elapsed, elapsed > TimeSpan.Zero ? ThroughputCalculator.MegabitsPerSecond(sent, elapsed) : 0, endpoint, false, note);
        }
    }

    private sealed class GeneratedPayloadStream : Stream
    {
        private readonly long _length;
        private readonly Action<int> _onRead;
        private long _remaining;
        public GeneratedPayloadStream(long length, Action<int> onRead)
        {
            _length = length;
            _remaining = length;
            _onRead = onRead;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _length - _remaining; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            var count = (int)Math.Min(buffer.Length, _remaining);
            buffer[..count].Clear();
            _remaining -= count;
            _onRead(count);
            return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => Task.FromResult(Read(buffer, offset, count));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => ValueTask.FromResult(Read(buffer.Span));
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
