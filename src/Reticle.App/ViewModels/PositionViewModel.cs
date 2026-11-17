using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reticle.App.Overlay;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

public sealed partial class PositionViewModel : ObservableObject
{
    private readonly AppState _state;
    public PositionViewModel(AppState state, MonitorService monitors)
    {
        _state = state;
        Monitors = monitors.GetAll();
        _state.Changed += Refresh;
    }
    public IReadOnlyList<MonitorInfo> Monitors { get; }
    public MonitorInfo? SelectedMonitor { get => MonitorService.Pick(Monitors, _state.Settings.MonitorDeviceName); set { if (value is not null) _state.Update(s => s with { MonitorDeviceName = value.DeviceName }); OnPropertyChanged(); } }
    public double OffsetX { get => _state.Settings.Offset.X; set => _state.Update(s => s with { Offset = new Vec2(value, s.Offset.Y) }); }
    public double OffsetY { get => _state.Settings.Offset.Y; set => _state.Update(s => s with { Offset = new Vec2(s.Offset.X, value) }); }
    public double GlobalScale { get => _state.Settings.GlobalScale; set => _state.Update(s => s with { GlobalScale = Math.Clamp(Math.Round(value / .05) * .05, .25, 4) }); }
    [RelayCommand] private void Center() => _state.Update(s => s with { Offset = new Vec2(0, 0) });
    private void Refresh() { OnPropertyChanged(nameof(SelectedMonitor)); OnPropertyChanged(nameof(OffsetX)); OnPropertyChanged(nameof(OffsetY)); OnPropertyChanged(nameof(GlobalScale)); }
}
