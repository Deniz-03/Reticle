namespace Reticle.Core.Storage;

internal static class AtomicFile
{
    /// <summary>Writes to <c>path.tmp</c>, then moves it over <paramref name="path"/>.</summary>
    public static void WriteAllText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
