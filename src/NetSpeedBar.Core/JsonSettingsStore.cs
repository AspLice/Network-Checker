using System.Text.Json;

namespace NetSpeedBar.Core;

public sealed class JsonSettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public async Task<MonitorSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return MonitorSettings.Create();
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<MonitorSettings>(stream, Options, cancellationToken).ConfigureAwait(false) ?? MonitorSettings.Create();
    }
    public async Task SaveAsync(MonitorSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken).ConfigureAwait(false);
    }
}
