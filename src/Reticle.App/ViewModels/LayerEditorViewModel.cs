using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

/// <summary>
/// Property panel for the selected layer: properties shared by all layer types (DE-9), outline (DE-10) and
/// animation (AN-1..3). Every setter clamps its input and commits a new layer via <c>with</c>; the designer
/// then calls <see cref="Refresh"/> with the stored layer.
/// </summary>
public abstract class LayerEditorViewModel : ObservableObject
{
    protected LayerEditorViewModel(DesignerViewModel owner, Layer layer)
    {
        Owner = owner;
        Layer = layer;
        Animation = SupportsAnimation ? new AnimationEditorViewModel(this) : null;
    }

    protected DesignerViewModel Owner { get; }

    public Layer Layer { get; private set; }

    /// <summary>AN-1: animations apply to vector layers.</summary>
    public virtual bool SupportsAnimation => true;

    public AnimationEditorViewModel? Animation { get; }

    public static LayerEditorViewModel Create(DesignerViewModel owner, Layer layer) => layer switch
    {
        ClassicLayer c => new ClassicEditorViewModel(owner, c),
        RadialLayer r => new RadialEditorViewModel(owner, r),
        ShapeLayer s => new ShapeEditorViewModel(owner, s),
        ImageLayer i => new ImageEditorViewModel(owner, i),
        _ => throw new NotSupportedException($"Unknown layer type {layer.GetType().Name}."),
    };

    internal void Refresh(Layer layer)
    {
        Layer = layer;
        OnPropertyChanged(string.Empty);
        Animation?.Refresh();
    }

    /// <summary>Stores <paramref name="next"/>; consecutive edits of one property merge into one undo step.</summary>
    protected internal void Commit(Layer next, [CallerMemberName] string property = "")
    {
        if (next.Equals(Layer)) return;
        Owner.ReplaceLayer(next, $"{Layer.Id}.{property}");
    }

    protected static int ClampInt(double value, int min, int max) => (int)Math.Clamp(Math.Round(value), min, max);

    protected static double Clamp(double value, double min, double max) => double.IsNaN(value) ? min : Math.Clamp(value, min, max);

    public string Name
    {
        get => Layer.Name;
        set
        {
            var name = value?.Trim();
            if (string.IsNullOrEmpty(name)) { OnPropertyChanged(); return; }
            Commit(Layer with { Name = name });
        }
    }

    public string ColorHex
    {
        get => ColorText.Format(Layer.Color);
        set
        {
            Commit(Layer with { Color = ColorText.ParseOr(value, Layer.Color) });
            OnPropertyChanged(); // reverts invalid text
        }
    }

    public double Opacity { get => Layer.Opacity; set => Commit(Layer with { Opacity = Clamp(value, 0, 1) }); }
    public double Rotation { get => Layer.Rotation; set => Commit(Layer with { Rotation = Clamp(value, -180, 180) }); }
    public double Scale { get => Layer.Scale; set => Commit(Layer with { Scale = Clamp(value, 0.1, 8) }); }
    public double OffsetX { get => Layer.Offset.X; set => Commit(Layer with { Offset = Layer.Offset with { X = Clamp(value, -500, 500) } }); }
    public double OffsetY { get => Layer.Offset.Y; set => Commit(Layer with { Offset = Layer.Offset with { Y = Clamp(value, -500, 500) } }); }
    public double Blur { get => Layer.Blur; set => Commit(Layer with { Blur = Clamp(value, 0, 20) }); }

    public bool OutlineEnabled
    {
        get => Layer.Outline.Enabled;
        set => Commit(Layer with { Outline = Layer.Outline with { Enabled = value } });
    }

    public double OutlineWidth
    {
        get => Layer.Outline.Width;
        set => Commit(Layer with { Outline = Layer.Outline with { Width = Clamp(value, 0.5, 10) } });
    }

    public string OutlineColorHex
    {
        get => ColorText.Format(Layer.Outline.Color);
        set
        {
            Commit(Layer with { Outline = Layer.Outline with { Color = ColorText.ParseOr(value, Layer.Outline.Color) } });
            OnPropertyChanged();
        }
    }

    public double OutlineOpacity
    {
        get => Layer.Outline.Opacity;
        set => Commit(Layer with { Outline = Layer.Outline with { Opacity = Clamp(value, 0, 1) } });
    }
}

/// <summary>DE-3: four lines with per-line toggles and a center dot.</summary>
public sealed class ClassicEditorViewModel(DesignerViewModel owner, ClassicLayer layer) : LayerEditorViewModel(owner, layer)
{
    private ClassicLayer L => (ClassicLayer)Layer;

