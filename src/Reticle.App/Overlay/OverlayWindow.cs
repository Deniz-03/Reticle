using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Reticle.App.Overlay;

/// <summary>
/// Transparent, click-through, always-on-top, non-activating window of <see cref="OverlayPlacement.WindowSize"/>
/// device pixels, centered on the chosen monitor's center pixel. Position and size are set only via
/// <c>SetWindowPos</c> in device pixels. Hiding draws nothing; the window itself stays up.
/// </summary>
public sealed class OverlayWindow : Window
{
    private const long OverlayExStyles = Win32.WS_EX_TRANSPARENT | Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;

    private readonly AppState _state;
    private readonly MonitorService _monitorService;
    private readonly OverlayRenderer _renderer;
    private readonly DispatcherTimer _topmostTimer;
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private IntPtr _hwnd;
    private (MonitorInfo Monitor, PointI Pos)? _placed;

    public OverlayWindow(AppState state, MonitorService monitorService)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(monitorService);
        _state = state;
        _monitorService = monitorService;

        Title = "Reticle Overlay";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;

        _renderer = new OverlayRenderer(state);
        Content = _renderer;

        // OV-10: some fullscreen-windowed games push themselves above topmost windows; re-assert once a second.
        _topmostTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _topmostTimer.Tick += (_, _) => AssertTopmost();

        _state.Changed += OnStateChanged;
    }

    /// <summary>The monitor the overlay currently sits on.</summary>
    public MonitorInfo? CurrentMonitor => _placed?.Monitor;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        long ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex | OverlayExStyles));
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        RefreshMonitorsAndPlace();
        _topmostTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _topmostTimer.Stop();
        _state.Changed -= OnStateChanged;
        _renderer.Detach();
        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is Win32.WM_DISPLAYCHANGE or Win32.WM_DPICHANGED)
        {
            // Let WPF process the message first (it updates its DPI and resizes), then restore our exact placement.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, RefreshMonitorsAndPlace);
        }
        return IntPtr.Zero;
    }

    private void OnStateChanged() => Place(force: false);

    private void RefreshMonitorsAndPlace()
    {
        _monitors = _monitorService.GetAll();
        Place(force: true);
    }

    private void Place(bool force)
    {
        if (_hwnd == IntPtr.Zero) return;
        var monitor = MonitorService.Pick(_monitors, _state.Settings.MonitorDeviceName);
        if (monitor is null) return;
        var pos = OverlayPlacement.WindowTopLeft(monitor, _state.Settings.Offset, OverlayPlacement.WindowSize);
        if (!force && _placed == (monitor, pos)) return;

        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, pos.X, pos.Y,
            OverlayPlacement.WindowSize, OverlayPlacement.WindowSize, Win32.SWP_NOACTIVATE);
        _placed = (monitor, pos);
        _renderer.Monitor = monitor;
    }

    private void AssertTopmost()
    {
        if (_hwnd == IntPtr.Zero) return;
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOACTIVATE | Win32.SWP_NOMOVE | Win32.SWP_NOSIZE);
    }
}
