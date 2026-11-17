using Reticle.Core.Model;

namespace Reticle.Core.Hotkeys;

/// <summary>
/// Pure, stateful translator from raw input events to overlay commands. Not thread-safe;
/// feed it from a single thread. Settings are re-read on every <see cref="Process"/> call.
/// </summary>
public sealed class HotkeyMatcher(Func<Settings> settings)
{
    private const int MouseRight = 2;

    private readonly HashSet<(InputKind Kind, int Code)> _down = new();
    private readonly HashSet<int> _modifierVks = new();
    private readonly HashSet<(InputKind Kind, int Code)> _hideKeys = new();
    private bool _rightClickHide;

    private static Modifiers ModifierOf(int vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => Modifiers.Shift,
        0x11 or 0xA2 or 0xA3 => Modifiers.Ctrl,
        0x12 or 0xA4 or 0xA5 => Modifiers.Alt,
        0x5B or 0x5C => Modifiers.Win,
        _ => Modifiers.None,
    };

    public IReadOnlyList<OverlayCommand> Process(InputEvent e)
    {
        var key = (e.Kind, e.Code);
        var result = new List<OverlayCommand>();

        if (e.Down)
        {
            if (!_down.Add(key)) return result; // auto-repeat: ignored entirely
            if (e.Kind == InputKind.Keyboard && ModifierOf(e.Code) != Modifiers.None) _modifierVks.Add(e.Code);
            OnDown(e, key, result);
        }
        else
        {
            _down.Remove(key);
            if (e.Kind == InputKind.Keyboard) _modifierVks.Remove(e.Code);
            OnUp(e, key, result);
        }

        result.Add(new AnimationTrigger(e.Kind, e.Code, e.Down));
        return result;
    }

    private Modifiers HeldModifiersExcluding(InputEvent e)
    {
        var mods = Modifiers.None;
        foreach (var vk in _modifierVks)
        {
            if (e.Kind == InputKind.Keyboard && vk == e.Code) continue;
            mods |= ModifierOf(vk);
        }
        return mods;
    }

    private void OnDown(InputEvent e, (InputKind, int) key, List<OverlayCommand> result)
    {
        var s = settings();
        var mods = HeldModifiersExcluding(e);

        foreach (var hb in s.Hotkeys)
        {
            var b = hb.Binding;
            if (b is null || b.Kind != e.Kind || b.Code != e.Code || b.Mods != mods) continue;

            switch (hb.Action)
            {
                case HotkeyAction.ToggleOverlay: result.Add(new ToggleOverlay()); break;
                case HotkeyAction.NextCrosshair: result.Add(new Cycle(1)); break;
                case HotkeyAction.PreviousCrosshair: result.Add(new Cycle(-1)); break;
                case HotkeyAction.Center: result.Add(new CenterOffset()); break;
                case HotkeyAction.SizeUp: result.Add(new ScaleStep(1)); break;
                case HotkeyAction.SizeDown: result.Add(new ScaleStep(-1)); break;
                case HotkeyAction.SelectCrosshair:
                    if (!string.IsNullOrEmpty(hb.CrosshairId)) result.Add(new SelectCrosshair(hb.CrosshairId));
                    break;
                case HotkeyAction.HideWhileHeld:
                    _hideKeys.Add(key);
                    result.Add(new SetHidden(true));
                    break;
            }
        }

        if (s.HideOnRightClick && e.Kind == InputKind.Mouse && e.Code == MouseRight)
        {
            _rightClickHide = true;
            result.Add(new SetHidden(true));
        }
    }

    private void OnUp(InputEvent e, (InputKind, int) key, List<OverlayCommand> result)
    {
        var removed = _hideKeys.Remove(key);
        if (e.Kind == InputKind.Mouse && e.Code == MouseRight && _rightClickHide)
        {
            _rightClickHide = false;
            removed = true;
        }
        if (removed && _hideKeys.Count == 0 && !_rightClickHide) result.Add(new SetHidden(false));
    }
}
