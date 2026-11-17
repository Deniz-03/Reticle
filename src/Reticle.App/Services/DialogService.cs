using System.Windows;
using Microsoft.Win32;
using Reticle.App.Resources;
using Reticle.App.Views;

namespace Reticle.App.Services;

/// <summary>WPF implementation of <see cref="IDialogService"/>; dialogs are owned by the active app window.</summary>
public sealed class DialogService : IDialogService
{
    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;

    public bool Confirm(string message)
        => Show(message, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowError(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Information);

    public string? PromptText(string title, string label, string initial)
    {
        var dialog = new TextPromptDialog(title, label, initial) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    public string? OpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? SaveFile(string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = defaultFileName, OverwritePrompt = true, AddExtension = false };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public ImportOutcome? ShowImportDialog()
    {
        var dialog = new ImportDialog { Owner = Owner };
        if (dialog.ShowDialog() != true || dialog.ViewModel.Result is not { Doc: { } doc } result) return null;
        return new ImportOutcome(doc, result.Warning);
    }

    public void SetClipboardText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.ExternalException ex) // clipboard held by another process
        {
            ShowError(ex.Message);
        }
    }

    private static MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage icon)
        => Owner is { } owner
            ? MessageBox.Show(owner, message, Strings.AppTitle, buttons, icon)
            : MessageBox.Show(message, Strings.AppTitle, buttons, icon);
}
