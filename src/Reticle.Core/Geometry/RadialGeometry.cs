using Reticle.Core.Model;

namespace Reticle.Core.Geometry;

public static class RadialGeometry
{
    private static readonly Vec2 Pivot = new(0.5, 0.5);

    public static LayerRender Build(RadialLayer layer, double sizeFactor, AnimatedProps? anim = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        anim ??= AnimatedProps.Identity;
        var common = LayerGeometry.Common.From(layer, sizeFactor, anim);
        if (!layer.Visible)
            return common.Render(layer, [], Pivot);

        int arms = Math.Clamp(layer.Arms, 2, 8);
        int t = LayerGeometry.Snap(layer.Thickness, sizeFactor, 1);
        int len = LayerGeometry.Snap(layer.Length, sizeFactor, 1);
        int gap = LayerGeometry.Snap(layer.Gap + anim.GapAdd, sizeFactor, int.MinValue);

        var items = new List<Prim>(arms);
        for (int i = 0; i < arms; i++)
        {
            double rad = (layer.StartAngle + i * 360.0 / arms) * Math.PI / 180;
            double sin = Math.Sin(rad), cos = Math.Cos(rad);
            // Arm pointing up, relative to the center; rotated clockwise (screen y points down).
            (double X, double Y)[] local =
            [
                (-t / 2.0, -gap - len), (t / 2.0, -gap - len), (t / 2.0, -gap), (-t / 2.0, -gap),
            ];
            var pts = local.Select(p => new Vec2(
                Math.Round(0.5 + p.X * cos - p.Y * sin, 2),
                Math.Round(0.5 + p.X * sin + p.Y * cos, 2))).ToList();
            items.Add(new PolygonPrim(pts, common.Fill, common.OutlineColor, common.OutlineWidth));
        }

        return common.Render(layer, items, Pivot);
    }
}
