using Reticle.Core.Model;

namespace Reticle.Core.Geometry;

public static class LayerGeometry
{
    /// <param name="sizeFactor">globalScale x (ReferenceHeight is int h ? monitorHeight / h : 1).</param>
    public static LayerRender Build(Layer layer, double sizeFactor, AnimatedProps? anim = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return layer switch
        {
            ClassicLayer c => ClassicGeometry.Build(c, sizeFactor, anim),
            RadialLayer r => RadialGeometry.Build(r, sizeFactor, anim),
            ShapeLayer s => ShapeGeometry.Build(s, sizeFactor, anim),
            ImageLayer i => ShapeGeometry.BuildImage(i, sizeFactor, anim),
            _ => throw new NotSupportedException($"Unknown layer type {layer.GetType().Name}."),
        };
    }

    internal static int Snap(double value, double sizeFactor, int min)
        => Math.Max(min, (int)Math.Round(value * sizeFactor, MidpointRounding.AwayFromZero));

    /// <summary>Layer-level values shared by all layer types, with animation and size factor applied.</summary>
    internal sealed record Common(
        double Scale, double Rotation, double Opacity, Rgba Fill,
        Rgba? OutlineColor, double OutlineWidth, Vec2 Offset, double Blur)
    {
        public static Common From(Layer layer, double sizeFactor, AnimatedProps anim)
        {
            Rgba? outlineColor = null;
            double outlineWidth = 0;
            if (layer.Outline.Enabled)
            {
                double a = Math.Round(layer.Outline.Color.A * layer.Outline.Opacity, MidpointRounding.AwayFromZero);
                outlineColor = layer.Outline.Color with { A = (byte)Math.Clamp(a, 0, 255) };
                outlineWidth = Snap(layer.Outline.Width, sizeFactor, 1);
            }

            var offset = new Vec2(
                Math.Round(layer.Offset.X * sizeFactor, MidpointRounding.AwayFromZero),
                Math.Round(layer.Offset.Y * sizeFactor, MidpointRounding.AwayFromZero));

            return new Common(
                layer.Scale * anim.ScaleMul,
                layer.Rotation + anim.RotationAdd,
                layer.Opacity * anim.OpacityMul,
                anim.Color ?? layer.Color,
                outlineColor, outlineWidth, offset, layer.Blur * sizeFactor);
        }

        public LayerRender Render(Layer layer, IReadOnlyList<Prim> items, Vec2 pivot)
            => new(layer.Id, layer.Visible ? items : [], Opacity, Blur, Rotation, Scale, Offset, pivot);
    }
}
