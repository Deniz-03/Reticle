using Reticle.Core.Model;

namespace Reticle.App.Overlay;

public static class OverlayPlacement
{
    /// <summary>Overlay window edge length in device pixels; the center pixel is at (WindowSize/2, WindowSize/2).</summary>
    public const int WindowSize = 768;

    /// <summary>Top-left of the overlay window so its center pixel lands on the monitor's center pixel plus <paramref name="offset"/>.</summary>
    public static PointI WindowTopLeft(MonitorInfo m, Vec2 offset, int windowSize)
    {
        ArgumentNullException.ThrowIfNull(m);
        var b = m.BoundsPx;
        int ox = (int)Math.Round(offset.X, MidpointRounding.AwayFromZero);
        int oy = (int)Math.Round(offset.Y, MidpointRounding.AwayFromZero);
        return new PointI(
            b.Left + b.Width / 2 + ox - windowSize / 2,
            b.Top + b.Height / 2 + oy - windowSize / 2);
    }
}
