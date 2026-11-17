using Reticle.Core.Hotkeys;
using Reticle.Core.Model;

namespace Reticle.Core.Tests;

public class HotkeyMatcherTests
{
    private const int F8 = 0x77, F9 = 0x78, LShift = 0xA0, RShift = 0xA1, LCtrl = 0xA2, H = 0x48, W = 0x57, K1 = 0x31;

    private static InputEvent KDown(int c) => new(InputKind.Keyboard, c, true);
    private static InputEvent KUp(int c) => new(InputKind.Keyboard, c, false);
    private static InputEvent MDown(int c) => new(InputKind.Mouse, c, true);
    private static InputEvent MUp(int c) => new(InputKind.Mouse, c, false);

    private static HotkeyMatcher Make(Settings? s = null) { var st = s ?? Settings.Default; return new HotkeyMatcher(() => st); }

    private static Settings With(params HotkeyBinding[] b) => Settings.Default with { Hotkeys = b };

    private static OverlayCommand[] Cmds(HotkeyMatcher m, InputEvent e) =>
        m.Process(e).Where(c => c is not AnimationTrigger).ToArray();

    [Fact]
    public void F8_Down_TogglesOverlay()
    {
        var r = Cmds(Make(), KDown(F8));
        Assert.Equal(new OverlayCommand[] { new ToggleOverlay() }, r);
    }

    [Fact]
    public void Repeat_IsIgnored()
    {
        var m = Make();
        Assert.Single(Cmds(m, KDown(F8)));
        Assert.Empty(m.Process(KDown(F8)));
        Assert.Empty(m.Process(KDown(F8)));
    }

    [Fact]
    public void ShiftF9_Previous_NotNext()
    {
        var m = Make();
        m.Process(KDown(LShift));
        Assert.Equal(new OverlayCommand[] { new Cycle(-1) }, Cmds(m, KDown(F9)));
        m.Process(KUp(F9)); m.Process(KUp(LShift));
        Assert.Equal(new OverlayCommand[] { new Cycle(1) }, Cmds(m, KDown(F9)));
    }

    [Fact]
    public void ExactModifierSet_Required()
    {
        var m = Make();
        m.Process(KDown(LCtrl));
        m.Process(KDown(LShift));
        Assert.Empty(Cmds(m, KDown(F9)));
    }

    [Fact]
    public void Ctrl_F8_Centers_NotToggle()
    {
        var m = Make();
        m.Process(KDown(LCtrl));
        Assert.Equal(new OverlayCommand[] { new CenterOffset() }, Cmds(m, KDown(F8)));
    }

    [Fact]
    public void ModifierAlone_NeverFiresNonModifierBinding()
    {
        var m = Make();
        Assert.Empty(Cmds(m, KDown(LShift)));
        Assert.Empty(Cmds(m, KDown(LCtrl)));
    }

    [Fact]
    public void BothShifts_ReleasingOneKeepsShiftHeld()
    {
        var m = Make();
        m.Process(KDown(LShift)); m.Process(KDown(RShift)); m.Process(KUp(LShift));
        Assert.Equal(new OverlayCommand[] { new Cycle(-1) }, Cmds(m, KDown(F9)));
    }

    [Fact]
    public void HideWhileHeld_ReleasesAfterOtherKey()
    {
        var m = Make(With(new HotkeyBinding(HotkeyAction.HideWhileHeld, new KeyBinding(InputKind.Keyboard, H))));
        Assert.Equal(new OverlayCommand[] { new SetHidden(true) }, Cmds(m, KDown(H)));
        Assert.Empty(Cmds(m, KDown(W)));
        Assert.Equal(new OverlayCommand[] { new SetHidden(false) }, Cmds(m, KUp(H)));
    }

    [Fact]
    public void HideWhileHeld_ReleaseIgnoresModifiers()
    {
        var m = Make(With(new HotkeyBinding(HotkeyAction.HideWhileHeld, new KeyBinding(InputKind.Keyboard, H))));
        Cmds(m, KDown(H));
        m.Process(KDown(LShift));
        Assert.Equal(new OverlayCommand[] { new SetHidden(false) }, Cmds(m, KUp(H)));
    }

    [Fact]
    public void RightClick_HidesWhenEnabled()
    {
        var m = Make(Settings.Default with { HideOnRightClick = true });
        Assert.Equal(new OverlayCommand[] { new SetHidden(true) }, Cmds(m, MDown(2)));
        Assert.Equal(new OverlayCommand[] { new SetHidden(false) }, Cmds(m, MUp(2)));
    }

    [Fact]
    public void RightClick_NothingWhenDisabled()
    {
        var m = Make();
        Assert.Empty(Cmds(m, MDown(2)));
        Assert.Empty(Cmds(m, MUp(2)));
    }

    [Fact]
    public void RightClick_IgnoresModifiers()
    {
        var m = Make(Settings.Default with { HideOnRightClick = true });
        m.Process(KDown(LCtrl));
        Assert.Equal(new OverlayCommand[] { new SetHidden(true) }, Cmds(m, MDown(2)));
    }

