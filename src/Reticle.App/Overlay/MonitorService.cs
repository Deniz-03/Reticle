namespace Reticle.App.Overlay;

/// <param name="BoundsPx">Full monitor rectangle in device pixels (virtual-screen coordinates).</param>
/// <param name="DpiScale">Effective DPI / 96.</param>
public sealed record MonitorInfo(string DeviceName, RectI BoundsPx, double DpiScale, bool IsPrimary);

/// <summary>Enumerates monitors in device pixels (the process is PerMonitorV2 DPI aware).</summary>
public sealed class MonitorService
{
    public IReadOnlyList<MonitorInfo> GetAll()
    {
        var result = new List<MonitorInfo>();
        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            var mi = new Win32.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFOEX>() };
            if (!Win32.GetMonitorInfo(hMonitor, ref mi)) return true;

            double scale = Win32.GetDpiForMonitor(hMonitor, Win32.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0
                ? dpiX / 96.0
                : 1.0;
            var r = mi.rcMonitor;
            result.Add(new MonitorInfo(
                mi.szDevice,
                new RectI(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top),
                scale,
                (mi.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>The monitor named <paramref name="deviceName"/>, else the primary, else the first; null when none exist.</summary>
    public static MonitorInfo? Pick(IReadOnlyList<MonitorInfo> monitors, string? deviceName)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        return monitors.FirstOrDefault(m => deviceName is not null && string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            ?? monitors.FirstOrDefault(m => m.IsPrimary)
            ?? monitors.FirstOrDefault();
    }
}
