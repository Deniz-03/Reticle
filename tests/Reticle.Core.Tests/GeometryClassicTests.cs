using Reticle.Core.Geometry;
using Reticle.Core.Model;

namespace Reticle.Core.Tests;

public class GeometryClassicTests
{
    private static ClassicLayer Layer(int t, int l, int g) => new() { Thickness = t, Length = l, Gap = g };

    private static RectPrim R(LayerRender r, int i) => Assert.IsType<RectPrim>(r.Items[i]);

    private static void AssertRect(RectPrim p, double x, double y, double w, double h)
        => Assert.Equal((x, y, w, h), (p.X, p.Y, p.W, p.H));

    [Fact]
    public void Classic_T1_L4_G2_FourRects()
    {
        var r = LayerGeometry.Build(Layer(1, 4, 2), 1);
        Assert.Equal(4, r.Items.Count);
        AssertRect(R(r, 0), 0, -6, 1, 4);
        AssertRect(R(r, 1), 0, 3, 1, 4);
        AssertRect(R(r, 2), -6, 0, 4, 1);
        AssertRect(R(r, 3), 3, 0, 4, 1);
        Assert.Equal(new Vec2(0.5, 0.5), r.Pivot);
    }

    [Fact]
    public void Classic_EvenThickness_ExtendsRightDown()
    {
        var r = LayerGeometry.Build(Layer(2, 4, 2), 1);
        AssertRect(R(r, 0), 0, -6, 2, 4);
        AssertRect(R(r, 1), 0, 4, 2, 4);
        Assert.Equal(new Vec2(1, 1), r.Pivot);
    }

    [Fact]
    public void Classic_TShape_OmitsTop()
    {
        var r = LayerGeometry.Build(Layer(1, 4, 2) with { Top = false }, 1);
        Assert.Equal(3, r.Items.Count);
    }

    [Fact]
    public void Classic_SizeFactor_RoundsAfterScaling()
    {
        var r = LayerGeometry.Build(Layer(1, 4, 2), 1.333);
        AssertRect(R(r, 0), 0, -8, 1, 5); // L=5, g=3, t=1
    }

    [Fact]
    public void Dot_Size3_Square()
    {
        var r = LayerGeometry.Build(Layer(1, 4, 2) with { Dot = new Dot(true, 3, false) }, 1);
        Assert.Equal(5, r.Items.Count);
        AssertRect(R(r, 4), -1, -1, 3, 3);
    }

    [Fact]
    public void Dot_Round_IsFilledEllipse()
    {
        var r = LayerGeometry.Build(Layer(1, 4, 2) with { Dot = new Dot(true, 3, true) }, 1);
        var e = Assert.IsType<EllipsePrim>(r.Items[4]);
        Assert.Equal((0.5, 0.5, 1.5, 1.5, 0.0), (e.Cx, e.Cy, e.Rx, e.Ry, e.StrokeWidth));
    }

    [Fact]
    public void Outline_AppliedToAllPrims()
    {
        var layer = Layer(1, 4, 2) with
        {
            Dot = new Dot(true, 3, true),
            Outline = new Outline(true, 1, new Rgba(0, 0, 0, 255), 0.5),
        };
        var r = LayerGeometry.Build(layer, 1);
        Assert.Equal(5, r.Items.Count);
        Assert.All(r.Items, p =>
        {
            Assert.Equal(128, p.OutlineColor!.Value.A);
            Assert.Equal(1, p.OutlineWidth);
        });
    }

    [Fact]
    public void Outline_Disabled_LeavesNoOutline()
    {
        var r = LayerGeometry.Build(Layer(1, 4, 2), 1);
        Assert.All(r.Items, p => Assert.Null(p.OutlineColor));
    }

    [Fact]
    public void NegativeGap_Allowed()
    {
        var r = LayerGeometry.Build(Layer(1, 4, -1), 1);
        AssertRect(R(r, 0), 0, -3, 1, 4);
    }

    [Fact]
    public void Invisible_YieldsNoItems()
    {
        var r = LayerGeometry.Build(Layer(1, 4, 2) with { Visible = false }, 1);
        Assert.Empty(r.Items);
    }

    [Fact]
    public void Anim_AppliesToGapColorAndTransform()
    {
        var layer = Layer(1, 4, 2) with { Opacity = 0.5, Scale = 2, Rotation = 10 };
        var anim = new AnimatedProps(1, 1.5, 5, 0.5, new Rgba(1, 2, 3, 255));
        var r = LayerGeometry.Build(layer, 1, anim);
        AssertRect(R(r, 0), 0, -7, 1, 4); // gap 3
        Assert.Equal(new Rgba(1, 2, 3, 255), r.Items[0].Fill);
        Assert.Equal((0.25, 3.0, 15.0), (r.Opacity, r.Scale, r.Rotation));
    }

    [Fact]
    public void NonClassic_NotImplementedYet()
    {
        Assert.Throws<NotImplementedException>(() => LayerGeometry.Build(new RadialLayer(), 1));
    }
}
