using System.Windows;
using System.Windows.Media;
using Reticle.App.Rendering;
using Reticle.Core.Geometry;
using Reticle.Core.Model;

namespace Reticle.App.Overlay;

/// <summary>
/// Draws the active crosshair of <see cref="AppState"/> in device pixels around the window's center pixel.
/// One child <see cref="ContainerVisual"/> per visible layer (so a layer can carry its own blur effect), drawn by
/// <see cref="LayerDrawer"/>,
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
    private readonly LayerDrawer _drawer = new();
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
            animatedImage |= _drawer.DrawLayer(VisualAt(count++), layer, render, nowMs);
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
}
