using System.IO;
using Reticle.App;
using Reticle.App.Resources;
using Reticle.App.ViewModels;
using Reticle.Core.Storage;

namespace Reticle.App.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "reticle-main-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private (AppState State, MainViewModel Main) Create()
    {
        var state = new AppState(new LibraryStore(_root), new SettingsStore(_root), () => 0);
        var dialogs = new FakeDialogs();
        var designer = new DesignerViewModel(state, dialogs);
        var main = new MainViewModel(state, edit => new LibraryViewModel(state, dialogs, edit), designer,
            new PositionViewModel(state, new Overlay.MonitorService()), new HotkeysViewModel(state), new SettingsViewModel(state));
        return (state, main);
    }

    [Fact]
    public void ToggleOverlayButton_TurnsOverlayOffAndOn()
    {
        var (state, main) = Create();
        Assert.True(main.OverlayEnabled);
        Assert.Equal(Strings.OverlayTurnOff, main.OverlayToggleText);

        var changed = new List<string?>();
        main.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        main.ToggleOverlayEnabledCommand.Execute(null);
        Assert.False(state.Settings.OverlayEnabled);
        Assert.False(main.OverlayEnabled);
        Assert.Equal(Strings.OverlayTurnOn, main.OverlayToggleText);
        Assert.Equal(Strings.OverlayStatusOff, main.OverlayStatusText);
        Assert.Contains(nameof(MainViewModel.OverlayEnabled), changed);

        main.ToggleOverlayEnabledCommand.Execute(null);
        Assert.True(state.Settings.OverlayEnabled);
        main.Dispose();
        state.Dispose();
    }
}
