using Reticle.Core.Model;
using Reticle.Core.Serialization;

namespace Reticle.Core.Tests;

public class SerializationTests
{
    private static CrosshairDoc SampleDoc()
    {
        var anim = new LayerAnimation(
            new InputTrigger(InputKind.Mouse, 1),
            AnimationMode.Once,
            new[] { new Keyframe(120, Easing.EaseOut, 2, 1.2, 15, 0.5, Rgba.Parse("#FF000080")) });
        return new CrosshairDoc(
            1, "doc1", "Sample",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 3, 3, 4, 5, TimeSpan.Zero),
            1080,
            new Layer[]
            {
                new ClassicLayer { Animation = anim, Dot = new Dot(true, 3, true) },
                new RadialLayer { Arms = 5, StartAngle = 10 },
                new ShapeLayer { Kind = ShapeKind.Arc, ArcStart = 20, ArcSweep = 90 },
                new ImageLayer { ImageBase64 = "R0lGOA==", MimeType = "image/gif", Width = 8, Height = 8 },
            });
    }

    [Fact]
    public void Doc_RoundTrips_AllLayerTypes()
    {
        var doc = SampleDoc();
        var json = Json.Serialize(doc);
        var back = Json.Deserialize<CrosshairDoc>(json);

        Assert.Equal(doc.Layers.Count, back.Layers.Count);
        for (int i = 0; i < doc.Layers.Count; i++)
        {
            Assert.IsType(doc.Layers[i].GetType(), back.Layers[i]);
            Assert.Equal(Json.Serialize(doc.Layers[i]), Json.Serialize(back.Layers[i]));
        }
        Assert.Equal(json, Json.Serialize(back));
        Assert.Equal(doc.ReferenceHeight, back.ReferenceHeight);
        Assert.Equal(doc.CreatedAt, back.CreatedAt);
    }

    [Fact]
    public void Settings_RoundTrips()
    {
        var s = Settings.Default;
        var json = Json.Serialize(s);
        Assert.Equal(json, Json.Serialize(Json.Deserialize<Settings>(json)));
    }

    [Fact]
    public void Rgba_Parse_6And8Digits()
    {
        Assert.Equal(new Rgba(0, 255, 255, 255), Rgba.Parse("#00FFFF"));
        var c = Rgba.Parse("#FF000080");
        Assert.Equal(128, c.A);
        Assert.Equal("#FF000080", c.ToHex());
        Assert.Equal("#00FFFFFF", Rgba.Parse("#00FFFF").ToHex());
    }

    [Fact]
    public void Rgba_Parse_Invalid_Throws()
    {
        Assert.Throws<FormatException>(() => Rgba.Parse("00FFFF"));
        Assert.Throws<FormatException>(() => Rgba.Parse("#00FF"));
        Assert.Throws<FormatException>(() => Rgba.Parse("#GGGGGG"));
    }

    [Fact]
    public void Layer_HasTypeDiscriminator()
    {
        Assert.Contains("\"type\":\"classic\"", Json.Serialize<Layer>(new ClassicLayer()));
        Assert.Contains("\"type\":\"radial\"", Json.Serialize<Layer>(new RadialLayer()));
        Assert.Contains("\"type\":\"shape\"", Json.Serialize<Layer>(new ShapeLayer()));
        Assert.Contains("\"type\":\"image\"", Json.Serialize<Layer>(new ImageLayer()));
    }

    [Fact]
    public void Deserialize_UnknownSchema_Throws()
    {
        var json = Json.Serialize(SampleDoc()).Replace("\"schema\":1", "\"schema\":99");
        Assert.Throws<NotSupportedException>(() => Json.Deserialize<CrosshairDoc>(json));
        var sj = Json.Serialize(Settings.Default).Replace("\"schema\":1", "\"schema\":99");
        Assert.Throws<NotSupportedException>(() => Json.Deserialize<Settings>(sj));
    }

    [Fact]
    public void Defaults_AreUsable()
    {
        var c = new ClassicLayer();
        Assert.Equal(6, c.Length);
        Assert.Equal(new Rgba(255, 255, 255, 255), c.Color);
        Assert.False(c.Outline.Enabled);
        Assert.Equal(32, c.Id.Length);
        var h = Settings.Default.Hotkeys;
        Assert.Equal(0x77, h.Single(x => x.Action == HotkeyAction.ToggleOverlay).Binding!.Code);
        Assert.Equal(Modifiers.Shift, h.Single(x => x.Action == HotkeyAction.PreviousCrosshair).Binding!.Mods);
    }

    [Fact]
    public void Layer_TypeDiscriminatorNotFirst_Deserializes()
    {
        var layer = Json.Deserialize<Layer>("""{"length":9,"type":"classic"}""");
        Assert.Equal(9, Assert.IsType<ClassicLayer>(layer).Length);
    }

    [Theory]
    [InlineData("""{"color":5}""")]
    [InlineData("""{"color":null}""")]
    public void Rgba_NonStringToken_ThrowsJsonException(string json)
    {
        Assert.Throws<System.Text.Json.JsonException>(() => Json.Deserialize<ClassicLayer>(json));
    }
}
