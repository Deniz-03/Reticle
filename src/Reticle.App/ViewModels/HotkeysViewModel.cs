using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reticle.Core.Hotkeys;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

public sealed partial class HotkeyRowViewModel : ObservableObject
{
    public HotkeyRowViewModel(HotkeyBinding binding) { Action = binding.Action; CrosshairId = binding.CrosshairId; Binding = binding.Binding; }
    public HotkeyAction Action { get; }
    public string? CrosshairId { get; }
    [ObservableProperty] private KeyBinding? _binding;
    public string Label => CrosshairId is null ? Action.ToString() : $"Crosshair: {CrosshairId}";
    public string BindingText => Binding is null ? "Keine Aktion" : HotkeysViewModel.Format(Binding);
    partial void OnBindingChanged(KeyBinding? value) => OnPropertyChanged(nameof(BindingText));
}

public sealed partial class HotkeysViewModel : ObservableObject, IDisposable
{
    private readonly AppState _state;
    private Modifiers _heldModifiers;
    private HotkeyRowViewModel? _capturing;
    public HotkeysViewModel(AppState state) { _state = state; Refresh(); _state.Changed += Refresh; }
    public ObservableCollection<HotkeyRowViewModel> Bindings { get; } = new();
    public bool HideOnRightClick { get => _state.Settings.HideOnRightClick; set => _state.Update(s => s with { HideOnRightClick = value }); }
    public bool HasConflict => Bindings.Where(x => x.Binding is not null).GroupBy(x => x.Binding).Any(g => g.Count() > 1);
    public string CaptureHint => _capturing is null ? "Taste oder Maustaste auswählen" : "Taste oder Maustaste drücken … (Esc: abbrechen, Rücktaste: löschen)";
    [RelayCommand] private void Capture(HotkeyRowViewModel? row) { _capturing = row; OnPropertyChanged(nameof(CaptureHint)); }
    [RelayCommand] private void AddCrosshairBinding()
    {
        var doc = _state.Active ?? _state.Library.FirstOrDefault();
        if (doc is null || Bindings.Any(x => x.Action == HotkeyAction.SelectCrosshair && x.CrosshairId == doc.Id)) return;
        SetBinding(HotkeyAction.SelectCrosshair, null, doc.Id);
    }
    public void ProcessInput(InputEvent e)
    {
        if (e.Kind == InputKind.Keyboard && ModifierOf(e.Code) is { } mod && mod != Modifiers.None) { _heldModifiers = e.Down ? _heldModifiers | mod : _heldModifiers & ~mod; return; }
        if (!e.Down || _capturing is null) return;
        if (e.Kind == InputKind.Keyboard && e.Code == 0x1B) { _capturing = null; OnPropertyChanged(nameof(CaptureHint)); return; }
        var binding = e.Kind == InputKind.Keyboard && e.Code == 0x08 ? null : new KeyBinding(e.Kind, e.Code, _heldModifiers);
        var row = _capturing; _capturing = null; SetBinding(row.Action, binding, row.CrosshairId); OnPropertyChanged(nameof(CaptureHint));
    }
    public void SetBinding(HotkeyAction action, KeyBinding? binding, string? crosshairId = null)
    {
        _state.Update(s => s with { Hotkeys = s.Hotkeys.Where(x => x.Action != action || x.CrosshairId != crosshairId).Append(new HotkeyBinding(action, binding, crosshairId)).ToArray() });
    }
    internal static string Format(KeyBinding binding) => (binding.Mods == Modifiers.None ? "" : binding.Mods + "+") + (binding.Kind == InputKind.Mouse ? $"Maustaste {binding.Code}" : $"VK {binding.Code}");
    private static Modifiers ModifierOf(int vk) => vk switch { 0x10 or 0xA0 or 0xA1 => Modifiers.Shift, 0x11 or 0xA2 or 0xA3 => Modifiers.Ctrl, 0x12 or 0xA4 or 0xA5 => Modifiers.Alt, 0x5B or 0x5C => Modifiers.Win, _ => Modifiers.None };
    private void Refresh() { Bindings.Clear(); foreach (var binding in _state.Settings.Hotkeys) Bindings.Add(new HotkeyRowViewModel(binding)); OnPropertyChanged(nameof(HideOnRightClick)); OnPropertyChanged(nameof(HasConflict)); }
    public void Dispose() => _state.Changed -= Refresh;
}
