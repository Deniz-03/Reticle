using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Reticle.App.Input;
using Reticle.App.Overlay;
using Reticle.App.Services;
using Reticle.App.ViewModels;
using Reticle.App.Views;
using Reticle.Core.Hotkeys;
using Reticle.Core.Model;
using Reticle.Core.Storage;

namespace Reticle.App;

public partial class App : Application
{
    private AppState? _state;
    private OverlayWindow? _overlay;
    private HotkeyMatcher? _matcher;
    private InputHookService? _input;
    private MainViewModel? _main;
    private SingleInstance? _singleInstance;
    private TrayService? _tray;
    private DispatcherTimer? _elevationTimer;
    private bool _elevationWarned;

    /// <summary>Command-line flag: start without showing the main window (autostart, Task 13).</summary>
    public const string MinimizedArg = "--minimized";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsPrimary)
        {
            Shutdown();
            return;
        }
        string root = LibraryStore.DefaultRoot;
        _state = new AppState(new LibraryStore(root), new SettingsStore(root));
        ApplyLanguage(_state.Settings.Language);

        var monitors = new MonitorService();
        _overlay = new OverlayWindow(_state, monitors);
        _overlay.Show();

        var state = _state;
        var dialogs = new DialogService();
        var designer = new DesignerViewModel(state, dialogs, doc => PreviewSizeFactor(state, monitors, doc));
        _main = new MainViewModel(state, edit => new LibraryViewModel(state, dialogs, edit), designer,
            new PositionViewModel(state, monitors), new HotkeysViewModel(state), new SettingsViewModel(state));
        var window = new MainWindow(_main);
        MainWindow = window; // closing it exits the app (ShutdownMode=OnMainWindowClose) until the tray arrives
        _tray = new TrayService(state, () =>
        {
            window.Show(); window.WindowState = WindowState.Normal; window.Activate();
        }, Shutdown);
        window.Closing += (_, closing) =>
        {
            if (_tray?.IsExiting != true) { closing.Cancel = true; window.Hide(); }
        };
        _elevationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _elevationTimer.Tick += (_, _) => CheckElevation();
        _elevationTimer.Start();
        CheckElevation();
        _singleInstance.Activated += () => Dispatcher.BeginInvoke(() =>
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        });
        if (!e.Args.Contains(MinimizedArg, StringComparer.OrdinalIgnoreCase)) window.Show();

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

    /// <summary>AP-4: explicit language from settings ("de"/"en"); otherwise the OS UI culture (resources fall back to English).</summary>
    private static void ApplyLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return;
        try
        {
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            Debug.WriteLine($"Unknown language '{language}', using the system language.");
        }
    }

    /// <summary>Designer preview at zoom 1 matches the overlay: global scale and the overlay monitor's height.</summary>
    private static double PreviewSizeFactor(AppState state, MonitorService monitors, CrosshairDoc doc)
    {
        var monitor = MonitorService.Pick(monitors.GetAll(), state.Settings.MonitorDeviceName);
        return monitor is null
            ? state.Settings.GlobalScale * Rendering.CrosshairRasterizer.NominalSizeFactor(doc)
            : OverlayRenderer.SizeFactor(state.Settings, doc, monitor);
    }

    /// <summary>Raw input → hotkeys, smart toggle and animation triggers (UI thread).</summary>
    private void OnInput(InputEvent e)
    {
        if (_state is null || _matcher is null) return;
        _main?.Hotkeys.ProcessInput(e);
        foreach (var command in _matcher.Process(e))
            _state.Apply(command);
    }

    private void CheckElevation()
    {
        bool warning = !ElevationService.IsElevated() && ElevationService.IsForegroundElevated();
        if (_main is not null) _main.ElevationWarning = warning;
        if (warning && !_elevationWarned) _tray?.ShowElevationWarning();
        _elevationWarned = warning;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _input?.Dispose();
        _elevationTimer?.Stop();
        _main?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        _state?.Dispose();
        base.OnExit(e);
    }
}
