using System.Windows;
using Reticle.App.Overlay;
using Reticle.Core.Storage;

namespace Reticle.App;

public partial class App : Application
{
    private AppState? _state;
    private OverlayWindow? _overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string root = LibraryStore.DefaultRoot;
        _state = new AppState(new LibraryStore(root), new SettingsStore(root));

        // Overlay only for now; the main window and tray arrive in Task 11.
        _overlay = new OverlayWindow(_state, new MonitorService());
        _overlay.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _state?.Dispose();
        base.OnExit(e);
    }
}
