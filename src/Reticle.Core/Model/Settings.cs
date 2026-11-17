namespace Reticle.Core.Model;

public enum HotkeyAction
{
    ToggleOverlay, NextCrosshair, PreviousCrosshair, HideWhileHeld,
    Center, SizeUp, SizeDown, SelectCrosshair,
}

public sealed record HotkeyBinding(HotkeyAction Action, KeyBinding? Binding, string? CrosshairId = null);

public sealed record Settings(
    int Schema,
    string? ActiveCrosshairId,
    bool OverlayEnabled,
    string? MonitorDeviceName,
    Vec2 Offset,
    double GlobalScale,
    bool HideOnRightClick,
    bool LaunchOnStartup,
    string? Language,
    IReadOnlyList<HotkeyBinding> Hotkeys)
{
    public const int CurrentSchema = 1;

    private const int VkF8 = 0x77;
    private const int VkF9 = 0x78;

    /// <summary>Factory defaults; a fresh instance on every access.</summary>
    public static Settings Default => new(
        CurrentSchema, null, true, null, new Vec2(0, 0), 1, false, false, null,
        new HotkeyBinding[]
        {
            new(HotkeyAction.ToggleOverlay, new KeyBinding(InputKind.Keyboard, VkF8)),
            new(HotkeyAction.NextCrosshair, new KeyBinding(InputKind.Keyboard, VkF9)),
            new(HotkeyAction.PreviousCrosshair, new KeyBinding(InputKind.Keyboard, VkF9, Modifiers.Shift)),
            new(HotkeyAction.Center, new KeyBinding(InputKind.Keyboard, VkF8, Modifiers.Ctrl)),
            new(HotkeyAction.HideWhileHeld, null),
            new(HotkeyAction.SizeUp, null),
            new(HotkeyAction.SizeDown, null),
        });
}
