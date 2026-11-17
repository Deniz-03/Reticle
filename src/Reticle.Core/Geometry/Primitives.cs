using Reticle.Core.Model;

namespace Reticle.Core.Geometry;

/// <summary>
/// Device-pixel render primitive. Renderer contract: draw outlines of all items first
/// (stroke of width 2*OutlineWidth centered on the shape edge, under the fill, so it shows
/// OutlineWidth outside), then fills.
/// </summary>
public abstract record Prim(Rgba Fill, Rgba? OutlineColor, double OutlineWidth);

public sealed record RectPrim(double X, double Y, double W, double H, Rgba Fill, Rgba? OutlineColor, double OutlineWidth)
    : Prim(Fill, OutlineColor, OutlineWidth);

/// <summary>StrokeWidth 0 = filled.</summary>
public sealed record EllipsePrim(double Cx, double Cy, double Rx, double Ry, double StrokeWidth, Rgba Fill, Rgba? OutlineColor, double OutlineWidth)
    : Prim(Fill, OutlineColor, OutlineWidth);

public sealed record PolygonPrim(IReadOnlyList<Vec2> Points, Rgba Fill, Rgba? OutlineColor, double OutlineWidth)
    : Prim(Fill, OutlineColor, OutlineWidth);

public sealed record ArcPrim(double Cx, double Cy, double R, double StartDeg, double SweepDeg, double StrokeWidth, Rgba Fill, Rgba? OutlineColor, double OutlineWidth)
    : Prim(Fill, OutlineColor, OutlineWidth);

public sealed record ImagePrim(string LayerId, double X, double Y, double W, double H, Rgba Fill, Rgba? OutlineColor, double OutlineWidth)
    : Prim(Fill, OutlineColor, OutlineWidth);

/// <summary>Primitives of one layer plus the transform: Scale and Rotation around Pivot, then Offset.</summary>
public sealed record LayerRender(
    string LayerId,
    IReadOnlyList<Prim> Items,
    double Opacity,
    double Blur,
    double Rotation,
    double Scale,
    Vec2 Offset,
    Vec2 Pivot);