    [Fact]
    public void HideSources_Combine()
    {
        var s = Settings.Default with
        {
            HideOnRightClick = true,
            Hotkeys = new[] { new HotkeyBinding(HotkeyAction.HideWhileHeld, new KeyBinding(InputKind.Keyboard, H)) },
        };
        var m = Make(s);
        Cmds(m, KDown(H)); Cmds(m, MDown(2));
        Assert.Empty(Cmds(m, KUp(H)));
        Assert.Equal(new OverlayCommand[] { new SetHidden(false) }, Cmds(m, MUp(2)));

        Cmds(m, KDown(H)); Cmds(m, MDown(2));
        Assert.Empty(Cmds(m, MUp(2)));
        Assert.Equal(new OverlayCommand[] { new SetHidden(false) }, Cmds(m, KUp(H)));
    }

    [Fact]
    public void PerCrosshairKey_SelectsCrosshair()
    {
        var m = Make(With(new HotkeyBinding(HotkeyAction.SelectCrosshair, new KeyBinding(InputKind.Keyboard, K1), "abc")));
        Assert.Equal(new OverlayCommand[] { new SelectCrosshair("abc") }, Cmds(m, KDown(K1)));
    }

    [Fact]
    public void SelectCrosshair_WithoutId_Ignored()
    {
        var m = Make(With(
            new HotkeyBinding(HotkeyAction.SelectCrosshair, new KeyBinding(InputKind.Keyboard, K1)),
            new HotkeyBinding(HotkeyAction.SelectCrosshair, new KeyBinding(InputKind.Keyboard, W), "")));
        Assert.Empty(Cmds(m, KDown(K1)));
        Assert.Empty(Cmds(m, KDown(W)));
    }

    [Fact]
    public void SizeAndCenterActions_Map()
    {
        var m = Make(With(
            new HotkeyBinding(HotkeyAction.SizeUp, new KeyBinding(InputKind.Keyboard, 0x21)),
            new HotkeyBinding(HotkeyAction.SizeDown, new KeyBinding(InputKind.Keyboard, 0x22)),
            new HotkeyBinding(HotkeyAction.Center, new KeyBinding(InputKind.Mouse, 3))));
        Assert.Equal(new OverlayCommand[] { new ScaleStep(1) }, Cmds(m, KDown(0x21)));
        Assert.Equal(new OverlayCommand[] { new ScaleStep(-1) }, Cmds(m, KDown(0x22)));
        Assert.Equal(new OverlayCommand[] { new CenterOffset() }, Cmds(m, MDown(3)));
    }

    [Fact]
    public void ModifierBinding_MatchesExcludingItself_AndNullBindingsIgnored()
    {
        var m = Make(With(
            new HotkeyBinding(HotkeyAction.ToggleOverlay, null),
            new HotkeyBinding(HotkeyAction.NextCrosshair, new KeyBinding(InputKind.Keyboard, LShift))));
        Assert.Equal(new OverlayCommand[] { new Cycle(1) }, Cmds(m, KDown(LShift)));
    }

    [Fact]
    public void MultipleMatches_AllFire_InOrder()
    {
        var m = Make(With(
            new HotkeyBinding(HotkeyAction.NextCrosshair, new KeyBinding(InputKind.Keyboard, F8)),
            new HotkeyBinding(HotkeyAction.ToggleOverlay, new KeyBinding(InputKind.Keyboard, F8))));
        Assert.Equal(new OverlayCommand[] { new Cycle(1), new ToggleOverlay() }, Cmds(m, KDown(F8)));
    }

    [Fact]
    public void Settings_ReadLive()
    {
        var s = Settings.Default;
        var m = new HotkeyMatcher(() => s);
        Assert.Empty(Cmds(m, MDown(2))); m.Process(MUp(2));
        s = s with { HideOnRightClick = true };
        Assert.Single(Cmds(m, MDown(2)));
    }

    [Fact]
    public void EveryEvent_EmitsAnimationTrigger()
    {
        var m = Make();
        var evs = new[] { KDown(W), KUp(W), MDown(1), MUp(1), KDown(F8), KUp(F8), KUp(H) };
        foreach (var e in evs)
        {
            var r = m.Process(e);
            Assert.Equal(new AnimationTrigger(e.Kind, e.Code, e.Down), r[^1]);
            Assert.Single(r.OfType<AnimationTrigger>());
        }
    }

    [Fact]
    public void BindingCommandsPrecedeAnimationTrigger()
    {
        var r = Make().Process(KDown(F8));
        Assert.IsType<ToggleOverlay>(r[0]);
        Assert.IsType<AnimationTrigger>(r[1]);
    }

    [Fact]
    public void UpForUnseenKey_OnlyAnimationTrigger()
    {
        var r = Make(Settings.Default with { HideOnRightClick = true }).Process(MUp(2));
        Assert.Equal(new OverlayCommand[] { new AnimationTrigger(InputKind.Mouse, 2, false) }, r);
    }
}
