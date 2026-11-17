using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Reticle.App.Views;

/// <summary>Asks DWM for a dark title bar to match the theme (Windows 10 20H1+ / 11; ignored elsewhere).</summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void Apply(Window window)
    {
        int on = 1;
        _ = DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
