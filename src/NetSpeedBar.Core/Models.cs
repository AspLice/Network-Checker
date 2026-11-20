namespace NetSpeedBar.Core;

public enum MeasurementKind { HttpQuality, LightweightEstimate, FullSpeed }
public enum ConnectionState { Healthy, Degraded, Offline, Unknown }
public enum Profile { DataSaver, Balanced, Diagnostics }

/// <summary>HTTP round-trip timing; it is not ICMP ping or TCP connect time.</summary>
public sealed record QualitySample(
    DateTimeOffset MeasuredAt,
    TimeSpan? HttpRoundTrip,
    bool Succeeded,
    ConnectionState State,
    string? FailureReason = null);

public sealed record ThroughputSample(
    MeasurementKind Kind,
    DateTimeOffset MeasuredAt,
    long PayloadBytes,
    TimeSpan Elapsed,
    double MegabitsPerSecond,
    Uri Server,
    bool IsComplete,
    string? Note = null);

public sealed record MeasurementResult(
    QualitySample? Quality,
    ThroughputSample? Download,
    ThroughputSample? Upload,
    long TotalMeasuredBytes,
    bool Cancelled);

public static class ThroughputCalculator
{
    public static double MegabitsPerSecond(long bytes, TimeSpan elapsed)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        if (elapsed <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        return bytes * 8d / elapsed.TotalSeconds / 1_000_000d;
    }
}
