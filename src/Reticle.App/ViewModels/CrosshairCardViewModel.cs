using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Reticle.App.Rendering;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

/// <summary>A library card: crosshair, lazily rendered thumbnail and active marker (LB-2, LB-4).</summary>
public sealed partial class CrosshairCardViewModel : ObservableObject
{
    public const int ThumbnailPx = 96;

    private readonly LayerDrawer _drawer;
    private BitmapSource? _thumbnail;

    public CrosshairCardViewModel(CrosshairDoc doc, LayerDrawer drawer)
    {
        Doc = doc;
        _drawer = drawer;
    }

    public CrosshairDoc Doc { get; private set; }
    public string Id => Doc.Id;
    public string Name => Doc.Name;

    [ObservableProperty]
    private bool _isActive;

    public BitmapSource Thumbnail => _thumbnail ??= CrosshairRasterizer.Render(
        Doc, ThumbnailPx, ThumbnailPx, CrosshairRasterizer.NominalSizeFactor(Doc), _drawer);

    internal void Update(CrosshairDoc doc)
    {
        if (ReferenceEquals(doc, Doc)) return;
        bool redraw = !doc.Layers.SequenceEqual(Doc.Layers) || doc.ReferenceHeight != Doc.ReferenceHeight;
        Doc = doc;
        if (redraw) _thumbnail = null;
        OnPropertyChanged(string.Empty);
    }
}
