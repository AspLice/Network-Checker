namespace NetSpeedBar.Core;

/// <summary>In-memory quality history only; measurements are intentionally not persisted.</summary>
public sealed class QualityHistory(int capacity = 120)
{
    private readonly Queue<QualitySample> _samples = new();
    public IReadOnlyList<QualitySample> Samples => _samples.ToArray();

    public void Add(QualitySample sample)
    {
        _samples.Enqueue(sample);
        while (_samples.Count > capacity) _samples.Dequeue();
    }

    public QualityStatistics GetStatistics(bool excludeLatest = false)
    {
        var source = excludeLatest ? _samples.Take(Math.Max(0, _samples.Count - 1)).ToArray() : _samples.ToArray();
        if (source.Length == 0) return new(0, 0, null, null, ConnectionState.Unknown);
        var successful = source.Where(s => s.Succeeded && s.HttpRoundTrip is not null).Select(s => s.HttpRoundTrip!.Value).OrderBy(x => x).ToArray();
        var successRate = (double)source.Count(s => s.Succeeded) / source.Length;
        TimeSpan? median = successful.Length == 0 ? null : successful[successful.Length / 2];
        var jitterPairs = successful.Zip(successful.Skip(1), (first, second) => (second - first).Duration()).ToArray();
        TimeSpan? jitter = jitterPairs.Length == 0 ? null : TimeSpan.FromTicks((long)jitterPairs.Average(x => x.Ticks));
        var state = source.Last().State;
        return new(source.Length, successRate, median, jitter, state);
    }
}
