using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Reticle.App.Rendering;

public enum PreviewBackground { Dark, Light, Checker, Sample }

/// <summary>Brushes behind the designer preview. The sample is an original procedural landscape-like gradient (no images).</summary>
public static class PreviewBackgrounds
{
    private static readonly Lazy<Brush> SampleBrush = new(CreateSample);
    private static readonly Lazy<Brush> CheckerBrush = new(CreateChecker);
    private static readonly Brush DarkBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x1B, 0x1E, 0x22)));
    private static readonly Brush LightBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xE4, 0xE7, 0xEA)));

    public static Brush Get(PreviewBackground kind) => kind switch
    {
        PreviewBackground.Light => LightBrush,
        PreviewBackground.Checker => CheckerBrush.Value,
        PreviewBackground.Sample => SampleBrush.Value,
        _ => DarkBrush,
    };

    private static Brush Frozen(Brush b)
    {
        b.Freeze();
        return b;
    }

    private static Brush CreateChecker()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x5A, 0x5F, 0x66)), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        var dark = new GeometryGroup();
        dark.Children.Add(new RectangleGeometry(new Rect(0, 0, 8, 8)));
        dark.Children.Add(new RectangleGeometry(new Rect(8, 8, 8, 8)));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x3C, 0x40, 0x46)), null, dark));
        return Frozen(new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 16, 16),
            ViewportUnits = BrushMappingMode.Absolute,
        });
    }

    /// <summary>Hazy sky gradient over two rolling value-noise ridges with fine grain; deterministic.</summary>
    private static Brush CreateSample()
    {
        const int w = 480, h = 270;
        var pixels = new byte[w * h * 4];
        var rng = new Random(1337);
        var noise = new double[64];
        for (int i = 0; i < noise.Length; i++) noise[i] = rng.NextDouble();

        double Ridge(double x, double freq, double phase)
        {
            double t = x * freq + phase;
            int i0 = (int)Math.Floor(t);
            double f = t - i0;
            f = f * f * (3 - 2 * f);
            double a = noise[((i0 % 64) + 64) % 64], b = noise[(((i0 + 1) % 64) + 64) % 64];
            return a + (b - a) * f;
        }

        for (int y = 0; y < h; y++)
        {
            double v = y / (double)h;
            for (int x = 0; x < w; x++)
            {
                double u = x / (double)w;
                // Sky: cool slate at the top fading to a warm haze at the horizon.
                double r = 70 + 120 * v, g = 92 + 92 * v, bl = 118 + 52 * v;
                double far = 0.52 + 0.10 * Ridge(u, 6, 3.1) + 0.04 * Ridge(u, 23, 7.7);
                double near = 0.66 + 0.12 * Ridge(u, 3.5, 11.2) + 0.03 * Ridge(u, 31, 1.9);
                if (v > far) { r = 92 - 20 * (v - far); g = 98 - 18 * (v - far); bl = 104 - 10 * (v - far); }
                if (v > near) { r = 74 - 30 * (v - near); g = 66 - 20 * (v - near); bl = 52 - 12 * (v - near); }
                double grain = (rng.NextDouble() - 0.5) * 10;
                int o = (y * w + x) * 4;
                pixels[o] = Clamp(bl + grain);
                pixels[o + 1] = Clamp(g + grain);
                pixels[o + 2] = Clamp(r + grain);
                pixels[o + 3] = 255;
            }
        }
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        bitmap.Freeze();
        var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality); // smooth upscaling of the small procedural image
        return Frozen(brush);
    }

    private static byte Clamp(double v) => (byte)Math.Clamp((int)Math.Round(v), 0, 255);
}
