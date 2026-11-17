using System.IO;
using Reticle.App.ViewModels;
using Reticle.Core.Codecs;
using Reticle.Core.Model;
using Reticle.Core.Storage;

namespace Reticle.App.Tests;

public sealed class LibraryViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "reticle-library-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeDialogs _dialogs = new();

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private (AppState State, LibraryViewModel Library) Create()
    {
        var store = new LibraryStore(_root);
        store.Save(new CrosshairDoc(1, "a", "Alpha", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, [new ClassicLayer()]));
        var state = new AppState(store, new SettingsStore(_root), () => 0);
        return (state, new LibraryViewModel(state, _dialogs, _ => { }));
    }

    [Fact]
    public void Library_Duplicate_Rename_Delete_WithConfirmation()
    {
        var (state, library) = Create();
        using (state)
        using (library)
        {
            var alpha = library.Cards.Single();
            library.DuplicateCommand.Execute(alpha);
            var copy = library.Selected!;
            Assert.NotEqual("a", copy.Id);
            Assert.Equal(2, state.Library.Count);

            _dialogs.PromptAnswer = "  Beta ";
            library.RenameCommand.Execute(copy);
            Assert.Equal("Beta", state.Library.Single(d => d.Id == copy.Id).Name);

            _dialogs.ConfirmAnswer = false;
            library.DeleteCommand.Execute(copy);
            Assert.Equal(2, state.Library.Count);

            _dialogs.ConfirmAnswer = true;
            library.ActivateCommand.Execute(copy);
            library.DeleteCommand.Execute(copy);
            Assert.Equal("a", Assert.Single(state.Library).Id);
            Assert.Equal("a", state.Active!.Id); // deleting the active doc falls back
            Assert.True(library.Cards.Single().IsActive);
        }
    }

    [Fact]
    public void Library_CopyCode_PutsShareCodeOnClipboard()
    {
        var (state, library) = Create();
        using (state)
        using (library)
        {
            library.CopyCodeCommand.Execute(library.Cards.Single());
            var decoded = ShareCode.Decode(_dialogs.Clipboard!);
            Assert.Equal("Alpha", decoded.Doc!.Name);
        }
    }
}
