using Microsoft.Win32;

namespace Reticle.App.Services;

/// <summary>Manages the per-user Windows Run entry without requiring elevation.</summary>
public static class AutostartService
{
    internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "Reticle";

    public static string CommandFor(string executablePath) => $"\"{executablePath}\" {App.MinimizedArg}";

    public static void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) key.SetValue(ValueName, CommandFor(executablePath), RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
