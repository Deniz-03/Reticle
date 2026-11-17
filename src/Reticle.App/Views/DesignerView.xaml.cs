using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Reticle.App.ViewModels;

namespace Reticle.App.Views;

public partial class DesignerView : UserControl
{
    private const string DragFormat = "Reticle.LayerId";
    private Point? _dragStart;

    public DesignerView()
    {
        InitializeComponent();
    }

    private DesignerViewModel? ViewModel => DataContext as DesignerViewModel;

    private void OnPreviewWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        vm.Zoom = Math.Clamp(vm.Zoom + Math.Sign(e.Delta), DesignerViewModel.MinZoom, DesignerViewModel.MaxZoom);
        e.Handled = true;
    }

    private void OnLayerDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IsFromToggle(e.OriginalSource)) return;
        if (ViewModel?.RenameLayerCommand is { } cmd && cmd.CanExecute(null)) cmd.Execute(null);
        e.Handled = true;
    }

    // DE-2: drag a layer row onto another row to reorder.
    private void OnLayerMouseDown(object sender, MouseButtonEventArgs e)
        => _dragStart = IsFromToggle(e.OriginalSource) ? null : e.GetPosition(LayerList);

    private void OnLayerMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed
            || sender is not ListBoxItem { DataContext: LayerItemViewModel item } row) return;
        var delta = e.GetPosition(LayerList) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragStart = null;
        DragDrop.DoDragDrop(row, new DataObject(DragFormat, item.Id), DragDropEffects.Move);
    }

    private void OnLayerDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm || e.Data.GetData(DragFormat) is not string id
            || sender is not ListBoxItem { DataContext: LayerItemViewModel target }) return;
        vm.MoveLayer(id, vm.Layers.IndexOf(target));
        e.Handled = true;
    }

    private static bool IsFromToggle(object source)
    {
        for (var d = source as DependencyObject; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : null)
            if (d is System.Windows.Controls.Primitives.ToggleButton) return true;
        return false;
    }
}
