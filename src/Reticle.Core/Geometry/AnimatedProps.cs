using Reticle.Core.Model;

namespace Reticle.Core.Geometry;

/// <summary>Per-frame animated modifiers applied on top of the static layer values.</summary>
public sealed record AnimatedProps(double GapAdd, double ScaleMul, double RotationAdd, double OpacityMul, Rgba? Color)
{
    public static AnimatedProps Identity { get; } = new(0, 1, 0, 1, null);
}
