using System.Windows.Input;
using Reticle.App.Resources;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

/// <summary>Localized display texts for model enums and triggers.</summary>
public static class Labels
{
    public static string LayerType(Layer layer) => layer switch
    {
        ClassicLayer => Strings.LayerTypeClassic,
        RadialLayer => Strings.LayerTypeRadial,
        ShapeLayer s => Shape(s.Kind),
        ImageLayer => Strings.LayerTypeImage,
        _ => layer.GetType().Name,
    };

    public static string Shape(ShapeKind kind) => kind switch
    {
        ShapeKind.Circle => Strings.ShapeCircle,
        ShapeKind.Ring => Strings.ShapeRing,
        ShapeKind.Rectangle => Strings.ShapeRectangle,
        ShapeKind.Triangle => Strings.ShapeTriangle,
        ShapeKind.Chevron => Strings.ShapeChevron,
        ShapeKind.Arc => Strings.ShapeArc,
        ShapeKind.Line => Strings.ShapeLine,
        _ => kind.ToString(),
    };

    public static string Easing(Easing easing) => easing switch
    {
        Core.Model.Easing.EaseIn => Strings.EasingIn,
        Core.Model.Easing.EaseOut => Strings.EasingOut,
        Core.Model.Easing.EaseInOut => Strings.EasingInOut,
        _ => Strings.EasingLinear,
    };

    public static string Mode(AnimationMode mode) => mode == AnimationMode.WhileHeld ? Strings.ModeWhileHeld : Strings.ModeOnce;

    public static string Trigger(InputTrigger? trigger) => trigger switch
    {
        null => Strings.TriggerNone,
        { Kind: InputKind.Mouse, Code: 1 } => Strings.MouseLeft,
        { Kind: InputKind.Mouse, Code: 2 } => Strings.MouseRight,
        { Kind: InputKind.Mouse, Code: 3 } => Strings.MouseMiddle,
        { Kind: InputKind.Mouse, Code: 4 } => Strings.MouseX1,
        { Kind: InputKind.Mouse, Code: 5 } => Strings.MouseX2,
        { Kind: InputKind.Mouse } t => $"{Strings.MouseButton} {t.Code}",
        _ => KeyInterop.KeyFromVirtualKey(trigger.Code) is var k && k != Key.None ? k.ToString() : $"VK {trigger.Code}",
    };

    public static IReadOnlyList<Choice<T>> Of<T>(Func<T, string> label) where T : struct, Enum
        => Enum.GetValues<T>().Select(v => new Choice<T>(v, label(v))).ToArray();
}
