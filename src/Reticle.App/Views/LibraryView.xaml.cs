using System.Windows.Controls;
using System.Windows.Input;
using Reticle.App.ViewModels;

namespace Reticle.App.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    /// <summary>Keeps a newly imported or duplicated crosshair visible.</summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is { } item) list.ScrollIntoView(item);
    }

    /// <summary>LB-4: double-clicking a card makes it the active crosshair.</summary>
    private void OnCardDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: CrosshairCardViewModel card } && DataContext is LibraryViewModel vm
            && vm.ActivateCommand.CanExecute(card))
        {
            vm.ActivateCommand.Execute(card);
            e.Handled = true;
        }
    }
}
