namespace Reticle.Core.Model;

/// <summary>Keyboard codes are Win32 virtual-key codes; mouse codes: 1=Left 2=Right 3=Middle 4=X1 5=X2.</summary>
public sealed record KeyBinding(InputKind Kind, int Code, Modifiers Mods = Modifiers.None);

[Flags]
public enum Modifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}
