using Reticle.Core.Animations;
using Reticle.Core.Geometry;
using Reticle.Core.Model;

namespace Reticle.Core.Tests;

public class AnimationEvaluatorTests
{
    private static readonly InputTrigger Click = new(InputKind.Mouse, 1);

    private static Layer Make(AnimationMode mode, Easing easing = Easing.Linear, params Keyframe[] extra)
    {
        var kfs = new List<Keyframe> { new(100, easing, GapAdd: 4) };
        kfs.AddRange(extra);
        return new ClassicLayer { Id = "a", Animation = new LayerAnimation(Click, mode, kfs) };
    }

    [Fact]
    public void Once_Linear_UpAndBack()
    {
        var layer = Make(AnimationMode.Once);
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        Assert.Equal(2, st.Evaluate(layer, 50).GapAdd, 6);
        Assert.Equal(4, st.Evaluate(layer, 100).GapAdd, 6);
        Assert.Equal(2, st.Evaluate(layer, 150).GapAdd, 6);
        Assert.Equal(AnimatedProps.Identity, st.Evaluate(layer, 200));
        Assert.True(st.IsAnimating(150));
        Assert.False(st.IsAnimating(200));
    }

    [Fact]
    public void WhileHeld_HoldsLastKeyframe()
    {
        var layer = Make(AnimationMode.WhileHeld);
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        Assert.Equal(4, st.Evaluate(layer, 1000).GapAdd, 6);
        Assert.False(st.IsAnimating(1000));
        Assert.True(st.IsAnimating(50));
    }

    [Fact]
    public void WhileHeld_Release_ReturnsToIdentity()
    {
        var layer = Make(AnimationMode.WhileHeld);
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        st.OnTrigger("a", false, 1000);
        Assert.Equal(2, st.Evaluate(layer, 1050).GapAdd, 6);
        Assert.True(st.IsAnimating(1050));
        Assert.Equal(AnimatedProps.Identity, st.Evaluate(layer, 1100));
        Assert.False(st.IsAnimating(1100));
    }

    [Fact]
    public void WhileHeld_ReleaseMidSequence_NoJump()
    {
        var layer = Make(AnimationMode.WhileHeld);
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        st.OnTrigger("a", false, 50);
        Assert.Equal(2, st.Evaluate(layer, 50).GapAdd, 6);
        Assert.Equal(1, st.Evaluate(layer, 100).GapAdd, 6);
    }

    [Fact]
    public void Retrigger_Restarts()
    {
        var layer = Make(AnimationMode.Once);
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        st.OnTrigger("a", true, 150);
        Assert.Equal(2, st.Evaluate(layer, 200).GapAdd, 6);
    }

    [Fact]
    public void EaseOut_Value()
    {
        var layer = Make(AnimationMode.Once, Easing.EaseOut);
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        Assert.Equal(4 * 0.75, st.Evaluate(layer, 50).GapAdd, 6);
    }

    [Fact]
    public void EaseIn_And_EaseInOut_Values()
    {
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        Assert.Equal(4 * 0.25, st.Evaluate(Make(AnimationMode.Once, Easing.EaseIn), 50).GapAdd, 6);
        Assert.Equal(4 * 0.5, st.Evaluate(Make(AnimationMode.Once, Easing.EaseInOut), 50).GapAdd, 6);
    }

    [Fact]
    public void MultiStage_And_ScaleOpacity_Interpolate()
    {
        var layer = Make(AnimationMode.Once, Easing.Linear, new Keyframe(100, ScaleMul: 2, OpacityMul: 0.5));
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        var p = st.Evaluate(layer, 150); // halfway K1 (GapAdd 4) -> K2 (GapAdd 0, scale 2, opacity .5)
        Assert.Equal(2, p.GapAdd, 6);
        Assert.Equal(1.5, p.ScaleMul, 6);
        Assert.Equal(0.75, p.OpacityMul, 6);
    }

    [Fact]
    public void Color_InterpolatesFromLayerColor_NullWhenNoColors()
    {
        var st = new AnimationState();
        st.OnTrigger("a", true, 0);
        Assert.Null(st.Evaluate(Make(AnimationMode.Once), 50).Color);

        var layer = new ClassicLayer
        {
            Id = "a",
            Color = new Rgba(0, 0, 0, 255),
            Animation = new LayerAnimation(Click, AnimationMode.Once, [new Keyframe(100, Color: new Rgba(200, 100, 50, 255))]),
        };
        Assert.Equal(new Rgba(100, 50, 25, 255), st.Evaluate(layer, 50).Color);
    }

    [Fact]
    public void NoAnimation_Or_NoKeyframes_IsIdentity_And_NotAnimating()
    {
        var st = new AnimationState();
        st.OnTrigger("x", true, 0);
        Assert.Equal(AnimatedProps.Identity, st.Evaluate(new ClassicLayer { Id = "x" }, 10));
        var empty = new ClassicLayer { Id = "x", Animation = new LayerAnimation(Click, AnimationMode.Once, []) };
        Assert.Equal(AnimatedProps.Identity, st.Evaluate(empty, 10));
        Assert.False(st.IsAnimating(10));
    }
}
