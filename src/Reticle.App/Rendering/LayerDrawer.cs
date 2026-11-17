using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Reticle.App.Overlay;
using Reticle.Core.Geometry;
using Reticle.Core.Model;

namespace Reticle.App.Rendering;

/// <summary>
/// Draws built layer geometry (<see cref="LayerRender"/>) into WPF visuals in device-pixel units.
/// Shared by the overlay and by off-screen renders (library thumbnails, designer preview, PNG export).
/// Caches brushes and decoded images; use from one thread.
/// </summary>
public sealed class LayerDrawer
{
    private readonly LayerImageCache _images = new();
    private readonly Dictionary<Rgba, SolidColorBrush> _brushes = new();

    /// <summary>
    /// Draws one layer into its container. Draw order is all outlines, then all fills; consecutive operations
    /// with the same edge mode share one child <see cref="DrawingVisual"/> (Ruling R6: Rect/Polygon prims are
    /// aliased when rotation is a multiple of 90 degrees and scale is 1; ellipses, arcs and images are always
    /// anti-aliased). Opacity and blur apply to the whole layer via the container.
    /// </summary>
    /// <returns>True when the layer shows an animated GIF.</returns>
    public bool DrawLayer(ContainerVisual layerVisual, Layer layer, LayerRender r, double nowMs)
    {
        if (r.Blur > 0)
        {
            if (layerVisual.Effect is BlurEffect blur) blur.Radius = r.Blur;
            else layerVisual.Effect = new BlurEffect { Radius = r.Blur, KernelType = KernelType.Gaussian };
        }
        else layerVisual.Effect = null;
        layerVisual.Opacity = Math.Clamp(r.Opacity, 0, 1);

        bool sharpLayer = r.Scale == 1 && Math.Abs(Math.IEEERemainder(r.Rotation, 90)) < 1e-9;
        var m = Matrix.Identity;
        m.ScaleAt(r.Scale, r.Scale, r.Pivot.X, r.Pivot.Y);
        m.RotateAt(r.Rotation, r.Pivot.X, r.Pivot.Y);
        m.Translate(r.Offset.X, r.Offset.Y);
        var transform = new MatrixTransform(m);
        transform.Freeze();

        int runs = 0;
        bool? runAliased = null;
        DrawingContext? dc = null;
        DrawingContext Ctx(Prim p)
        {
            bool aliased = sharpLayer && p is RectPrim or PolygonPrim;
            if (dc is not null && runAliased == aliased) return dc;
            dc?.Close();
            DrawingVisual dv;
            if (runs < layerVisual.Children.Count) dv = (DrawingVisual)layerVisual.Children[runs];
            else layerVisual.Children.Add(dv = new DrawingVisual());
            runs++;
            RenderOptions.SetEdgeMode(dv, aliased ? EdgeMode.Aliased : EdgeMode.Unspecified);
            dc = dv.RenderOpen();
            dc.PushTransform(transform);
            runAliased = aliased;
            return dc;
        }

        bool animated = false;
        try
        {
            foreach (var item in r.Items)
                if (item.OutlineColor is not null && item.OutlineWidth > 0) DrawOutline(Ctx(item), item);
            foreach (var item in r.Items)
            {
                if (item is ImagePrim img)
                {
                    if (layer is ImageLayer il && _images.Get(il) is { } image)
                    {
                        Ctx(item).DrawImage(image.FrameAt(nowMs), new Rect(img.X, img.Y, Math.Max(0, img.W), Math.Max(0, img.H)));
                        animated |= image.IsAnimated;
                    }
                }
                else DrawFill(Ctx(item), item);
            }
        }
        finally
        {
            dc?.Close();
        }
        while (layerVisual.Children.Count > runs) layerVisual.Children.RemoveAt(layerVisual.Children.Count - 1);
        return animated;
    }

