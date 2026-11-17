using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reticle.Core.Geometry;
using Reticle.Core.Model;

namespace Reticle.App.Rendering;

/// <summary>Renders a crosshair doc (static, no animation) into a transparent bitmap, 1 unit = 1 bitmap pixel.</summary>
public static class CrosshairRasterizer
{
    /// <summary>Monitor height assumed for docs with a reference height when no real monitor applies (thumbnails, PNG export).</summary>
    public const int NominalMonitorHeight = 1080;

    /// <summary>Size factor for off-screen renders: reference-height docs are scaled to a 1080 px monitor.</summary>
    public static double NominalSizeFactor(CrosshairDoc doc)
        => doc.ReferenceHeight is int h && h > 0 ? NominalMonitorHeight / (double)h : 1;

    /// <summary>
    /// Draws all visible layers with the crosshair's center pixel at (<paramref name="widthPx"/>/2, <paramref name="heightPx"/>/2).
    /// The result is frozen.
    /// </summary>
    public static BitmapSource Render(CrosshairDoc doc, int widthPx, int heightPx, double sizeFactor, LayerDrawer drawer)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(drawer);
        widthPx = Math.Max(1, widthPx);
        heightPx = Math.Max(1, heightPx);

        var root = new ContainerVisual { Transform = new TranslateTransform(widthPx / 2, heightPx / 2) };
        foreach (var layer in doc.Layers)
        {
            if (!layer.Visible) continue;
            var visual = new ContainerVisual();
            root.Children.Add(visual);
            drawer.DrawLayer(visual, layer, LayerGeometry.Build(layer, sizeFactor, AnimatedProps.Identity), nowMs: 0);
        }

        var bitmap = new RenderTargetBitmap(widthPx, heightPx, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Encodes a bitmap as PNG.</summary>
    public static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new System.IO.MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
