using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reticle.App.Services;
using Reticle.Core.Storage;

namespace Reticle.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppState _state;
    public SettingsViewModel(AppState state) { _state = state; _state.Changed += () => OnPropertyChanged(nameof(LaunchOnStartup)); }
    public bool LaunchOnStartup { get => _state.Settings.LaunchOnStartup; set { AutostartService.SetEnabled(value, Environment.ProcessPath!); _state.Update(s => s with { LaunchOnStartup = value }); } }
    public bool OverlayEnabled { get => _state.Settings.OverlayEnabled; set => _state.Update(s => s with { OverlayEnabled = value }); }
    public string? Language { get => _state.Settings.Language; set => _state.Update(s => s with { Language = value }); }
    [RelayCommand] private void OpenDataFolder() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LibraryStore.DefaultRoot) { UseShellExecute = true });
    [RelayCommand] private void RestartAsAdministrator() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
}
