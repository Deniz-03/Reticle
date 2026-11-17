using Reticle.Core.Model;

namespace Reticle.Core.Hotkeys;

/// <summary>Raw input event. Keyboard Code = Win32 VK; mouse codes: 1=Left 2=Right 3=Middle 4=X1 5=X2.</summary>
public sealed record InputEvent(InputKind Kind, int Code, bool Down);

public abstract record OverlayCommand;

public sealed record ToggleOverlay : OverlayCommand;
public sealed record Cycle(int Delta) : OverlayCommand;
public sealed record SelectCrosshair(string Id) : OverlayCommand;
public sealed record SetHidden(bool Hidden) : OverlayCommand;
public sealed record CenterOffset : OverlayCommand;
public sealed record ScaleStep(int Delta) : OverlayCommand;
public sealed record AnimationTrigger(InputKind Kind, int Code, bool Down) : OverlayCommand;
