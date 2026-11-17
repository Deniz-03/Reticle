using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reticle.App.Rendering;
using Reticle.App.Resources;
using Reticle.App.Services;
using Reticle.Core.Codecs;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

/// <summary>Crosshair library (LB-1..4) with share code, file and PNG export/import (SH-1..3, IM-1).</summary>
public sealed partial class LibraryViewModel : ObservableObject, IDisposable
{
    public const int PngExportPx = 256;

    private readonly AppState _state;
    private readonly IDialogService _dialogs;
    private readonly Action<CrosshairDoc> _edit;
    private readonly Func<DateTimeOffset> _now;
    private readonly LayerDrawer _drawer = new();

    /// <param name="edit">Opens a crosshair in the designer.</param>
    public LibraryViewModel(AppState state, IDialogService dialogs, Action<CrosshairDoc> edit, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(edit);
        _state = state;
        _dialogs = dialogs;
        _edit = edit;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _state.Changed += Sync;
        Sync();
    }

    public ObservableCollection<CrosshairCardViewModel> Cards { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivateCommand), nameof(EditCommand), nameof(DuplicateCommand), nameof(RenameCommand),
        nameof(DeleteCommand), nameof(CopyCodeCommand), nameof(ExportFileCommand), nameof(ExportPngCommand))]
    private CrosshairCardViewModel? _selected;

    /// <summary>Short feedback line ("Code copied", ...).</summary>
    [ObservableProperty]
    private string? _status;

    public void Dispose() => _state.Changed -= Sync;

    private CrosshairCardViewModel? Target(CrosshairCardViewModel? card) => card ?? Selected;

    private bool HasTarget(CrosshairCardViewModel? card) => Target(card) is not null;

    [RelayCommand(CanExecute = nameof(HasTarget))]
    private void Activate(CrosshairCardViewModel? card)
    {
        if (Target(card) is not { } c) return;
        _state.Update(s => s with { ActiveCrosshairId = c.Id });
    }

    [RelayCommand(CanExecute = nameof(HasTarget))]
    private void Edit(CrosshairCardViewModel? card)
    {
        if (Target(card) is { } c) _edit(c.Doc);
    }

    [RelayCommand]
    private void New()
    {
        var now = _now();
        var doc = new CrosshairDoc(CrosshairDoc.CurrentSchema, Guid.NewGuid().ToString("N"), Strings.NewCrosshairName, now, now, 1080,
            [new ClassicLayer { Name = Strings.LayerTypeClassic }]);
        if (Save(doc)) _edit(doc);
    }

    [RelayCommand(CanExecute = nameof(HasTarget))]
    private void Duplicate(CrosshairCardViewModel? card)
    {
        if (Target(card) is not { } c) return;
        var now = _now();
        Save(c.Doc with
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.Format(Strings.CopyNameFormat, c.Doc.Name),
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    [RelayCommand(CanExecute = nameof(HasTarget))]
    private void Rename(CrosshairCardViewModel? card)
    {
        if (Target(card) is not { } c) return;
        var name = _dialogs.PromptText(Strings.RenameCrosshairTitle, Strings.NameLabel, c.Doc.Name)?.Trim();
        if (string.IsNullOrEmpty(name) || name == c.Doc.Name) return;
        Save(c.Doc with { Name = name, UpdatedAt = _now() });
    }

    [RelayCommand(CanExecute = nameof(HasTarget))]
    private void Delete(CrosshairCardViewModel? card)
    {
        if (Target(card) is not { } c) return;
        if (!_dialogs.Confirm(string.Format(Strings.ConfirmDelete, c.Name))) return;
        try
        {
            _state.DeleteDoc(c.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(string.Format(Strings.ErrorDelete, ex.Message));
        }
    }

    [RelayCommand]
    private void Import()
    {
        if (_dialogs.ShowImportDialog() is not { } outcome) return;
        if (Save(outcome.Doc) && outcome.Warning is { } warning) _dialogs.ShowInfo(warning);
    }

    [RelayCommand(CanExecute = nameof(HasTarget))]
    private void CopyCode(CrosshairCardViewModel? card)
    {
        if (Target(card) is not { } c) return;
        _dialogs.SetClipboardText(ShareCode.Encode(c.Doc));
        Status = string.Format(Strings.StatusCodeCopied, c.Name);
    }

    [RelayCommand(CanExecute = nameof(HasTarget))]
    private void ExportFile(CrosshairCardViewModel? card)
    {
        if (Target(card) is not { } c) return;
        var path = _dialogs.SaveFile(JsonFilter, CrosshairFiles.SuggestFileName(c.Name, CrosshairFiles.Extension));
        if (path is null) return;
        try
        {
            CrosshairFiles.Write(path, c.Doc);
            Status = string.Format(Strings.StatusExported, Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(string.Format(Strings.ErrorFileWrite, ex.Message));
        }
    }

    [RelayCommand]
    private void ImportFile()
    {
        var path = _dialogs.OpenFile(JsonFilter);
        if (path is null) return;
        var result = CrosshairFiles.Read(path);
        if (result.Doc is null) _dialogs.ShowError(result.Error ?? Strings.ErrorFileInvalid);
        else Save(result.Doc);
    }

    [RelayCommand(CanExecute = nameof(HasTarget))]
    private void ExportPng(CrosshairCardViewModel? card)
    {
        if (Target(card) is not { } c) return;
        var path = _dialogs.SaveFile($"PNG (*.png)|*.png", CrosshairFiles.SuggestFileName(c.Name, ".png"));
        if (path is null) return;
        try
        {
            var bitmap = CrosshairRasterizer.Render(c.Doc, PngExportPx, PngExportPx, CrosshairRasterizer.NominalSizeFactor(c.Doc), _drawer);
            File.WriteAllBytes(path, CrosshairRasterizer.EncodePng(bitmap));
            Status = string.Format(Strings.StatusExported, Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(string.Format(Strings.ErrorFileWrite, ex.Message));
        }
    }

    private static string JsonFilter => $"{Strings.FileFilterCrosshair} (*{CrosshairFiles.Extension})|*{CrosshairFiles.Extension}";

    /// <summary>Saves and selects <paramref name="doc"/>; reports failures.</summary>
    private bool Save(CrosshairDoc doc)
    {
        try
        {
            _state.SaveDoc(doc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(string.Format(Strings.ErrorSave, ex.Message));
            return false;
        }
        Selected = Cards.FirstOrDefault(c => c.Id == doc.Id) ?? Selected;
        return true;
    }

    /// <summary>Mirrors <see cref="AppState.Library"/>, reusing cards (and their thumbnails) of unchanged docs.</summary>
    private void Sync()
    {
        var library = _state.Library;
        string? selectedId = Selected?.Id;
        string? activeId = _state.Settings.ActiveCrosshairId;
        if (!library.Select(d => d.Id).SequenceEqual(Cards.Select(c => c.Id)))
        {
            var existing = Cards.ToDictionary(c => c.Id);
            Cards.Clear();
            foreach (var doc in library)
                Cards.Add(existing.TryGetValue(doc.Id, out var card) ? card : new CrosshairCardViewModel(doc, _drawer));
        }
        for (int i = 0; i < library.Count; i++)
        {
            Cards[i].Update(library[i]);
            Cards[i].IsActive = Cards[i].Id == activeId;
        }
        Selected = Cards.FirstOrDefault(c => c.Id == selectedId);
    }
}
