using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reticle.Core.Model;

namespace Reticle.App.Overlay;

/// <summary>A decoded image layer: a single frame, or composited animated-GIF frames with their delays.</summary>
public sealed class LayerImage
{
    private readonly double _totalMs;

    public LayerImage(IReadOnlyList<BitmapSource> frames, IReadOnlyList<double> delaysMs)
    {
        Frames = frames;
        DelaysMs = delaysMs;
        _totalMs = delaysMs.Sum();
    }

    public IReadOnlyList<BitmapSource> Frames { get; }
    public IReadOnlyList<double> DelaysMs { get; }
    public bool IsAnimated => Frames.Count > 1 && _totalMs > 0;

    public BitmapSource FrameAt(double nowMs)
    {
        if (!IsAnimated) return Frames[0];
        double t = nowMs % _totalMs;
        for (int i = 0; i < Frames.Count; i++)
        {
            if (t < DelaysMs[i]) return Frames[i];
            t -= DelaysMs[i];
        }
        return Frames[^1];
    }
}

/// <summary>Decodes each image layer once per layer id (re-decodes only when the embedded data changes).</summary>
public sealed class LayerImageCache
{
    private const double DefaultGifDelayMs = 100;
    private readonly Dictionary<string, (string Data, LayerImage? Image)> _cache = new();

    /// <returns>null when the data cannot be decoded.</returns>
    public LayerImage? Get(ImageLayer layer)
    {
        if (_cache.TryGetValue(layer.Id, out var hit) && string.Equals(hit.Data, layer.ImageBase64, StringComparison.Ordinal))
            return hit.Image;
        var image = Decode(layer.ImageBase64);
        _cache[layer.Id] = (layer.ImageBase64, image);
        return image;
    }

    private static LayerImage? Decode(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder is GifBitmapDecoder gif && gif.Frames.Count > 1)
                return DecodeGif(gif);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return new LayerImage([frame], [0]);
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or IOException
                                       or ArgumentException or InvalidOperationException
                                       or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    /// <summary>Composites GIF frames (offsets + disposal) onto the logical screen so each frame is complete.</summary>
    private static LayerImage DecodeGif(GifBitmapDecoder gif)
    {
        int width = gif.Frames.Max(f => f.PixelWidth + (int)Meta(f, "/imgdesc/Left"));
        int height = gif.Frames.Max(f => f.PixelHeight + (int)Meta(f, "/imgdesc/Top"));
        var frames = new List<BitmapSource>(gif.Frames.Count);
        var delays = new List<double>(gif.Frames.Count);
        BitmapSource? previous = null;
        Rect? clearRect = null;

        foreach (var f in gif.Frames)
        {
            var rect = new Rect(Meta(f, "/imgdesc/Left"), Meta(f, "/imgdesc/Top"), f.PixelWidth, f.PixelHeight);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (previous is not null)
                {
                    if (clearRect is { } cr)
                    {
                        dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,
                            new RectangleGeometry(new Rect(0, 0, width, height)), new RectangleGeometry(cr)));
                        dc.DrawImage(previous, new Rect(0, 0, width, height));
                        dc.Pop();
                    }
                    else dc.DrawImage(previous, new Rect(0, 0, width, height));
                }
                dc.DrawImage(f, rect);
            }
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            frames.Add(rtb);

            double delayCs = Meta(f, "/grctlext/Delay");
            delays.Add(delayCs >= 2 ? delayCs * 10 : DefaultGifDelayMs);

            int disposal = (int)Meta(f, "/grctlext/Disposal");
            // 2 = restore to background (clear this frame's area); 3 = restore previous (canvas state before
            // this frame stays the base for the next one); others keep the frame.
            if (disposal == 3) continue;
            previous = rtb;
            clearRect = disposal == 2 ? rect : null;
        }
        return new LayerImage(frames, delays);
    }

    private static double Meta(BitmapFrame frame, string query)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata md && md.ContainsQuery(query))
                return Convert.ToDouble(md.GetQuery(query), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or FormatException or InvalidCastException) { }
        return 0;
    }
}
