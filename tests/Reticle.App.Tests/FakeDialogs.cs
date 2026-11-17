using Reticle.App.Services;

namespace Reticle.App.Tests;

/// <summary>Scripted dialog answers; records shown messages.</summary>
internal sealed class FakeDialogs : IDialogService
{
    public bool ConfirmAnswer { get; set; } = true;
    public string? PromptAnswer { get; set; }
    public string? OpenFileAnswer { get; set; }
    public string? SaveFileAnswer { get; set; }
    public ImportOutcome? ImportAnswer { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Infos { get; } = new();
    public string? Clipboard { get; private set; }

    public bool Confirm(string message) => ConfirmAnswer;
    public void ShowError(string message) => Errors.Add(message);
    public void ShowInfo(string message) => Infos.Add(message);
    public string? PromptText(string title, string label, string initial) => PromptAnswer;
    public string? OpenFile(string filter) => OpenFileAnswer;
    public string? SaveFile(string filter, string defaultFileName) => SaveFileAnswer;
    public ImportOutcome? ShowImportDialog() => ImportAnswer;
    public void SetClipboardText(string text) => Clipboard = text;
}
