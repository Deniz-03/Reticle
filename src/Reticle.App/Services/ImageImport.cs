using System.IO;
using System.Windows.Media.Imaging;
using Reticle.App.Resources;
using Reticle.Core.Model;

namespace Reticle.App.Services;

/// <summary>Loads PNG/JPG/WebP/GIF files for image layers (DE-6): embedded as base64, at most 2 MB.</summary>
public static class ImageImport
{
    public const long MaxBytes = 2 * 1024 * 1024;
    public const double MaxDefaultSize = 128;

    public const string FileFilterPattern = "*.png;*.jpg;*.jpeg;*.webp;*.gif";

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
    };

    public sealed record Result(string? Base64, string? MimeType, double Width, double Height, string? Error);

    /// <summary>Never throws. Width/Height are the pixel size scaled down (aspect kept) to fit 128 px.</summary>
    public static Result Load(string path)
    {
        if (!MimeTypes.TryGetValue(Path.GetExtension(path), out var mime))
            return Fail(Strings.ErrorImageFormat);
        try
        {
            if (new FileInfo(path).Length > MaxBytes) return Fail(Strings.ErrorImageTooLarge);
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > MaxBytes) return Fail(Strings.ErrorImageTooLarge);

            using var stream = new MemoryStream(bytes);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var (w, h) = FitSize(frame.PixelWidth, frame.PixelHeight);
            return new Result(Convert.ToBase64String(bytes), mime, w, h, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or FileFormatException or ArgumentException or InvalidOperationException
                                       or System.Runtime.InteropServices.COMException)
        {
            return Fail(Strings.ErrorImageRead);
        }
    }

    /// <summary>Pixel size capped at <see cref="MaxDefaultSize"/> on the longer side, aspect kept, at least 1.</summary>
    public static (double Width, double Height) FitSize(int pixelWidth, int pixelHeight)
    {
        double w = Math.Max(1, pixelWidth), h = Math.Max(1, pixelHeight);
        double scale = Math.Min(1, MaxDefaultSize / Math.Max(w, h));
        return (Math.Max(1, Math.Round(w * scale)), Math.Max(1, Math.Round(h * scale)));
    }

    /// <summary>Applies a loaded image to a layer (keeps all other properties).</summary>
    public static ImageLayer Apply(ImageLayer layer, Result image)
        => layer with { ImageBase64 = image.Base64 ?? "", MimeType = image.MimeType ?? layer.MimeType, Width = image.Width, Height = image.Height };

    private static Result Fail(string error) => new(null, null, 0, 0, error);
}
