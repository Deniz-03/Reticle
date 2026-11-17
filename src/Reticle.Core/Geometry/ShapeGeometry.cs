using Reticle.Core.Model;

namespace Reticle.Core.Geometry;

public static class ShapeGeometry
{
    private const double C = 0.5;
    private static readonly Vec2 Pivot = new(C, C);

    public static LayerRender Build(ShapeLayer layer, double sizeFactor, AnimatedProps? anim = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var common = LayerGeometry.Common.From(layer, sizeFactor, anim ?? AnimatedProps.Identity);
        if (!layer.Visible)
            return common.Render(layer, [], Pivot);

        double w = layer.Width * sizeFactor;
        double h = layer.Height * sizeFactor;
        double sw = layer.StrokeWidth > 0 ? Math.Max(1, layer.StrokeWidth * sizeFactor) : 0;
        var (fill, oc, ow) = (common.Fill, common.OutlineColor, common.OutlineWidth);
        double left = C - w / 2, right = C + w / 2, top = C - h / 2, bottom = C + h / 2;

        Prim prim = layer.Kind switch
        {
            ShapeKind.Circle => new EllipsePrim(C, C, w / 2, h / 2, 0, fill, oc, ow),
            ShapeKind.Ring => new EllipsePrim(C, C, w / 2, h / 2, sw, fill, oc, ow),
            ShapeKind.Rectangle => new RectPrim(left, top, w, h, fill, oc, ow),
            ShapeKind.Triangle => new PolygonPrim(
                [new Vec2(C, top), new Vec2(right, bottom), new Vec2(left, bottom)], fill, oc, ow),
            // "^" band of vertical thickness sw inside the Width x Height bounding box.
            ShapeKind.Chevron => new PolygonPrim(
                [
                    new Vec2(C, top), new Vec2(right, bottom - sw), new Vec2(right, bottom),
                    new Vec2(C, top + sw), new Vec2(left, bottom), new Vec2(left, bottom - sw),
                ], fill, oc, ow),
            ShapeKind.Arc => new ArcPrim(C, C, w / 2, layer.ArcStart, layer.ArcSweep, sw, fill, oc, ow),
            ShapeKind.Line => new RectPrim(left, C - sw / 2, w, sw, fill, oc, ow),
            _ => throw new NotSupportedException($"Unknown shape kind {layer.Kind}."),
        };
        return common.Render(layer, [prim], Pivot);
    }

    public static LayerRender BuildImage(ImageLayer layer, double sizeFactor, AnimatedProps? anim = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var common = LayerGeometry.Common.From(layer, sizeFactor, anim ?? AnimatedProps.Identity);
        double w = layer.Width * sizeFactor;
        double h = layer.Height * sizeFactor;
        var prim = new ImagePrim(layer.Id, C - w / 2, C - h / 2, w, h, common.Fill, common.OutlineColor, common.OutlineWidth);
        return common.Render(layer, [prim], Pivot);
    }
}
