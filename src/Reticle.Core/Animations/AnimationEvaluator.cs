using Reticle.Core.Geometry;
using Reticle.Core.Model;

namespace Reticle.Core.Animations;

/// <summary>
/// Tracks per-layer trigger state and evaluates animated properties for a caller-supplied time.
/// Keyframes are stages Identity -> K1 -> K2 ...; returning to Identity uses the first keyframe's duration and easing.
/// </summary>
public sealed class AnimationState
{
    private sealed class LayerState
    {
        public double StartMs;
        public bool Down;
        public double? ReleaseMs;
        public LayerAnimation? Animation; // cached on Evaluate
        public bool Evaluated;
    }

    private readonly Dictionary<string, LayerState> _states = new();

    public void OnTrigger(string layerId, bool down, double nowMs)
    {
        _states.TryGetValue(layerId, out var s);
        if (down)
        {
            // WhileHeld ignores auto-repeat downs while already held; Once restarts on every down.
            if (s is { Down: true, Animation.Mode: AnimationMode.WhileHeld }) return;
            _states[layerId] = new LayerState { StartMs = nowMs, Down = true, Animation = s?.Animation, Evaluated = s?.Evaluated ?? false };
        }
        else if (s is { Down: true })
        {
            s.Down = false;
            s.ReleaseMs = nowMs;
        }
    }

    public AnimatedProps Evaluate(Layer layer, double nowMs)
    {
        var anim = layer.Animation;
        if (_states.TryGetValue(layer.Id, out var st0)) { st0.Animation = anim; st0.Evaluated = true; }
        if (anim is null || anim.Keyframes.Count == 0 || !_states.TryGetValue(layer.Id, out var s))
            return AnimatedProps.Identity;
        var kfs = anim.Keyframes;

        if (anim.Mode == AnimationMode.Once)
        {
            double elapsed = nowMs - s.StartMs;
            if (elapsed < 0) return AnimatedProps.Identity;
            return Sequence(layer, kfs, elapsed, returnToIdentity: true);
        }

        if (s.ReleaseMs is null)
            return Sequence(layer, kfs, Math.Max(0, nowMs - s.StartMs), returnToIdentity: false);

        var from = Sequence(layer, kfs, Math.Max(0, s.ReleaseMs.Value - s.StartMs), returnToIdentity: false);
        return Segment(layer, from, AnimatedProps.Identity, nowMs - s.ReleaseMs.Value, kfs[0]);
    }

    /// <summary>True while some layer's values are still changing.</summary>
    public bool IsAnimating(double nowMs)
    {
        foreach (var s in _states.Values)
        {
            if (s.Animation is not { Keyframes.Count: > 0 } anim)
            {
                if (!s.Evaluated && (s.Down || s.ReleaseMs is not null)) return true; // not yet evaluated
                continue;
            }
            double stages = 0;
            foreach (var k in anim.Keyframes) stages += Math.Max(0, k.DurationMs);
            double back = Math.Max(0, anim.Keyframes[0].DurationMs);

            if (anim.Mode == AnimationMode.Once)
            {
                if (nowMs < s.StartMs + stages + back) return true;
            }
            else if (s.ReleaseMs is null)
            {
                if (s.Down && nowMs < s.StartMs + stages) return true;
            }
            else if (nowMs < s.ReleaseMs.Value + back) return true;
        }
        return false;
    }

    private static AnimatedProps Sequence(Layer layer, IReadOnlyList<Keyframe> kfs, double elapsed, bool returnToIdentity)
    {
        var prev = AnimatedProps.Identity;
        double t = elapsed;
        foreach (var k in kfs)
        {
            var target = ToProps(k);
            double d = Math.Max(0, k.DurationMs);
            if (t < d) return Segment(layer, prev, target, t, k);
            t -= d;
            prev = target;
        }
        if (!returnToIdentity) return prev;
        return Segment(layer, prev, AnimatedProps.Identity, t, kfs[0]);
    }

    private static AnimatedProps ToProps(Keyframe k)
        => new(k.GapAdd, k.ScaleMul, k.RotationAdd, k.OpacityMul, k.Color);

    private static AnimatedProps Segment(Layer layer, AnimatedProps a, AnimatedProps b, double t, Keyframe timing)
    {
        double d = Math.Max(0, timing.DurationMs);
        double u = d <= 0 ? 1 : Math.Clamp(t / d, 0, 1);
        u = Ease(timing.Easing, u);
        return new AnimatedProps(
            Lerp(a.GapAdd, b.GapAdd, u),
            Lerp(a.ScaleMul, b.ScaleMul, u),
            Lerp(a.RotationAdd, b.RotationAdd, u),
            Lerp(a.OpacityMul, b.OpacityMul, u),
            LerpColor(layer.Color, a.Color, b.Color, u));
    }

    private static double Ease(Easing e, double t) => e switch
    {
        Easing.EaseIn => t * t,
        Easing.EaseOut => 1 - (1 - t) * (1 - t),
        Easing.EaseInOut => t * t * (3 - 2 * t),
        _ => t,
    };

    private static double Lerp(double a, double b, double u) => a + (b - a) * u;

    private static Rgba? LerpColor(Rgba baseColor, Rgba? a, Rgba? b, double u)
    {
        if (a is null && b is null) return null;
        var x = a ?? baseColor;
        var y = b ?? baseColor;
        static byte Ch(byte p, byte q, double u) => (byte)Math.Clamp(Math.Round(p + (q - p) * u), 0, 255);
        return new Rgba(Ch(x.R, y.R, u), Ch(x.G, y.G, u), Ch(x.B, y.B, u), Ch(x.A, y.A, u));
    }
}
