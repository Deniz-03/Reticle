using System.Globalization;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

/// <summary>Hex text for colors in the property panel: <c>#RRGGBB</c> when opaque, else <c>#RRGGBBAA</c>.</summary>
public static class ColorText
{
    public static string Format(Rgba c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : c.ToHex();

    /// <summary>Accepts <c>RRGGBB</c> or <c>RRGGBBAA</c>, with or without a leading '#'.</summary>
    public static bool TryParse(string? text, out Rgba color)
    {
        color = default;
        var t = text?.Trim() ?? "";
        if (t.StartsWith('#')) t = t[1..];
        if (t.Length is not (6 or 8) || !uint.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _))
            return false;
        color = Rgba.Parse("#" + t);
        return true;
    }

    /// <summary>Parses <paramref name="text"/>; keeps the alpha of <paramref name="fallback"/> when only RGB is given.</summary>
    public static Rgba ParseOr(string? text, Rgba fallback)
    {
        if (!TryParse(text, out var c)) return fallback;
        var digits = (text ?? "").Trim().TrimStart('#');
        return digits.Length == 6 ? c with { A = fallback.A } : c;
    }
}

/// <summary>A value with a localized label for combo boxes.</summary>
public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
