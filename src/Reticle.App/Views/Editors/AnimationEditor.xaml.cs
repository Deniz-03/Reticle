using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Reticle.App.ViewModels;
using Reticle.Core.Model;

namespace Reticle.App.Views.Editors;

/// <summary>Animation editor; the trigger is picked by pressing a key or mouse button inside the main window.</summary>
public partial class AnimationEditor : UserControl
{
    private Window? _captureWindow;

    public AnimationEditor()
    {
        InitializeComponent();
        Unloaded += (_, _) => StopCapture();
        DataContextChanged += (_, _) => StopCapture();
    }

    private AnimationEditorViewModel? ViewModel => DataContext as AnimationEditorViewModel;

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || _captureWindow is not null) return;
        _captureWindow = Window.GetWindow(this);
        if (_captureWindow is null) return;
        vm.IsCapturing = true;
        _captureWindow.PreviewKeyDown += OnCaptureKey;
        _captureWindow.PreviewMouseDown += OnCaptureMouse;
        _captureWindow.Deactivated += OnCaptureCancel;
    }

    private void OnCaptureKey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            StopCapture();
            return;
        }
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;
        var vm = ViewModel;
        StopCapture();
        vm?.SetTrigger(new InputTrigger(InputKind.Keyboard, vk));
    }

    private void OnCaptureMouse(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        int code = e.ChangedButton switch
        {
            MouseButton.Left => 1,
            MouseButton.Right => 2,
            MouseButton.Middle => 3,
            MouseButton.XButton1 => 4,
            MouseButton.XButton2 => 5,
            _ => 0,
        };
        if (code == 0) return;
        var vm = ViewModel;
        StopCapture();
        vm?.SetTrigger(new InputTrigger(InputKind.Mouse, code));
    }

    private void OnCaptureCancel(object? sender, EventArgs e) => StopCapture();

    private void StopCapture()
    {
        if (_captureWindow is { } w)
        {
            w.PreviewKeyDown -= OnCaptureKey;
            w.PreviewMouseDown -= OnCaptureMouse;
            w.Deactivated -= OnCaptureCancel;
            _captureWindow = null;
        }
        if (ViewModel is { IsCapturing: true } vm) vm.IsCapturing = false;
    }
}