    public double Length { get => L.Length; set => Commit(L with { Length = ClampInt(value, 1, 100) }); }
    public double Thickness { get => L.Thickness; set => Commit(L with { Thickness = ClampInt(value, 1, 50) }); }
    public double Gap { get => L.Gap; set => Commit(L with { Gap = ClampInt(value, -20, 100) }); }
    public bool Top { get => L.Top; set => Commit(L with { Top = value }); }
    public bool Bottom { get => L.Bottom; set => Commit(L with { Bottom = value }); }
    public bool Left { get => L.Left; set => Commit(L with { Left = value }); }
    public bool Right { get => L.Right; set => Commit(L with { Right = value }); }
    public bool DotEnabled { get => L.Dot.Enabled; set => Commit(L with { Dot = L.Dot with { Enabled = value } }); }
    public double DotSize { get => L.Dot.Size; set => Commit(L with { Dot = L.Dot with { Size = ClampInt(value, 1, 50) } }); }
    public bool DotRound { get => L.Dot.Round; set => Commit(L with { Dot = L.Dot with { Round = value } }); }
}

/// <summary>DE-4: 2..8 arms.</summary>
public sealed class RadialEditorViewModel(DesignerViewModel owner, RadialLayer layer) : LayerEditorViewModel(owner, layer)
{
    private RadialLayer L => (RadialLayer)Layer;

    public double Arms { get => L.Arms; set => Commit(L with { Arms = ClampInt(value, 2, 8) }); }
    public double StartAngle { get => L.StartAngle; set => Commit(L with { StartAngle = Clamp(value, 0, 360) }); }
    public double Length { get => L.Length; set => Commit(L with { Length = ClampInt(value, 1, 100) }); }
    public double Thickness { get => L.Thickness; set => Commit(L with { Thickness = ClampInt(value, 1, 50) }); }
    public double Gap { get => L.Gap; set => Commit(L with { Gap = ClampInt(value, -20, 100) }); }
}

/// <summary>DE-5: circle, ring, rectangle, triangle, chevron, arc, line.</summary>
public sealed class ShapeEditorViewModel(DesignerViewModel owner, ShapeLayer layer) : LayerEditorViewModel(owner, layer)
{
    public const double MinStroke = 1;
    public const double MaxStroke = 64;

    public static IReadOnlyList<Choice<ShapeKind>> Kinds { get; } = Labels.Of<ShapeKind>(Labels.Shape);

    private ShapeLayer L => (ShapeLayer)Layer;

    public ShapeKind Kind { get => L.Kind; set => Commit(Valid(L with { Kind = value })); }
    public double Width { get => L.Width; set => Commit(L with { Width = Clamp(value, 1, 256) }); }
    public double Height { get => L.Height; set => Commit(Valid(L with { Height = Clamp(value, 1, 256) })); }
    public double StrokeWidth { get => L.StrokeWidth; set => Commit(Valid(L with { StrokeWidth = value })); }
    public double ArcStart { get => L.ArcStart; set => Commit(L with { ArcStart = Clamp(value, -360, 360) }); }
    public double ArcSweep { get => L.ArcSweep; set => Commit(L with { ArcSweep = Clamp(value, -360, 360) }); }

    /// <summary>Stroke-based kinds need a stroke of at least 1; a chevron's stroke cannot exceed its height.</summary>
    public bool HasStroke => L.Kind is ShapeKind.Ring or ShapeKind.Chevron or ShapeKind.Arc or ShapeKind.Line;
    public bool HasHeight => L.Kind is not (ShapeKind.Arc or ShapeKind.Line);
    public bool IsArc => L.Kind == ShapeKind.Arc;

    /// <summary>Clamps the stroke for the shape kind (Ring/Chevron/Arc/Line ≥ 1, Chevron ≤ Height).</summary>
    public static ShapeLayer Valid(ShapeLayer s)
    {
        double max = s.Kind == ShapeKind.Chevron ? Math.Min(MaxStroke, s.Height) : MaxStroke;
        return s with { StrokeWidth = Clamp(s.StrokeWidth, MinStroke, Math.Max(MinStroke, max)) };
    }
}

/// <summary>DE-6: embedded image (PNG/JPG/WebP/GIF).</summary>
public sealed class ImageEditorViewModel : LayerEditorViewModel
{
    public ImageEditorViewModel(DesignerViewModel owner, ImageLayer layer) : base(owner, layer)
    {
        ReplaceImageCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ReplaceImage);
    }

    private ImageLayer L => (ImageLayer)Layer;

    public override bool SupportsAnimation => false;

    public double Width { get => L.Width; set => Commit(L with { Width = Clamp(value, 1, 512) }); }
    public double Height { get => L.Height; set => Commit(L with { Height = Clamp(value, 1, 512) }); }

    public string ImageInfo => $"{L.MimeType} · {L.ImageBase64.Length * 3 / 4 / 1024.0:0.#} KB";

    public CommunityToolkit.Mvvm.Input.IRelayCommand ReplaceImageCommand { get; }

    private void ReplaceImage()
    {
        if (Owner.PickImage() is { } image) Commit(Services.ImageImport.Apply(L, image));
    }
}
