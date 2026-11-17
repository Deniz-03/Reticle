using Reticle.Core.Codecs;
using Reticle.Core.Model;
using Reticle.Core.Serialization;

namespace Reticle.Core.Tests;

public class ShareCodeTests
{
    private static CrosshairDoc Sample(params Layer[] layers) => new(
        1, "doc1", "Sample",
        new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 3, 3, 4, 5, TimeSpan.Zero),
        1080,
        layers.Length > 0 ? layers : new Layer[]
        {
            new ClassicLayer { Id = "l1", Dot = new Dot(true, 3, true) },
            new RadialLayer { Id = "l2", Arms = 5, StartAngle = 10 },
            new ShapeLayer { Id = "l3", Kind = ShapeKind.Arc, ArcStart = 20, ArcSweep = 90 },
            new ImageLayer { Id = "l4", ImageBase64 = "R0lGOA==", MimeType = "image/gif", Width = 8, Height = 8 },
        });

    [Fact]
    public void RoundTrip_PreservesLayers()
    {
        var doc = Sample();
        var result = ShareCode.Decode(ShareCode.Encode(doc));
        Assert.Null(result.Error);
        Assert.Equal(doc.Name, result.Doc!.Name);
        Assert.Equal(doc.ReferenceHeight, result.Doc.ReferenceHeight);
        Assert.Equal(Json.Serialize(doc.Layers), Json.Serialize(result.Doc.Layers));
    }

    [Fact]
    public void Decode_NewId()
    {
        var doc = Sample();
        var code = ShareCode.Encode(doc);
        var a = ShareCode.Decode(code).Doc!;
        var b = ShareCode.Decode(code).Doc!;
        Assert.NotEqual(doc.Id, a.Id);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(32, a.Id.Length);
        Assert.True(a.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(a.CreatedAt, a.UpdatedAt);
    }

    [Fact]
    public void Decode_TrimsWhitespace()
    {
        var code = ShareCode.Encode(Sample());
        var result = ShareCode.Decode("  \r\n" + code + "\n ");
        Assert.NotNull(result.Doc);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("RTCL1.!!!")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("rtcl1.AAAA")]
    [InlineData("RTCL1.A")]
    [InlineData("RTCL1.AAAAAAAA")]
    [InlineData("0;0;4;1;0.5;1;0;2;2;1;1;0;0;0;1;1;1;1;0;0")] // Valorant-style code
    public void Decode_Garbage_ReturnsError(string code)
    {
        var result = ShareCode.Decode(code);
        Assert.Null(result.Doc);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Decode_Null_ReturnsError()
    {
        Assert.Null(ShareCode.Decode(null!).Doc);
    }

    [Fact]
    public void Decode_ValidBrotliButNotJson_ReturnsError()
    {
        var code = ShareCode.Prefix + Convert.ToBase64String(new byte[] { 0x0B, 0x00, 0x80, 0x61, 0x03 }).TrimEnd('=');
        Assert.Null(ShareCode.Decode(code).Doc);
    }

    [Fact]
    public void Decode_UnsupportedSchema_ReturnsError()
    {
        var code = ShareCode.Encode(Sample() with { Schema = 2 });
        var result = ShareCode.Decode(code);
        Assert.Null(result.Doc);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Decode_OversizedPayload_ReturnsError()
    {
        var big = Sample(new ImageLayer { ImageBase64 = new string('A', 2 * 1024 * 1024), MimeType = "image/png", Width = 8, Height = 8 });
        var result = ShareCode.Decode(ShareCode.Encode(big));
        Assert.Null(result.Doc);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Encode_IsCompact()
    {
        var code = ShareCode.Encode(Sample(new ClassicLayer()));
        Assert.StartsWith("RTCL1.", code);
        Assert.True(code.Length < 300, $"length {code.Length}");
    }

    [Fact]
    public void Encode_StripsIdAndTimestamps()
    {
        var a = ShareCode.Encode(Sample());
        var b = ShareCode.Encode(Sample() with { Id = "other", UpdatedAt = DateTimeOffset.UtcNow });
        Assert.Equal(a, b);
    }
}
