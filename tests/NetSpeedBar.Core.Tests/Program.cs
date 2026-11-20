using System.Net;
using System.Net.Http;
using NetSpeedBar.Core;

var tests = new (string Name, Action Run)[]
{
    ("speed calculation", () => Equal(8d, ThroughputCalculator.MegabitsPerSecond(1_000_000, TimeSpan.FromSeconds(1)))),
    ("invalid duration rejected", () => Throws<ArgumentOutOfRangeException>(() => ThroughputCalculator.MegabitsPerSecond(1, TimeSpan.Zero))),
    ("data saver blocks automatic estimate", () => False(AutomaticMeasurementGate.Decide(MonitorSettings.Create(), 0, false, null, null, null, DateTimeOffset.UtcNow).Allowed)),
    ("busy traffic blocks automatic estimate", () => False(AutomaticMeasurementGate.Decide(MonitorSettings.Create(Profile.Balanced), 999_999, false, null, null, null, DateTimeOffset.UtcNow).Allowed)),
    ("elevated HTTP RTT blocks automatic estimate", () => False(AutomaticMeasurementGate.Decide(MonitorSettings.Create(Profile.Balanced), 0, false, TimeSpan.FromMilliseconds(101), TimeSpan.FromMilliseconds(50), null, DateTimeOffset.UtcNow).Allowed)),
    ("download payload budget enforced", DownloadBudgetIsEnforced),
    ("quality timeout maps to offline", QualityTimeoutMapsToOffline),
    ("settings JSON round trip", SettingsRoundTrip)
};
var failures = 0;
foreach (var (name, run) in tests) { try { run(); Console.WriteLine($"PASS {name}"); } catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); } }
return failures;

static void DownloadBudgetIsEnforced()
{
    using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[4096]) }));
    var budget = new MeasurementBudget(1024, TimeSpan.FromSeconds(2), 1, TimeSpan.Zero);
    var result = new ThroughputProbe(client).DownloadAsync(new Uri("https://example.test/file"), MeasurementKind.LightweightEstimate, budget, CancellationToken.None).GetAwaiter().GetResult();
    Equal(1024L, result.PayloadBytes); True(result.IsComplete);
}
static void QualityTimeoutMapsToOffline()
{
    using var client = new HttpClient(new StubHandler(_ => throw new TaskCanceledException()));
    var result = new QualityProbe(client).ProbeAsync(new Uri("https://example.test/health"), TimeSpan.FromMilliseconds(1), CancellationToken.None).GetAwaiter().GetResult();
    Equal(ConnectionState.Offline, result.State);
}
static void SettingsRoundTrip()
{
    var folder = Path.Combine(Path.GetTempPath(), "NetSpeedBar.Tests", Guid.NewGuid().ToString("N"));
    var store = new JsonSettingsStore(Path.Combine(folder, "settings.json"));
    var expected = MonitorSettings.Create(Profile.Balanced);
    store.SaveAsync(expected).GetAwaiter().GetResult();
    var actual = store.LoadAsync().GetAwaiter().GetResult();
    Equal(Profile.Balanced, actual.Profile);
    Equal(expected.LightweightBudget.MaxBytes, actual.LightweightBudget.MaxBytes);
}
static void True(bool value) { if (!value) throw new Exception("Expected true."); }
static void False(bool value) { if (value) throw new Exception("Expected false."); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}."); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}."); }
sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(responder(request));
}
