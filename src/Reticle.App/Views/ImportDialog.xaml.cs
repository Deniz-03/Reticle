using System.Windows;
using Reticle.App.ViewModels;

namespace Reticle.App.Views;

/// <summary>Paste a Reticle share code or a Valorant crosshair code (format detected automatically).</summary>
public partial class ImportDialog : Window
{
    public ImportDialog()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        DataContext = ViewModel;
        Loaded += (_, _) => CodeBox.Focus();
    }

    public ImportDialogViewModel ViewModel { get; } = new();

    private void OnImport(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanImport) return;
        DialogResult = true;
    }
}
