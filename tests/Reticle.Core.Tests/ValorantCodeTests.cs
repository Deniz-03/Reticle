using Reticle.Core.Codecs;
using Reticle.Core.Model;

namespace Reticle.Core.Tests;

public class ValorantCodeTests
{
    private static ClassicLayer Layer(ImportResult r, string name) =>
        Assert.IsType<ClassicLayer>(Assert.Single(r.Doc!.Layers, l => l.Name == name));

    [Fact]
    public void Parse_Requirements_Example()
    {
        var r = ValorantCode.Parse("0;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0");
        Assert.Null(r.Error);
        Assert.Null(r.Warning);
        Assert.Equal(1080, r.Doc!.ReferenceHeight);
        Assert.Equal("Valorant Import", r.Doc.Name);
        var inner = Assert.IsType<ClassicLayer>(Assert.Single(r.Doc.Layers));
        Assert.Equal("inner", inner.Name);
        Assert.Equal("#00FFFFFF", inner.Color.ToHex());
        Assert.Equal(4, inner.Length);
        Assert.Equal(2, inner.Gap);
        Assert.Equal(1, inner.Opacity);
        Assert.False(inner.Outline.Enabled);
        Assert.Null(inner.Animation);
    }

    [Fact]
    public void Parse_Defaults_InnerAndOuter()
    {
        var r = ValorantCode.Parse("0;P");
        Assert.Null(r.Error);
        Assert.Equal(new[] { "outer", "inner" }, r.Doc!.Layers.Select(l => l.Name));
        var inner = Layer(r, "inner");
        Assert.Equal((6, 2, 3, 0.8), (inner.Length, inner.Thickness, inner.Gap, inner.Opacity));
        var outer = Layer(r, "outer");
        Assert.Equal((2, 2, 10, 0.35), (outer.Length, outer.Thickness, outer.Gap, outer.Opacity));
        Assert.True(inner.Outline.Enabled);
        Assert.Equal(1, inner.Outline.Width);
        Assert.Equal(0.5, inner.Outline.Opacity);
        Assert.Equal("#000000FF", inner.Outline.Color.ToHex());
        Assert.NotNull(inner.Animation);
    }

    [Fact]
    public void Parse_CustomColor()
    {
        var r = ValorantCode.Parse("0;P;c;8;u;FF5500FF");
        Assert.Equal("#FF5500FF", Layer(r, "inner").Color.ToHex());
    }

    [Fact]
    public void Parse_UsesOnlyPrimary()
    {
        var r = ValorantCode.Parse("0;P;c;1;A;c;7");
        Assert.Equal("#00FF00FF", Layer(r, "inner").Color.ToHex());
    }

    [Fact]
    public void Parse_CenterDot()
    {
        var r = ValorantCode.Parse("0;P;d;1;z;3;a;1;0b;0;1b;0");
        Assert.Null(r.Error);
        var dot = Assert.IsType<ClassicLayer>(Assert.Single(r.Doc!.Layers));
        Assert.Equal("dot", dot.Name);
        Assert.True(dot.Dot.Enabled);
        Assert.Equal(3, dot.Dot.Size);
        Assert.False(dot.Dot.Round);
        Assert.False(dot.Top || dot.Bottom || dot.Left || dot.Right);
    }

    [Fact]
    public void Parse_DotIsLast()
    {
        var r = ValorantCode.Parse("0;P;d;1");
        Assert.Equal(new[] { "outer", "inner", "dot" }, r.Doc!.Layers.Select(l => l.Name));
    }

    [Fact]
    public void Parse_FiringError_AddsAnimation()
    {
        var r = ValorantCode.Parse("0;P;0f;1;1b;0");
        var anim = Layer(r, "inner").Animation!;
        Assert.Equal(new InputTrigger(InputKind.Mouse, 1), anim.Trigger);
        Assert.Equal(AnimationMode.Once, anim.Mode);
        var k = Assert.Single(anim.Keyframes);
        Assert.Equal((3.0, 80.0, Easing.EaseOut), (k.GapAdd, k.DurationMs, k.Easing));
    }

    [Fact]
    public void Parse_FiringErrorOff_NoAnimation()
    {
        var r = ValorantCode.Parse("0;P;0f;0;1f;0");
        Assert.Null(Layer(r, "inner").Animation);
        Assert.Null(Layer(r, "outer").Animation);
    }

    [Fact]
    public void Parse_OutlineSettings()
    {
        var r = ValorantCode.Parse("0;P;h;1;t;2;o;0.25;1b;0");
        var o = Layer(r, "inner").Outline;
        Assert.True(o.Enabled);
        Assert.Equal(2, o.Width);
        Assert.Equal(0.25, o.Opacity);
    }

    [Fact]
    public void Parse_ZeroLengthOrOpacity_OmitsSection()
    {
        var r = ValorantCode.Parse("0;P;0l;0;1a;0");
        Assert.Empty(r.Doc!.Layers);
        Assert.Equal("Crosshair has no visible parts.", r.Warning);
    }

    [Fact]
    public void Parse_VerticalLengthDiffers_Warns()
    {
        var r = ValorantCode.Parse("0;P;0l;4;0g;1;0v;6;1b;0");
        Assert.Equal(4, Layer(r, "inner").Length);
        Assert.Contains("4", r.Warning);
        Assert.Contains("6", r.Warning);
    }

    [Fact]
    public void Parse_VerticalLengthIgnoredWithoutG()
    {
        var r = ValorantCode.Parse("0;P;0l;4;0v;6;1b;0");
        Assert.Null(r.Warning);
    }

    [Fact]
    public void Parse_IgnoresUnknownKeys()
    {
        var r = ValorantCode.Parse("0;P;c;1;zz;9;");
        Assert.Null(r.Error);
        Assert.Equal("#00FF00FF", Layer(r, "inner").Color.ToHex());
    }

    [Fact]
    public void Parse_IgnoresPreambleBeforeMarker()
    {
        var r = ValorantCode.Parse("0;s;1;P;c;1;1b;0");
        Assert.Null(r.Error);
        Assert.Equal("#00FF00FF", Layer(r, "inner").Color.ToHex());
    }

    [Fact]
    public void Parse_DecimalsUseInvariantCulture()
    {
        var r = ValorantCode.Parse("0;P;0a;0.5;1b;0");
        Assert.Equal(0.5, Layer(r, "inner").Opacity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("RTCL1.abc")]
    [InlineData("1;P;c;1")]
    [InlineData("0;P;c;abc")]
    [InlineData("0;P;c;9")]
    [InlineData("0;P;h;2")]
    [InlineData("0;P;0l;NaN")]
    [InlineData("0;P;c;8;u;ZZZ")]
    [InlineData("0;P;c")]
    public void Parse_Garbage_ReturnsError(string code)
    {
        var r = ValorantCode.Parse(code);
        Assert.Null(r.Doc);
        Assert.False(string.IsNullOrEmpty(r.Error));
    }

    [Fact]
    public void Parse_Null_ReturnsError()
    {
        Assert.Null(ValorantCode.Parse(null!).Doc);
    }
}
