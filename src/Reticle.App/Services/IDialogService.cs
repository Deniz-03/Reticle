using Reticle.Core.Model;

namespace Reticle.App.Services;

/// <summary>User interaction needed by view models (replaced by a fake in tests).</summary>
public interface IDialogService
{
    bool Confirm(string message);
    void ShowError(string message);
    void ShowInfo(string message);

    /// <returns>The entered text, or null when cancelled.</returns>
    string? PromptText(string title, string label, string initial);

    /// <returns>The chosen path, or null when cancelled.</returns>
    string? OpenFile(string filter);

    /// <returns>The chosen path, or null when cancelled.</returns>
    string? SaveFile(string filter, string defaultFileName);

    /// <returns>The imported doc, or null when cancelled.</returns>
    ImportOutcome? ShowImportDialog();

    void SetClipboardText(string text);
}

/// <summary>An accepted import plus the non-fatal warning shown for it, if any.</summary>
public sealed record ImportOutcome(CrosshairDoc Doc, string? Warning);
