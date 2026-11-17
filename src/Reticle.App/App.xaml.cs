using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Reticle.App.Input;
using Reticle.App.Overlay;
using Reticle.Core.Hotkeys;
using Reticle.Core.Storage;

namespace Reticle.App;

public partial class App : Application
{
    private AppState? _state;
    private OverlayWindow? _overlay;
    private HotkeyMatcher? _matcher;
    private InputHookService? _input;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string root = LibraryStore.DefaultRoot;
        _state = new AppState(new LibraryStore(root), new SettingsStore(root));

        // Overlay only for now; the main window and tray arrive in Task 11.
        _overlay = new OverlayWindow(_state, new MonitorService());
        _overlay.Show();

        var state = _state;
        _matcher = new HotkeyMatcher(() => state.Settings);
        _input = new InputHookService(Dispatcher);
        _input.Input += OnInput;
        try
        {
            _input.Start();
        }
        catch (Win32Exception ex)
        {
            // The overlay still works without hotkeys.
            Debug.WriteLine($"Global input hooks unavailable: {ex.Message}");
        }
    }

    /// <summary>Raw input → hotkeys, smart toggle and animation triggers (UI thread).</summary>
    private void OnInput(InputEvent e)
    {
        if (_state is null || _matcher is null) return;
        foreach (var command in _matcher.Process(e))
            _state.Apply(command);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _input?.Dispose();
        _state?.Dispose();
        base.OnExit(e);
    }
}
