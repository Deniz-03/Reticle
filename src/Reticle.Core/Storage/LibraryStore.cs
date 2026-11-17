using System.Text.Json;
using Reticle.Core.Model;
using Reticle.Core.Serialization;

namespace Reticle.Core.Storage;

/// <summary>One JSON file per crosshair under <c>&lt;root&gt;/crosshairs/&lt;id&gt;.json</c>.</summary>
public sealed class LibraryStore(string rootDir)
{
    private string Dir => Path.Combine(rootDir, "crosshairs");

    /// <summary>Default root: <c>%APPDATA%\Reticle</c>.</summary>
    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Reticle");

    /// <summary>Loads all valid docs ordered by name then id. Unreadable files are skipped and reported via <paramref name="onError"/>; they are left untouched.</summary>
    public IReadOnlyList<CrosshairDoc> LoadAll(Action<string>? onError = null)
    {
        var docs = new List<CrosshairDoc>();
        if (!Directory.Exists(Dir)) return docs;

        foreach (var file in Directory.EnumerateFiles(Dir, "*.json"))
        {
            try
            {
                var doc = Json.Deserialize<CrosshairDoc>(File.ReadAllText(file));
                if (doc.Layers is null || doc.Layers.Any(l => l is null))
                    throw new JsonException("Document has no valid layer list.");
                docs.Add(doc);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException
                                           or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                onError?.Invoke($"Skipped '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        return docs
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Writes the doc as-is (the caller owns <c>UpdatedAt</c>).</summary>
    /// <exception cref="ArgumentException">The id is not usable as a file name.</exception>
    public void Save(CrosshairDoc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        AtomicFile.WriteAllText(PathFor(doc.Id), Json.Serialize(doc));
    }

    /// <summary>Deleting an id that does not exist is a no-op.</summary>
    /// <exception cref="ArgumentException">The id is not usable as a file name.</exception>
    public void Delete(string id) => File.Delete(PathFor(id));

    private string PathFor(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || id.Contains('/') || id.Contains('\\'))
            throw new ArgumentException($"Invalid crosshair id '{id}'.", nameof(id));
        return Path.Combine(Dir, id + ".json");
    }
}
