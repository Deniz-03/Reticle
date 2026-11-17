using System.Globalization;
using Reticle.Core.Model;

namespace Reticle.Core.Codecs;

/// <summary>
/// Imports a Valorant crosshair profile code (e.g. <c>0;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0</c>)
/// as a Reticle crosshair. Only the primary (<c>P</c>) section is used.
/// </summary>
/// <remarks>
/// Riot does not publish the format; it is community reverse-engineered. Sources checked (2026-11):
/// https://github.com/faheem-s27/ValorantCrosshairParser (Decoder.json: full key list per section),
/// https://github.com/phenibut645/pupupu/blob/HEAD/apps/web/src/overlay/utils/valorantCrosshair.ts (parser),
/// https://github.com/ruwiss/valorant-tracker/blob/HEAD/src/utils/crosshair.ts (generator that omits keys equal
/// to the in-game defaults, which gives the default table below).
/// Defaults when a key is absent: outlines on, t=1, o=0.5; center dot off, z=2, a=1;
/// inner lines on, 0t=2, 0l=6, 0o=3, 0a=0.8; outer lines on, 1t=2, 1l=2, 1o=10, 1a=0.35; color 0 (white).
/// Firing-error default (0f/1f = on) is not stated by those sources; it is inferred from real codes always
/// spelling out "0f;0" when it is off. Movement-error keys (0m, 1m, 0s, ...) have no Reticle equivalent and are ignored.
/// </remarks>
public static class ValorantCode
{
    private const string Invalid = "Invalid Valorant crosshair code.";
    private const int MaxSize = 1000;

    private static readonly string[] PresetColors =
    {
        "#FFFFFF", "#00FF00", "#7FFF00", "#DFFF00", "#FFFF00", "#00FFFF", "#FF00FF", "#FF0000",
    };

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "c", "u", "h", "t", "o", "d", "z", "a",
        "0b", "0t", "0l", "0v", "0g", "0o", "0a", "0f",
        "1b", "1t", "1l", "1v", "1g", "1o", "1a", "1f",
    };

    private static readonly LayerAnimation FiringError = new(
        new InputTrigger(InputKind.Mouse, 1),
        AnimationMode.Once,
        new[] { new Keyframe(80, Easing.EaseOut, GapAdd: 3) });

    private sealed class InvalidCodeException : Exception { }

    /// <summary>Never throws; failures are reported through <see cref="ImportResult.Error"/>.</summary>
    public static ImportResult Parse(string code)
    {
        try
        {
            var tokens = (code ?? "").Trim().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0 || tokens[0] != "0")
                return new ImportResult(null, Invalid);

            return Build(new Values(ReadPrimarySection(tokens)));
        }
        catch (InvalidCodeException)
        {
            return new ImportResult(null, Invalid);
        }
    }

    private static Dictionary<string, string> ReadPrimarySection(string[] tokens)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var inPrimary = false;
        for (var i = 1; i < tokens.Length;)
        {
            if (tokens[i] is "P" or "A" or "S")
            {
                inPrimary = tokens[i] == "P";
                i++;
                continue;
            }
            if (inPrimary)
            {
                if (i + 1 < tokens.Length)
                    map[tokens[i]] = tokens[i + 1];
                else if (KnownKeys.Contains(tokens[i]))
                    throw new InvalidCodeException(); // known key without a value
            }
            i += 2;
        }
        return map;
    }

    private readonly struct Values(Dictionary<string, string> map)
    {
        public double Num(string key, double fallback, double min = 0, double max = MaxSize)
        {
            if (!map.TryGetValue(key, out var s)) return fallback;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v))
                throw new InvalidCodeException();
            return Math.Clamp(v, min, max);
        }

        public bool Bool(string key, bool fallback)
        {
            if (!map.TryGetValue(key, out var s)) return fallback;
            return s switch { "1" => true, "0" => false, _ => throw new InvalidCodeException() };
        }

        public string? Text(string key) => map.GetValueOrDefault(key);
    }

    private static ImportResult Build(Values v)
    {
        var color = ReadColor(v);
        var outline = new Outline(v.Bool("h", true), v.Num("t", 1, 0, 20), new Rgba(0, 0, 0, 255), v.Num("o", 0.5, 0, 1));

        var layers = new List<Layer>();
        var warnings = new List<string>();

        var outer = Lines(v, "1", "outer", color, outline, 2, 2, 10, 0.35, warnings);
        var inner = Lines(v, "0", "inner", color, outline, 6, 2, 3, 0.8, warnings);
        if (outer is not null) layers.Add(outer);
        if (inner is not null) layers.Add(inner);

        var dotOn = v.Bool("d", false);
        var dotSize = v.Num("z", 2);
        var dotOpacity = v.Num("a", 1, 0, 1);
        if (dotOn && dotSize > 0 && dotOpacity > 0)
        {
            layers.Add(new ClassicLayer
            {
                Name = "dot",
                Color = color,
                Opacity = dotOpacity,
                Outline = outline,
                Top = false, Bottom = false, Left = false, Right = false,
                Dot = new Dot(true, ToPixels(dotSize, 1), false),
            });
        }

        if (layers.Count == 0)
            warnings.Add("Crosshair has no visible parts.");

        var now = DateTimeOffset.UtcNow;
        var doc = new CrosshairDoc(CrosshairDoc.CurrentSchema, Guid.NewGuid().ToString("N"), "Valorant Import",
            now, now, 1080, layers);
        return new ImportResult(doc, null, warnings.Count == 0 ? null : string.Join(" ", warnings));
    }

    private static ClassicLayer? Lines(Values v, string p, string name, Rgba color, Outline outline,
        double defLength, double defThickness, double defOffset, double defOpacity, List<string> warnings)
    {
        var shown = v.Bool(p + "b", true);
        var length = v.Num(p + "l", defLength);
        var thickness = v.Num(p + "t", defThickness);
        var offset = v.Num(p + "o", defOffset);
        var opacity = v.Num(p + "a", defOpacity, 0, 1);
        var fireError = v.Bool(p + "f", true);
        var separate = v.Bool(p + "g", false);
        var vertical = v.Num(p + "v", length);

        if (!shown || length <= 0 || opacity <= 0)
            return null;

        if (separate && vertical != length)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"Valorant {name} lines have separate lengths (horizontal {length}, vertical {vertical}); using {length}."));
        }

        return new ClassicLayer
        {
            Name = name,
            Color = color,
            Opacity = opacity,
            Outline = outline,
            Length = ToPixels(length, 1),
            Thickness = ToPixels(thickness, 1),
            Gap = ToPixels(offset, 0),
            Animation = fireError ? FiringError : null,
        };
    }

    private static int ToPixels(double value, int min) =>
        Math.Max(min, (int)Math.Round(value, MidpointRounding.AwayFromZero));

    private static Rgba ReadColor(Values v)
    {
        var index = v.Num("c", 0, -1, 9);
        if (index != Math.Floor(index) || index is < 0 or > 8) throw new InvalidCodeException();
        if (index < 8) return Rgba.Parse(PresetColors[(int)index]);

        var u = v.Text("u");
        if (u is null) return new Rgba(255, 255, 255, 255);
        if (u.Length is not (6 or 8) || !u.All(Uri.IsHexDigit)) throw new InvalidCodeException();
        return Rgba.Parse("#" + u);
    }
}
