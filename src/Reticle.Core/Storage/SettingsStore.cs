using System.Text.Json;
using Reticle.Core.Model;
using Reticle.Core.Serialization;

namespace Reticle.Core.Storage;

/// <summary>Settings in <c>&lt;root&gt;/settings.json</c>.</summary>
public sealed class SettingsStore(string rootDir)
{
    private string FilePath => Path.Combine(rootDir, "settings.json");

    /// <summary>Missing file gives defaults; a corrupt file is renamed to <c>settings.corrupt-&lt;yyyyMMddHHmmss&gt;.json</c> and defaults are returned.</summary>
    public Settings Load()
    {
        if (!File.Exists(FilePath)) return Settings.Default;
        try
        {
            var s = Json.Deserialize<Settings>(File.ReadAllText(FilePath));
            if (s.Hotkeys is null || s.Hotkeys.Any(h => h is null))
                throw new JsonException("Settings have no valid hotkey list.");
            return s;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException
                                       or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            BackUpCorrupt();
            return Settings.Default;
        }
    }

    public void Save(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AtomicFile.WriteAllText(FilePath, Json.Serialize(settings));
    }

    private void BackUpCorrupt()
    {
        try
        {
            var target = Path.Combine(rootDir, $"settings.corrupt-{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Move(FilePath, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* defaults are returned regardless */ }
    }
}
