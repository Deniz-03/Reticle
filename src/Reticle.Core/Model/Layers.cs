using System.Text.Json.Serialization;

namespace Reticle.Core.Model;

public enum ShapeKind { Circle, Ring, Rectangle, Triangle, Chevron, Arc, Line }

public sealed record Outline(bool Enabled, double Width, Rgba Color, double Opacity)
{
    /// <summary>Disabled, 1 px, black, fully opaque.</summary>
    public static Outline Default { get; } = new(false, 1, new Rgba(0, 0, 0, 255), 1);
}

public sealed record Dot(bool Enabled = false, int Size = 2, bool Round = false);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ClassicLayer), "classic")]
[JsonDerivedType(typeof(RadialLayer), "radial")]
[JsonDerivedType(typeof(ShapeLayer), "shape")]
[JsonDerivedType(typeof(ImageLayer), "image")]
public abstract record Layer
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Layer";
    public bool Visible { get; init; } = true;
    public Rgba Color { get; init; } = new(255, 255, 255, 255);
    public double Opacity { get; init; } = 1;
    public double Rotation { get; init; }
    public double Scale { get; init; } = 1;
    public Vec2 Offset { get; init; }
    public double Blur { get; init; }
    public Outline Outline { get; init; } = Outline.Default;
    public LayerAnimation? Animation { get; init; }
}

public sealed record ClassicLayer : Layer
{
    public int Length { get; init; } = 6;
    public int Thickness { get; init; } = 2;
    public int Gap { get; init; } = 3;
    public bool Top { get; init; } = true;
    public bool Bottom { get; init; } = true;
    public bool Left { get; init; } = true;
    public bool Right { get; init; } = true;
    public Dot Dot { get; init; } = new();
}

public sealed record RadialLayer : Layer
{
    /// <summary>Number of arms, 2..8.</summary>
    public int Arms { get; init; } = 4;
    public double StartAngle { get; init; }
    public int Length { get; init; } = 6;
    public int Thickness { get; init; } = 2;
    public int Gap { get; init; } = 3;
}

public sealed record ShapeLayer : Layer
{
    public ShapeKind Kind { get; init; } = ShapeKind.Circle;
    public double Width { get; init; } = 8;
    public double Height { get; init; } = 8;
    public double StrokeWidth { get; init; } = 1;
    public double ArcStart { get; init; }
    public double ArcSweep { get; init; } = 90;
}

public sealed record ImageLayer : Layer
{
    public string ImageBase64 { get; init; } = "";
    public string MimeType { get; init; } = "image/png";
    public double Width { get; init; } = 16;
    public double Height { get; init; } = 16;
}