    private void DrawOutline(DrawingContext dc, Prim p)
    {
        if (p.OutlineColor is not { } oc || p.OutlineWidth <= 0) return;
        var brush = Brush(oc);
        double ow = p.OutlineWidth;
        switch (p)
        {
            case RectPrim r:
                dc.DrawRectangle(null, MakePen(brush, 2 * ow), RectOf(r.X, r.Y, r.W, r.H));
                break;
            case EllipsePrim { StrokeWidth: <= 0 } e:
                dc.DrawEllipse(null, MakePen(brush, 2 * ow), new Point(e.Cx, e.Cy), e.Rx, e.Ry);
                break;
            case EllipsePrim e:
                dc.DrawEllipse(null, MakePen(brush, e.StrokeWidth + 2 * ow), new Point(e.Cx, e.Cy),
                    RingRadius(e.Rx, e.StrokeWidth), RingRadius(e.Ry, e.StrokeWidth));
                break;
            case PolygonPrim poly:
                dc.DrawGeometry(null, MakePen(brush, 2 * ow), PolygonGeometry(poly.Points));
                break;
            case ArcPrim a:
                double mid = RingRadius(a.R, a.StrokeWidth);
                double extendDeg = mid > 0 ? ow / mid * 180 / Math.PI : 0;
                double sweep = Math.Sign(a.SweepDeg) * (Math.Abs(a.SweepDeg) + 2 * extendDeg);
                double start = a.StartDeg - Math.Sign(a.SweepDeg) * extendDeg;
                DrawArc(dc, MakePen(brush, a.StrokeWidth + 2 * ow), a.Cx, a.Cy, mid, start, sweep);
                break;
            case ImagePrim i:
                dc.DrawRectangle(null, MakePen(brush, 2 * ow), RectOf(i.X, i.Y, i.W, i.H));
                break;
        }
    }

    private void DrawFill(DrawingContext dc, Prim p)
    {
        var brush = Brush(p.Fill);
        switch (p)
        {
            case RectPrim r:
                dc.DrawRectangle(brush, null, RectOf(r.X, r.Y, r.W, r.H));
                break;
            case EllipsePrim { StrokeWidth: <= 0 } e:
                dc.DrawEllipse(brush, null, new Point(e.Cx, e.Cy), e.Rx, e.Ry);
                break;
            case EllipsePrim e:
                dc.DrawEllipse(null, MakePen(brush, e.StrokeWidth), new Point(e.Cx, e.Cy),
                    RingRadius(e.Rx, e.StrokeWidth), RingRadius(e.Ry, e.StrokeWidth));
                break;
            case PolygonPrim poly:
                dc.DrawGeometry(brush, null, PolygonGeometry(poly.Points));
                break;
            case ArcPrim a:
                DrawArc(dc, MakePen(brush, a.StrokeWidth), a.Cx, a.Cy, RingRadius(a.R, a.StrokeWidth), a.StartDeg, a.SweepDeg);
                break;
        }
    }

    /// <summary>Rings and arcs: the radius is the outer edge, so the stroke centerline sits half a stroke inside.</summary>
    private static double RingRadius(double outer, double stroke) => Math.Max(0, outer - stroke / 2);

    private static Rect RectOf(double x, double y, double w, double h) => new(x, y, Math.Max(0, w), Math.Max(0, h));

    /// <summary>Angles in degrees, 0 = +X (right), positive = clockwise on screen (y points down).</summary>
    private static void DrawArc(DrawingContext dc, Pen pen, double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        if (r <= 0 || sweepDeg == 0) return;
        if (Math.Abs(sweepDeg) >= 360)
        {
            dc.DrawEllipse(null, pen, new Point(cx, cy), r, r);
            return;
        }
        static Point At(double cx, double cy, double r, double deg)
        {
            double rad = deg * Math.PI / 180;
            return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
        }
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(At(cx, cy, r, startDeg), isFilled: false, isClosed: false);
            ctx.ArcTo(At(cx, cy, r, startDeg + sweepDeg), new Size(r, r), 0, Math.Abs(sweepDeg) > 180,
                sweepDeg > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static StreamGeometry PolygonGeometry(IReadOnlyList<Vec2> points)
    {
        var geo = new StreamGeometry();
        if (points.Count > 0)
        {
            using var ctx = geo.Open();
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: true, isClosed: true);
            for (int i = 1; i < points.Count; i++)
                ctx.LineTo(new Point(points[i].X, points[i].Y), isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();
        return geo;
    }

    private static Pen MakePen(Brush brush, double width)
    {
        var pen = new Pen(brush, width) { LineJoin = PenLineJoin.Miter, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        pen.Freeze();
        return pen;
    }

    private SolidColorBrush Brush(Rgba c)
    {
        if (_brushes.TryGetValue(c, out var b)) return b;
        b = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        b.Freeze();
        if (_brushes.Count > 256) _brushes.Clear(); // animated colors produce many values
        _brushes[c] = b;
        return b;
    }
}
