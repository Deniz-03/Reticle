using Reticle.Core.Geometry;
using Reticle.Core.Model;

namespace Reticle.Core.Tests;

public class GeometryShapesTests
{
    private static double AngleFromCenter(Vec2 p)
    {
        double deg = Math.Atan2(p.X - 0.5, -(p.Y - 0.5)) * 180 / Math.PI; // 0 = up, clockwise
        return (deg + 360) % 360;
    }

    private static Vec2 FarEdgeMid(PolygonPrim p)
        => new((p.Points[0].X + p.Points[1].X) / 2, (p.Points[0].Y + p.Points[1].Y) / 2);

    [Fact]
    public void Radial_3Arms_AnglesAt0_120_240()
    {
        var r = LayerGeometry.Build(new RadialLayer { Arms = 3, Length = 6, Thickness = 2, Gap = 3 }, 1);
        Assert.Equal(3, r.Items.Count);
        var m0 = FarEdgeMid(Assert.IsType<PolygonPrim>(r.Items[0]));
        Assert.Equal(0.5, m0.X, 2);
        Assert.True(m0.Y < 0);
        Assert.Equal(-8.5, m0.Y, 2);
        Assert.Equal(120, AngleFromCenter(FarEdgeMid(Assert.IsType<PolygonPrim>(r.Items[1]))), 0.5);
        Assert.Equal(240, AngleFromCenter(FarEdgeMid(Assert.IsType<PolygonPrim>(r.Items[2]))), 0.5);
        Assert.Equal(new Vec2(0.5, 0.5), r.Pivot);
    }

    [Theory]
    [InlineData(12, 8)]
    [InlineData(1, 2)]
    public void Radial_ArmsOutOfRange_Clamped(int arms, int expected)
        => Assert.Equal(expected, LayerGeometry.Build(new RadialLayer { Arms = arms }, 1).Items.Count);

    [Fact]
    public void Radial_GapAdd_WidensGap()
    {
        var r = LayerGeometry.Build(new RadialLayer { Arms = 2, Length = 6, Gap = 3 }, 1,
            AnimatedProps.Identity with { GapAdd = 2 });
        Assert.Equal(-10.5, FarEdgeMid(Assert.IsType<PolygonPrim>(r.Items[0])).Y, 2);
    }

    [Fact]
    public void Ring_StrokeWidth()
    {
        var r = LayerGeometry.Build(new ShapeLayer { Kind = ShapeKind.Ring, Width = 10, Height = 10, StrokeWidth = 2 }, 1);
        var e = Assert.IsType<EllipsePrim>(Assert.Single(r.Items));
        Assert.Equal((0.5, 0.5, 5.0, 5.0, 2.0), (e.Cx, e.Cy, e.Rx, e.Ry, e.StrokeWidth));
    }

    [Fact]
    public void Triangle_ApexUp()
    {
        var r = LayerGeometry.Build(new ShapeLayer { Kind = ShapeKind.Triangle, Width = 10, Height = 8 }, 1);
        var p = Assert.IsType<PolygonPrim>(Assert.Single(r.Items));
        var apex = p.Points.MinBy(v => v.Y)!;
        Assert.Equal(0.5, apex.X);
        Assert.Equal(-3.5, apex.Y);
    }

    [Fact]
    public void Rectangle_Centered()
    {
        var r = LayerGeometry.Build(new ShapeLayer { Kind = ShapeKind.Rectangle, Width = 10, Height = 4 }, 1);
        var p = Assert.IsType<RectPrim>(Assert.Single(r.Items));
        Assert.Equal((-4.5, -1.5, 10.0, 4.0), (p.X, p.Y, p.W, p.H));
    }

    [Fact]
    public void Line_HorizontalWithStrokeThickness()
    {
        var r = LayerGeometry.Build(new ShapeLayer { Kind = ShapeKind.Line, Width = 10, StrokeWidth = 2 }, 1);
        var p = Assert.IsType<RectPrim>(Assert.Single(r.Items));
        Assert.Equal((-4.5, -0.5, 10.0, 2.0), (p.X, p.Y, p.W, p.H));
    }

