using System.IO;
using Reticle.Core.Hotkeys;
using Reticle.Core.Model;
using Reticle.Core.Storage;

namespace Reticle.App.Tests;

public sealed class AppStateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "reticle-app-tests-" + Guid.NewGuid().ToString("N"));
    private double _now;
    private AppState? _state;

    public void Dispose()
    {
        _state?.Dispose();
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private AppState Create()
    {
        _state = new AppState(new LibraryStore(_root), new SettingsStore(_root), () => _now);
        return _state;
    }

    private static CrosshairDoc Doc(string id, string name, params Layer[] layers) => new(
        1, id, name, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1080,
        layers.Length > 0 ? layers : new Layer[] { new ClassicLayer { Id = "l" } });

    private void SeedLibrary(params CrosshairDoc[] docs)
    {
        var store = new LibraryStore(_root);
        foreach (var d in docs) store.Save(d);
    }

    [Fact]
    public void AppState_SeedsPresets_AndActivatesFirst()
    {
        var s = Create();
        Assert.Equal(Presets.All.Count, s.Library.Count);
        Assert.NotNull(s.Active);
        Assert.Equal(s.Library[0].Id, s.Settings.ActiveCrosshairId);
    }

    [Fact]
    public void AppState_Cycle_WrapsAround()
    {
        SeedLibrary(Doc("a", "A"), Doc("b", "B"), Doc("c", "C"));
        var s = Create();
        Assert.Equal("a", s.Active!.Id);

        s.Apply(new Cycle(-1));
        Assert.Equal("c", s.Active!.Id);
        s.Apply(new Cycle(1));
        Assert.Equal("a", s.Active!.Id);
        s.Apply(new Cycle(1));
        s.Apply(new Cycle(1));
        Assert.Equal("c", s.Active!.Id);
        s.Apply(new Cycle(1));
        Assert.Equal("a", s.Active!.Id);
    }

    [Fact]
    public void AppState_SelectUnknownId_NoChange()
    {
        SeedLibrary(Doc("a", "A"), Doc("b", "B"));
        var s = Create();
        int changed = 0;
        s.Changed += () => changed++;

        s.Apply(new SelectCrosshair("nope"));

        Assert.Equal("a", s.Active!.Id);
        Assert.Equal(0, changed);

        s.Apply(new SelectCrosshair("b"));
        Assert.Equal("b", s.Active!.Id);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void AppState_UnknownActiveId_ActivatesFirst()
    {
        SeedLibrary(Doc("a", "A"), Doc("b", "B"));
        new SettingsStore(_root).Save(Settings.Default with { ActiveCrosshairId = "gone" });
        var s = Create();
        Assert.Equal("a", s.Active!.Id);
    }

    [Fact]
    public void AppState_Toggle_Center_Hidden()
    {
        SeedLibrary(Doc("a", "A"));
        var s = Create();
        s.Update(x => x with { Offset = new Vec2(4, 5) });

        s.Apply(new ToggleOverlay());
        Assert.False(s.Settings.OverlayEnabled);
        s.Apply(new ToggleOverlay());
        Assert.True(s.Settings.OverlayEnabled);

        s.Apply(new CenterOffset());
        Assert.Equal(new Vec2(0, 0), s.Settings.Offset);

        s.Apply(new SetHidden(true));
        Assert.True(s.Hidden);
        s.Apply(new SetHidden(false));
        Assert.False(s.Hidden);
    }

    [Fact]
    public void AppState_ScaleStep_ClampsTo025And4()
    {
        SeedLibrary(Doc("a", "A"));
        var s = Create();
        s.Apply(new ScaleStep(1));
        Assert.Equal(1.05, s.Settings.GlobalScale, 9);
        s.Apply(new ScaleStep(-1));
        s.Apply(new ScaleStep(-1));
        Assert.Equal(0.95, s.Settings.GlobalScale, 9);

        s.Update(x => x with { GlobalScale = 0.27 });
        s.Apply(new ScaleStep(-1));
        Assert.Equal(0.25, s.Settings.GlobalScale, 9);
        s.Update(x => x with { GlobalScale = 3.98 });
        s.Apply(new ScaleStep(1));
        Assert.Equal(4, s.Settings.GlobalScale, 9);
    }

    [Fact]
    public void AppState_AnimationTrigger_OnlyMatchingVisibleLayers()
    {
        var anim = new LayerAnimation(new InputTrigger(InputKind.Mouse, 1), AnimationMode.Once,
            new[] { new Keyframe(100, GapAdd: 5) });
        var other = new LayerAnimation(new InputTrigger(InputKind.Keyboard, 0x20), AnimationMode.Once,
            new[] { new Keyframe(100, GapAdd: 5) });
        var matching = new ClassicLayer { Id = "m", Animation = anim };
        var hidden = new ClassicLayer { Id = "h", Animation = anim, Visible = false };
        var different = new ClassicLayer { Id = "d", Animation = other };
        SeedLibrary(Doc("a", "A", matching, hidden, different));
        var s = Create();

        _now = 1000;
        s.Apply(new AnimationTrigger(InputKind.Mouse, 1, true));

        Assert.True(s.Animations.IsAnimating(1010));
        Assert.Equal(5, s.Animations.Evaluate(matching, 1100).GapAdd, 9);
        Assert.Equal(0, s.Animations.Evaluate(hidden, 1100).GapAdd, 9);
        Assert.Equal(0, s.Animations.Evaluate(different, 1100).GapAdd, 9);
        Assert.False(s.Animations.IsAnimating(1300));
    }

    [Fact]
    public void AppState_Settings_PersistedOnDispose_HiddenNot()
    {
        SeedLibrary(Doc("a", "A"), Doc("b", "B"));
        using (var s = new AppState(new LibraryStore(_root), new SettingsStore(_root)))
        {
            s.Apply(new SelectCrosshair("b"));
            s.Apply(new SetHidden(true));
        }
        var reloaded = Create();
        Assert.Equal("b", reloaded.Settings.ActiveCrosshairId);
        Assert.False(reloaded.Hidden);
    }

    [Fact]
    public void AppState_DeleteActiveDoc_ActivatesFirstRemaining()
    {
        SeedLibrary(Doc("a", "A"), Doc("b", "B"));
        var s = Create();
        s.Apply(new SelectCrosshair("b"));
        s.DeleteDoc("b");
        Assert.Equal("a", s.Active!.Id);
        Assert.Single(s.Library);

        s.SaveDoc(Doc("0", "0 first"));
        Assert.Equal("0", s.Library[0].Id);
        Assert.Equal(2, new LibraryStore(_root).LoadAll().Count);
    }
}
