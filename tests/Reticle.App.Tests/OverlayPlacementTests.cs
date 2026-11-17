using Reticle.App.Overlay;
using Reticle.Core.Model;

namespace Reticle.App.Tests;

public sealed class OverlayPlacementTests
{
    private static MonitorInfo Monitor(int left, int top, int width, int height)
        => new("\\\\.\\DISPLAY1", new RectI(left, top, width, height), 1.0, true);

    [Fact]
    public void OverlayPlacement_EvenResolution()
    {
        var p = OverlayPlacement.WindowTopLeft(Monitor(0, 0, 1920, 1080), new Vec2(0, 0), 768);
        Assert.Equal(new PointI(576, 156), p);
    }

    [Fact]
    public void OverlayPlacement_OddResolution()
    {
        var p = OverlayPlacement.WindowTopLeft(Monitor(0, 0, 1366, 767), new Vec2(0, 0), 768);
        Assert.Equal(new PointI(299, -1), p);
    }

    [Fact]
    public void OverlayPlacement_SecondMonitor_Offset()
    {
        var p = OverlayPlacement.WindowTopLeft(Monitor(1920, 0, 2560, 1440), new Vec2(3, -2), 768);
        Assert.Equal(new PointI(2819, 334), p);
    }

    [Fact]
    public void OverlayPlacement_WindowSizeConstant()
    {
        Assert.Equal(768, OverlayPlacement.WindowSize);
    }
}
