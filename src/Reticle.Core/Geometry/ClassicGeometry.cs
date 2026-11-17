using Reticle.Core.Model;

namespace Reticle.Core.Geometry;

public static class ClassicGeometry
{
    public static LayerRender Build(ClassicLayer layer, double sizeFactor, AnimatedProps? anim = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        anim ??= AnimatedProps.Identity;

        var common = LayerGeometry.Common.From(layer, sizeFactor, anim);

        int t = LayerGeometry.Snap(layer.Thickness, sizeFactor, 1);
        int c0 = -((t - 1) / 2);
        var pivot = new Vec2(c0 + t / 2.0, c0 + t / 2.0);

        if (!layer.Visible)
            return common.Render(layer, [], pivot);

        int len = LayerGeometry.Snap(layer.Length, sizeFactor, 1);
        int gap = LayerGeometry.Snap(layer.Gap + anim.GapAdd, sizeFactor, int.MinValue);

        var fill = common.Fill;
        var outlineColor = common.OutlineColor;
        double outlineWidth = common.OutlineWidth;

        var items = new List<Prim>(5);
        void Rect(double x, double y, double w, double h)
            => items.Add(new RectPrim(x, y, w, h, fill, outlineColor, outlineWidth));

        if (layer.Top) Rect(c0, c0 - gap - len, t, len);
        if (layer.Bottom) Rect(c0, c0 + t + gap, t, len);
        if (layer.Left) Rect(c0 - gap - len, c0, len, t);
        if (layer.Right) Rect(c0 + t + gap, c0, len, t);

        if (layer.Dot.Enabled)
        {
            int s = LayerGeometry.Snap(layer.Dot.Size, sizeFactor, 1);
            int d0 = -((s - 1) / 2);
            if (layer.Dot.Round)
                items.Add(new EllipsePrim(d0 + s / 2.0, d0 + s / 2.0, s / 2.0, s / 2.0, 0, fill, outlineColor, outlineWidth));
            else
                Rect(d0, d0, s, s);
        }

        return common.Render(layer, items, pivot);
    }
}
