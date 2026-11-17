using Reticle.App.Services;
using System.IO;
using Reticle.App.ViewModels;
using Reticle.Core.Model;
using Reticle.Core.Storage;

namespace Reticle.App.Tests;

public sealed class AutostartServiceTests
{
    [Fact]
    public void Autostart_Command_ContainsMinimizedFlag()
    {
        string command = AutostartService.CommandFor(@"C:\Program Files\Reticle\Reticle.exe");
        Assert.Equal("\"C:\\Program Files\\Reticle\\Reticle.exe\" --minimized", command);
    }

    [Fact]
    public void HotkeysViewModel_DetectsConflict()
    {
        string root = Path.Combine(Path.GetTempPath(), "reticle-hotkeys-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var state = new AppState(new LibraryStore(root), new SettingsStore(root), () => 0);
            using var vm = new HotkeysViewModel(state);
            var key = new KeyBinding(InputKind.Keyboard, 0x70);
            vm.SetBinding(HotkeyAction.ToggleOverlay, key);
            vm.SetBinding(HotkeyAction.NextCrosshair, key);
            Assert.True(vm.HasConflict);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
