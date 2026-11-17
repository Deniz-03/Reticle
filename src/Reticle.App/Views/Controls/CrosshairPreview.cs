using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Reticle.App.Rendering;
using Reticle.Core.Model;

namespace Reticle.App.Views.Controls;

/// <summary>
/// Designer preview (DE-1): the crosshair rendered in device pixels like the overlay, magnified 1x-8x with
/// nearest-neighbour scaling so single pixels stay crisp, centered over a selectable backdrop.
/// </summary>
public sealed class CrosshairPreview : FrameworkElement
{
    public static readonly DependencyProperty DocProperty = DependencyProperty.Register(
        nameof(Doc), typeof(CrosshairDoc), typeof(CrosshairPreview), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(int), typeof(CrosshairPreview), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SizeFactorProperty = DependencyProperty.Register(
        nameof(SizeFactor), typeof(double), typeof(CrosshairPreview), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackdropProperty = DependencyProperty.Register(
        nameof(Backdrop), typeof(PreviewBackground), typeof(CrosshairPreview),
        new FrameworkPropertyMetadata(PreviewBackground.Dark, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly LayerDrawer _drawer = new();
    private readonly DrawingVisual _crosshair = new();
    private (CrosshairDoc Doc, int W, int H, double Factor)? _cacheKey;
    private BitmapSource? _cache;

    public CrosshairPreview()
    {
        ClipToBounds = true;
        // Only the magnified crosshair is scaled nearest-neighbour; the backdrop stays smooth.
        RenderOptions.SetBitmapScalingMode(_crosshair, BitmapScalingMode.NearestNeighbor);
        AddVisualChild(_crosshair);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index)
        => index == 0 ? _crosshair : throw new ArgumentOutOfRangeException(nameof(index));

    public CrosshairDoc? Doc { get => (CrosshairDoc?)GetValue(DocProperty); set => SetValue(DocProperty, value); }
    public int Zoom { get => (int)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public double SizeFactor { get => (double)GetValue(SizeFactorProperty); set => SetValue(SizeFactorProperty, value); }
    public PreviewBackground Backdrop { get => (PreviewBackground)GetValue(BackdropProperty); set => SetValue(BackdropProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(RenderSize);
        dc.DrawRectangle(PreviewBackgrounds.Get(Backdrop), null, bounds);
        using var cdc = _crosshair.RenderOpen();
        if (Doc is not { } doc || bounds.Width < 1 || bounds.Height < 1) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        double devW = bounds.Width * dpi.DpiScaleX, devH = bounds.Height * dpi.DpiScaleY;
        int zoom = Math.Clamp(Zoom, 1, 8);
        int w = (int)Math.Ceiling(devW / zoom) + 2, h = (int)Math.Ceiling(devH / zoom) + 2;
        var key = (doc, w, h, SizeFactor);
        if (_cache is null || _cacheKey != key)
        {
            _cache = CrosshairRasterizer.Render(doc, w, h, SizeFactor, _drawer);
            _cacheKey = key;
        }

        // Put the bitmap's center pixel on the element's center device pixel.
        double left = Math.Floor(devW / 2) - (w / 2) * zoom;
        double top = Math.Floor(devH / 2) - (h / 2) * zoom;
        cdc.DrawImage(_cache, new Rect(left / dpi.DpiScaleX, top / dpi.DpiScaleY, w * zoom / dpi.DpiScaleX, h * zoom / dpi.DpiScaleY));
    }
}
