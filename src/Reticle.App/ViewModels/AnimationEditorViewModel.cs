using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

/// <summary>
/// AN-1..3: trigger, mode and keyframes of the selected layer's animation. Keyframe edits always replace the
/// whole keyframe list (imported docs may share keyframe arrays).
/// </summary>
public sealed partial class AnimationEditorViewModel : ObservableObject
{
    private readonly LayerEditorViewModel _owner;

    public AnimationEditorViewModel(LayerEditorViewModel owner)
    {
        _owner = owner;
        SyncKeyframes();
    }

    public static IReadOnlyList<Choice<AnimationMode>> Modes { get; } = Labels.Of<AnimationMode>(Labels.Mode);
    public static IReadOnlyList<Choice<Easing>> Easings { get; } = Labels.Of<Easing>(Labels.Easing);

    /// <summary>Left click, once: a small spread kick.</summary>
    public static LayerAnimation Default { get; } = new(
        new InputTrigger(InputKind.Mouse, 1), AnimationMode.Once,
        [new Keyframe(80, Easing.EaseOut, GapAdd: 3)]);

    private LayerAnimation? Anim => _owner.Layer.Animation;

    public ObservableCollection<KeyframeViewModel> Keyframes { get; } = new();

    [ObservableProperty]
    private bool _isCapturing;

    public bool Enabled
    {
        get => Anim is not null;
        set
        {
            if (value == Enabled) return;
            Set(value ? Default : null);
        }
    }

    public string TriggerText => IsCapturing ? Resources.Strings.PressKeyOrButton : Labels.Trigger(Anim?.Trigger);

    public AnimationMode Mode
    {
        get => Anim?.Mode ?? AnimationMode.Once;
        set
        {
            if (Anim is { } a) Set(a with { Mode = value });
        }
    }

    public bool CanRemoveKeyframe => Anim is { Keyframes.Count: > 1 };

    partial void OnIsCapturingChanged(bool value) => OnPropertyChanged(nameof(TriggerText));

    /// <summary>Sets the captured trigger and ends capture mode.</summary>
    public void SetTrigger(InputTrigger trigger)
    {
        IsCapturing = false;
        if (Anim is { } a) Set(a with { Trigger = trigger });
    }

    [RelayCommand]
    private void AddKeyframe()
    {
        if (Anim is not { } a) return;
        var last = a.Keyframes.Count > 0 ? a.Keyframes[^1] : new Keyframe(100);
        Set(a with { Keyframes = [.. a.Keyframes, last] }, "keyframes");
    }

    [RelayCommand]
    private void RemoveKeyframe(KeyframeViewModel? keyframe)
    {
        if (Anim is not { } a || keyframe is null || a.Keyframes.Count <= 1) return;
        var list = a.Keyframes.ToList();
        list.RemoveAt(keyframe.Index);
        Set(a with { Keyframes = list }, "keyframes");
    }

    internal void SetKeyframe(int index, Keyframe keyframe, string property)
    {
        if (Anim is not { } a || index < 0 || index >= a.Keyframes.Count) return;
        var list = a.Keyframes.ToArray(); // copy: never write into a shared list
        list[index] = keyframe;
        Set(a with { Keyframes = list }, $"kf{index}.{property}");
    }

    internal void Refresh()
    {
        SyncKeyframes();
        OnPropertyChanged(string.Empty);
    }

    private void Set(LayerAnimation? animation, string property = "animation")
        => _owner.Commit(_owner.Layer with { Animation = animation }, property);

    private void SyncKeyframes()
    {
        var frames = Anim?.Keyframes ?? [];
        if (frames.Count == Keyframes.Count)
        {
            for (int i = 0; i < frames.Count; i++) Keyframes[i].Update(frames[i]);
            return;
        }
        Keyframes.Clear();
        for (int i = 0; i < frames.Count; i++) Keyframes.Add(new KeyframeViewModel(this, i, frames[i]));
    }
}

/// <summary>One animation stage (AN-2/AN-3): duration, easing and the animated deltas.</summary>
public sealed class KeyframeViewModel : ObservableObject
{
    private readonly AnimationEditorViewModel _owner;
    private Keyframe _frame;

    public KeyframeViewModel(AnimationEditorViewModel owner, int index, Keyframe frame)
    {
        _owner = owner;
        Index = index;
        _frame = frame;
    }

    public int Index { get; }
    public string Title => string.Format(Resources.Strings.KeyframeTitle, Index + 1);

    public double DurationMs { get => _frame.DurationMs; set => Set(_frame with { DurationMs = Math.Clamp(Math.Round(value), 1, 5000) }); }
    public Easing Easing { get => _frame.Easing; set => Set(_frame with { Easing = value }); }
    public double GapAdd { get => _frame.GapAdd; set => Set(_frame with { GapAdd = Math.Clamp(value, -50, 50) }); }
    public double ScaleMul { get => _frame.ScaleMul; set => Set(_frame with { ScaleMul = Math.Clamp(value, 0, 8) }); }
    public double RotationAdd { get => _frame.RotationAdd; set => Set(_frame with { RotationAdd = Math.Clamp(value, -360, 360) }); }
    public double OpacityMul { get => _frame.OpacityMul; set => Set(_frame with { OpacityMul = Math.Clamp(value, 0, 1) }); }

    public bool HasColor
    {
        get => _frame.Color is not null;
        set => Set(_frame with { Color = value ? _frame.Color ?? new Rgba(255, 255, 255, 255) : null });
    }

    public string ColorHex
    {
        get => _frame.Color is { } c ? ColorText.Format(c) : "";
        set
        {
            if (_frame.Color is { } c) Set(_frame with { Color = ColorText.ParseOr(value, c) });
            OnPropertyChanged();
        }
    }

    internal void Update(Keyframe frame)
    {
        if (ReferenceEquals(frame, _frame)) return;
        _frame = frame;
        OnPropertyChanged(string.Empty);
    }

    private void Set(Keyframe next, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        if (next.Equals(_frame)) return;
        _owner.SetKeyframe(Index, next, property);
    }
}
