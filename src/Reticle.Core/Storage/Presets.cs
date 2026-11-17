using System.Reflection;
using Reticle.Core.Model;
using Reticle.Core.Serialization;

namespace Reticle.Core.Storage;

/// <summary>Original starter crosshairs shipped as embedded JSON resources.</summary>
public static class Presets
{
    private static readonly Lazy<IReadOnlyList<CrosshairDoc>> Docs = new(Load);

    public static IReadOnlyList<CrosshairDoc> All => Docs.Value;

    /// <summary>Saves every preset when the library holds no valid docs.</summary>
    public static void SeedIfEmpty(LibraryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (store.LoadAll().Count > 0) return;
        foreach (var doc in All) store.Save(doc);
    }

    private static IReadOnlyList<CrosshairDoc> Load()
    {
        var asm = typeof(Presets).Assembly;
        return asm.GetManifestResourceNames()
            .Where(n => n.Contains(".Storage.Presets.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(n =>
            {
                using var stream = asm.GetManifestResourceStream(n)!;
                using var reader = new StreamReader(stream);
                return Json.Deserialize<CrosshairDoc>(reader.ReadToEnd());
            })
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
