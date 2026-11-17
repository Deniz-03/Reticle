using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reticle.App.Rendering;
using Reticle.Core.Model;

namespace Reticle.App.Tests;

public sealed class CrosshairRasterizerTests
{
    private static T OnSta<T>(Func<T> work)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new Xunit.Sdk.XunitException("STA work failed: " + error);
        return result;
    }

    [Fact]
    public void PngExport_Is256Transparent_WithCrosshairCentered()
    {
        // 1 px dot at the center pixel, reference-height doc: 1080 nominal height => size factor 1.
        var doc = new CrosshairDoc(1, "d", "Dot", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1080,
            [new ClassicLayer { Color = new Rgba(255, 0, 0, 255), Top = false, Bottom = false, Left = false, Right = false, Dot = new Dot(true, 1) }]);

        var pixels = OnSta(() =>
        {
            var bitmap = CrosshairRasterizer.Render(doc, 256, 256, CrosshairRasterizer.NominalSizeFactor(doc), new LayerDrawer());
            var png = CrosshairRasterizer.EncodePng(bitmap);
            var decoded = BitmapDecoder.Create(new MemoryStream(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal((256, 256), (decoded.PixelWidth, decoded.PixelHeight));
            var bgra = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0);
            var data = new byte[256 * 256 * 4];
            bgra.CopyPixels(data, 256 * 4, 0);
            return data;
        });

        int At(int x, int y, int channel) => pixels[(y * 256 + x) * 4 + channel];
        Assert.Equal(0, At(0, 0, 3)); // transparent background
        Assert.Equal((255, 255), (At(128, 128, 2), At(128, 128, 3))); // red, opaque center pixel
        Assert.Equal(0, At(127, 128, 3));
        Assert.Equal(0, At(128, 127, 3));
        Assert.Single(Enumerable.Range(0, 256 * 256), i => pixels[i * 4 + 3] > 0); // nothing else drawn
    }
}
