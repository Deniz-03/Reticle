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
            _ => throw new NotImplementedException($"Geometry for {layer.GetType().Name} is not implemented yet."),
        };
    }

    internal static int Snap(double value, double sizeFactor, int min)
        => Math.Max(min, (int)Math.Round(value * sizeFactor, MidpointRounding.AwayFromZero));
}
