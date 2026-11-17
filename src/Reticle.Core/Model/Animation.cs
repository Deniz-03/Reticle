namespace Reticle.Core.Model;

public enum InputKind { Keyboard, Mouse }

public enum AnimationMode { Once, WhileHeld }

public enum Easing { Linear, EaseIn, EaseOut, EaseInOut }

/// <summary>Keyboard codes are Win32 virtual-key codes; mouse codes: 1=Left 2=Right 3=Middle 4=X1 5=X2.</summary>
public sealed record InputTrigger(InputKind Kind, int Code);

public sealed record Keyframe(
    double DurationMs,
    Easing Easing = Easing.Linear,
    double GapAdd = 0,
    double ScaleMul = 1,
    double RotationAdd = 0,
    double OpacityMul = 1,
    Rgba? Color = null);

public sealed record LayerAnimation(
    InputTrigger Trigger,
    AnimationMode Mode,
    IReadOnlyList<Keyframe> Keyframes);
