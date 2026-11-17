using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Reticle.Core.Geometry;
using Reticle.Core.Model;

namespace Reticle.App.Overlay;

/// <summary>
/// Draws the active crosshair of <see cref="AppState"/> in device pixels around the window's center pixel.
/// One child <see cref="ContainerVisual"/> per visible layer (so a layer can carry its own <see cref="BlurEffect"/>)
/// under a root visual whose transform is translate(center) then scale(1/DpiScale), making 1 unit = 1 device pixel
/// with the origin at the top-left corner of the center pixel.
/// Redraws on <see cref="AppState.Changed"/>; hooks <see cref="CompositionTarget.Rendering"/> only while an
/// animation runs or an animated GIF is visible (NF-1).
/// </summary>
public sealed class OverlayRenderer : FrameworkElement
{
    private readonly AppState _state;
    private readonly ContainerVisual _root = new();
    private readonly List<ContainerVisual> _layerVisuals = new();
    private readonly LayerImageCache _images = new();
    private readonly Dictionary<Rgba, SolidColorBrush> _brushes = new();
    private MonitorInfo? _monitor;
    private bool _loopActive;

    public OverlayRenderer(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        IsHitTestVisible = false;
        AddVisualChild(_root);
        _state.Changed += Redraw;
    }

    /// <summary>Center pixel of the drawing surface, in device pixels from its top-left.</summary>
    public int CenterPx { get; init; } = OverlayPlacement.WindowSize / 2;

    /// <summary>Monitor the overlay sits on; determines the size factor. Nothing is drawn while null.</summary>
    public MonitorInfo? Monitor
    {
        get => _monitor;
        set
        {
            if (_monitor == value) return;
            _monitor = value;
            Redraw();
        }
    }

    /// <summary>Renderer size factor: GlobalScale x (ReferenceHeight is int h ? monitor height / h : 1).</summary>
    public static double SizeFactor(Settings settings, CrosshairDoc doc, MonitorInfo monitor)
        => settings.GlobalScale * (doc.ReferenceHeight is int h && h > 0 ? monitor.BoundsPx.Height / (double)h : 1);

    /// <summary>Stops listening to state changes and frame callbacks.</summary>
    public void Detach()
    {
        _state.Changed -= Redraw;
        SetLoop(false);
    }

    /// <summary>Redraws now and starts or stops the per-frame loop as needed.</summary>
    public void Redraw()
    {
        bool needsLoop = Draw(_state.NowMs);
        SetLoop(needsLoop);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index)
        => index == 0 ? _root : throw new ArgumentOutOfRangeException(nameof(index));

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Redraw();
    }

    private void SetLoop(bool on)
    {
        if (on == _loopActive) return;
        if (on) CompositionTarget.Rendering += OnRendering;
        else CompositionTarget.Rendering -= OnRendering;
        _loopActive = on;
    }

    private void OnRendering(object? sender, EventArgs e) => Redraw();

    /// <returns>True when another frame is needed (animation running or animated GIF visible).</returns>
    private bool Draw(double nowMs)
    {
        var settings = _state.Settings;
        var doc = _state.Active;
        if (!settings.OverlayEnabled || _state.Hidden || doc is null || _monitor is null)
        {
            TrimVisuals(0);
            return false;
        }

        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var root = Matrix.Identity;
        root.Translate(CenterPx, CenterPx);
        root.Scale(1 / dpi, 1 / dpi);
        _root.Transform = new MatrixTransform(root);

        double sizeFactor = SizeFactor(settings, doc, _monitor);
        int count = 0;
        bool animatedImage = false;
        foreach (var layer in doc.Layers)
        {
            if (!layer.Visible) continue;
            var anim = _state.Animations.Evaluate(layer, nowMs);
            var render = LayerGeometry.Build(layer, sizeFactor, anim);
            animatedImage |= DrawLayer(VisualAt(count++), layer, render, nowMs);
        }
        TrimVisuals(count);
        return animatedImage || _state.Animations.IsAnimating(nowMs);
    }

    private ContainerVisual VisualAt(int index)
    {
        if (index < _layerVisuals.Count) return _layerVisuals[index];
        var cv = new ContainerVisual();
        _layerVisuals.Add(cv);
        _root.Children.Add(cv);
        return cv;
    }

    private void TrimVisuals(int count)
    {
        for (int i = _layerVisuals.Count - 1; i >= count; i--)
        {
            _root.Children.Remove(_layerVisuals[i]);
            _layerVisuals.RemoveAt(i);
        }
    }

    /// <summary>
    /// Draws one layer into its container. Draw order is all outlines, then all fills; consecutive operations
    /// with the same edge mode share one child <see cref="DrawingVisual"/> (Ruling R6: Rect/Polygon prims are
    /// aliased when rotation is a multiple of 90 degrees and scale is 1; ellipses, arcs and images are always
    /// anti-aliased). Opacity and blur apply to the whole layer via the container.
    /// </summary>
    /// <returns>True when the layer shows an animated GIF.</returns>
    private bool DrawLayer(ContainerVisual layerVisual, Layer layer, LayerRender r, double nowMs)
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
