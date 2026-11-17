using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reticle.Core.Model;

[JsonConverter(typeof(RgbaJsonConverter))]
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    /// <summary>Parses <c>#RRGGBB</c> (opaque) or <c>#RRGGBBAA</c>.</summary>
    public static Rgba Parse(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length is not (7 or 9) || hex[0] != '#')
            throw new FormatException($"Invalid color '{hex}'; expected #RRGGBB or #RRGGBBAA.");

        static byte Pair(string s, int i)
        {
            if (!byte.TryParse(s.AsSpan(i, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
                throw new FormatException($"Invalid color '{s}'; non-hex digits.");
            return b;
        }

        byte a = hex.Length == 9 ? Pair(hex, 7) : (byte)255;
        return new Rgba(Pair(hex, 1), Pair(hex, 3), Pair(hex, 5), a);
    }

    /// <summary>Always <c>#RRGGBBAA</c> (8 upper-case hex digits).</summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}

internal sealed class RgbaJsonConverter : JsonConverter<Rgba>
{
    public override Rgba Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try { return Rgba.Parse(reader.GetString() ?? throw new JsonException("Color must be a string.")); }
        catch (FormatException e) { throw new JsonException(e.Message, e); }
    }

    public override void Write(Utf8JsonWriter writer, Rgba value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToHex());
}
