using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Reticle.App.Services;

public static class ElevationService
{
    public static bool IsElevated() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public static bool IsForegroundElevated()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out uint pid);
        using var process = Process.GetProcessById((int)pid);
        try { return process.Handle != IntPtr.Zero && IsProcessElevated(process.Handle); }
        catch (Win32Exception) { return true; } // protected elevated process: assume elevated so the warning is conservative
        catch (InvalidOperationException) { return false; }
    }
    private static bool IsProcessElevated(IntPtr handle)
    {
        if (!OpenProcessToken(handle, 8, out var token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try { return GetTokenInformation(token, 20, out int elevated, sizeof(int), out _) && elevated != 0; }
        finally { CloseHandle(token); }
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int type, out int value, int length, out int returned);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
