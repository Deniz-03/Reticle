using System.IO;
using System.Text.Json;
using Reticle.App.Resources;
using Reticle.Core.Codecs;
using Reticle.Core.Model;
using Reticle.Core.Serialization;

namespace Reticle.App.Services;

/// <summary>Reads and writes single crosshairs as <c>*.reticle.json</c> files (SH-2).</summary>
public static class CrosshairFiles
{
    public const string Extension = ".reticle.json";
    private const long MaxFileBytes = 16 * 1024 * 1024;

    /// <summary>Never throws; the imported doc gets a fresh id and timestamps.</summary>
    public static ImportResult Read(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes) return new ImportResult(null, Strings.ErrorFileTooLarge);
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new ImportResult(null, string.Format(Strings.ErrorFileRead, ex.Message));
        }
    }

    /// <summary>Never throws; the imported doc gets a fresh id and timestamps.</summary>
    public static ImportResult Parse(string json)
    {
        try
        {
            var doc = Json.Deserialize<CrosshairDoc>(json);
            if (doc.Layers is null || doc.Layers.Any(l => l is null) || string.IsNullOrWhiteSpace(doc.Name))
                return new ImportResult(null, Strings.ErrorFileInvalid);
            var now = DateTimeOffset.UtcNow;
            return new ImportResult(doc with { Id = Guid.NewGuid().ToString("N"), CreatedAt = now, UpdatedAt = now }, null);
        }
        catch (NotSupportedException)
        {
            return new ImportResult(null, Strings.ErrorFileVersion);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or FormatException)
        {
            return new ImportResult(null, Strings.ErrorFileInvalid);
        }
    }

    public static void Write(string path, CrosshairDoc doc) => File.WriteAllText(path, Json.Serialize(doc));

    /// <summary>A file name derived from the crosshair name with invalid characters replaced.</summary>
    public static string SuggestFileName(string name, string extension)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return (safe.Length > 0 ? safe : "crosshair") + extension;
    }
}
