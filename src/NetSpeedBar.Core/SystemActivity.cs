using System.Diagnostics;
using System.Net.NetworkInformation;

namespace NetSpeedBar.Core;

/// <summary>Aggregates active non-loopback adapter traffic; it does not attribute traffic to a game.</summary>
public sealed class NetworkActivitySampler
{
    private long? _previousBytes;
    private DateTimeOffset? _previousAt;
    public long SampleBytesPerSecond()
    {
        var now = DateTimeOffset.UtcNow;
        var total = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Sum(n => n.GetIPStatistics().BytesReceived + n.GetIPStatistics().BytesSent);
        var rate = _previousBytes is { } previous && _previousAt is { } previousAt && now > previousAt
            ? (long)Math.Max(0, (total - previous) / (now - previousAt).TotalSeconds)
            : 0;
        _previousBytes = total;
        _previousAt = now;
        return rate;
    }
}

public static class SuppressingProcessDetector
{
    public static bool IsRunning(IReadOnlyList<string> names)
    {
        if (names.Count == 0) return false;
        var expected = names.Select(Normalize).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Process.GetProcesses().Any(p => expected.Contains(Normalize(p.ProcessName)));
    }
    private static string Normalize(string name) => Path.GetFileNameWithoutExtension(name.Trim());
}