    [Fact]
    public void Arc_UsesHalfWidthRadius()
    {
        var r = LayerGeometry.Build(new ShapeLayer { Kind = ShapeKind.Arc, Width = 10, StrokeWidth = 2, ArcStart = 30, ArcSweep = 90 }, 2);
        var a = Assert.IsType<ArcPrim>(Assert.Single(r.Items));
        Assert.Equal((0.5, 0.5, 10.0, 30.0, 90.0, 4.0), (a.Cx, a.Cy, a.R, a.StartDeg, a.SweepDeg, a.StrokeWidth));
    }

    [Fact]
    public void Chevron_SixPointsWithinBoundingBox()
    {
        var r = LayerGeometry.Build(new ShapeLayer { Kind = ShapeKind.Chevron, Width = 10, Height = 6, StrokeWidth = 2 }, 1);
        var p = Assert.IsType<PolygonPrim>(Assert.Single(r.Items));
        Assert.Equal(6, p.Points.Count);
        Assert.Equal((-4.5, 5.5, -2.5, 3.5), (p.Points.Min(v => v.X), p.Points.Max(v => v.X), p.Points.Min(v => v.Y), p.Points.Max(v => v.Y)));
        Assert.Equal(0.5, p.Points.MinBy(v => v.Y)!.X);
    }

    [Fact]
    public void Shape_StrokeWidth_MinOnePixelWhenPositive()
    {
        var r = LayerGeometry.Build(new ShapeLayer { Kind = ShapeKind.Ring, Width = 10, StrokeWidth = 0.2 }, 1);
        Assert.Equal(1, Assert.IsType<EllipsePrim>(r.Items[0]).StrokeWidth);
    }

    [Fact]
    public void Image_ScaledBySizeFactor()
    {
        var layer = new ImageLayer { Width = 20, Height = 10 };
        var r = LayerGeometry.Build(layer, 2);
        var p = Assert.IsType<ImagePrim>(Assert.Single(r.Items));
        Assert.Equal((layer.Id, -19.5, -9.5, 40.0, 20.0), (p.LayerId, p.X, p.Y, p.W, p.H));
    }

    [Fact]
    public void InvisibleLayers_HaveNoItems()
    {
        Assert.Empty(LayerGeometry.Build(new RadialLayer { Visible = false }, 1).Items);
        Assert.Empty(LayerGeometry.Build(new ShapeLayer { Visible = false }, 1).Items);
        Assert.Empty(LayerGeometry.Build(new ImageLayer { Visible = false }, 1).Items);
    }

    [Fact]
    public void AnimatedProps_AppliedToShapeLayer()
    {
        var anim = new AnimatedProps(5, 2, 10, 0.5, new Rgba(1, 2, 3, 255));
        var r = LayerGeometry.Build(new ShapeLayer { Rotation = 5, Opacity = 0.8 }, 1, anim);
        Assert.Equal((2.0, 15.0, 0.4), (r.Scale, r.Rotation, r.Opacity));
        Assert.Equal(new Rgba(1, 2, 3, 255), r.Items[0].Fill);
    }

    [Fact]
    public void OffsetAndBlur_ScaledForAllTypes()
    {
        Layer[] layers = [new RadialLayer(), new ShapeLayer(), new ImageLayer()];
        foreach (var l in layers)
        {
            var r = LayerGeometry.Build(l with { Offset = new Vec2(3, -2), Blur = 1.5 }, 2);
            Assert.Equal(new Vec2(6, -4), r.Offset);
            Assert.Equal(3, r.Blur);
        }
    }

    [Fact]
    public void Outline_AppliedToRadialPolygons()
    {
        var l = new RadialLayer { Outline = new Outline(true, 1, new Rgba(0, 0, 0, 255), 0.5) };
        var p = LayerGeometry.Build(l, 2).Items[0];
        Assert.Equal(new Rgba(0, 0, 0, 128), p.OutlineColor);
        Assert.Equal(2, p.OutlineWidth);
    }
}
