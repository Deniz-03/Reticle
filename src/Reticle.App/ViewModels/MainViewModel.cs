using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reticle.App.Resources;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

public enum AppPage { Library, Designer, Position, Hotkeys, Settings }

/// <param name="Glyph">Segoe Fluent Icons glyph.</param>
public sealed record NavItem(AppPage Page, string Title, string Glyph);

/// <summary>A page that is not implemented yet (filled in by later tasks).</summary>
public sealed record PlaceholderViewModel(string Title);

/// <summary>Navigation shell: left nav with Library, Designer, Position &amp; Monitor, Hotkeys, Settings.</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<AppPage, object> _pages = new();

    public MainViewModel(LibraryViewModelFactory library, DesignerViewModel designer, PositionViewModel position, HotkeysViewModel hotkeys, SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(designer);
        Designer = designer;
        Library = library(OpenInDesigner);
        NavItems =
        [
            new(AppPage.Library, Strings.NavLibrary, ""),
            new(AppPage.Designer, Strings.NavDesigner, ""),
            new(AppPage.Position, Strings.NavPosition, ""),
            new(AppPage.Hotkeys, Strings.NavHotkeys, ""),
            new(AppPage.Settings, Strings.NavSettings, ""),
        ];
        _pages[AppPage.Library] = Library;
        _pages[AppPage.Designer] = Designer;
        _pages[AppPage.Position] = position;
        _pages[AppPage.Hotkeys] = hotkeys;
        _pages[AppPage.Settings] = settings;
        _selectedNav = NavItems[0];
        _currentPage = Library;
    }

    public LibraryViewModel Library { get; }
    public DesignerViewModel Designer { get; }
    public HotkeysViewModel Hotkeys => (HotkeysViewModel)_pages[AppPage.Hotkeys];
    public IReadOnlyList<NavItem> NavItems { get; }

    [ObservableProperty]
    private NavItem _selectedNav;

    [ObservableProperty]
    private object _currentPage;

    [ObservableProperty]
    private bool _elevationWarning;

    /// <summary>Lets a later task replace a placeholder page with its view model.</summary>
    public void SetPage(AppPage page, object viewModel)
    {
        _pages[page] = viewModel;
        if (SelectedNav.Page == page) CurrentPage = viewModel;
    }

    public void Navigate(AppPage page) => SelectedNav = NavItems.First(n => n.Page == page);

    partial void OnSelectedNavChanged(NavItem? oldValue, NavItem newValue)
    {
        if (oldValue?.Page == AppPage.Designer) Designer.FlushSave();
        if (newValue.Page == AppPage.Designer) Designer.EnsureDoc();
        CurrentPage = _pages[newValue.Page];
    }

    private void OpenInDesigner(CrosshairDoc doc)
    {
        Designer.Open(doc);
        Navigate(AppPage.Designer);
    }

    /// <summary>Ctrl+Z / Ctrl+Y act on the designer only while it is shown.</summary>
    [RelayCommand]
    private void Undo()
    {
        if (SelectedNav.Page == AppPage.Designer && Designer.UndoCommand.CanExecute(null)) Designer.UndoCommand.Execute(null);
    }

    [RelayCommand]
    private void Redo()
    {
        if (SelectedNav.Page == AppPage.Designer && Designer.RedoCommand.CanExecute(null)) Designer.RedoCommand.Execute(null);
    }

    public void Dispose()
    {
        Designer.Dispose();
        Library.Dispose();
        Hotkeys.Dispose();
    }
}

/// <summary>Creates the library view model given the "open in designer" callback.</summary>
public delegate LibraryViewModel LibraryViewModelFactory(Action<CrosshairDoc> edit);
