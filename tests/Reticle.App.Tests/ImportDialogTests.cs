using System.IO;
using Reticle.App.Services;
using Reticle.App.ViewModels;
using Reticle.Core.Codecs;
using Reticle.Core.Model;
using Reticle.Core.Storage;

namespace Reticle.App.Tests;

public sealed class ImportDialogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "reticle-import-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    [Fact]
    public void ImportDialog_DetectsFormat()
    {
        var vm = new ImportDialogViewModel();
        Assert.Null(vm.Result);
        Assert.False(vm.CanImport);

        // Acceptance criterion 9: cyan cross, length 4, gap 2, no outline, no outer lines.
        vm.Text = "  0;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0 ";
        Assert.True(vm.CanImport);
        Assert.Null(vm.Error);
        Assert.Equal(Resources.Strings.FormatValorant, vm.FormatLabel);
        var cross = Assert.IsType<ClassicLayer>(Assert.Single(vm.Result!.Doc!.Layers));
        Assert.Equal(new Rgba(0, 255, 255, 255), cross.Color);
        Assert.Equal((4, 2), (cross.Length, cross.Gap));
        Assert.True(cross.Top && cross.Bottom && cross.Left && cross.Right);
        Assert.False(cross.Outline.Enabled);
        Assert.Equal("inner", cross.Name);

        // Reticle share code round trip.
        var original = new CrosshairDoc(1, "x", "Shared", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null,
            [new RadialLayer { Arms = 3, Gap = 5 }]);
        vm.Text = ShareCode.Encode(original);
        Assert.True(vm.CanImport);
        Assert.Equal(Resources.Strings.FormatShareCode, vm.FormatLabel);
        Assert.Equal("Shared", vm.Result!.Doc!.Name);
        Assert.NotEqual("x", vm.Result.Doc.Id);
        Assert.Equal(3, Assert.IsType<RadialLayer>(Assert.Single(vm.Result.Doc.Layers)).Arms);

        // Damaged share code: the decoder's error is shown.
        vm.Text = "RTCL1.@@@";
        Assert.False(vm.CanImport);
        Assert.NotNull(vm.Error);

        // Anything else: unknown format.
        vm.Text = "hello";
        Assert.False(vm.CanImport);
        Assert.Equal(Resources.Strings.ErrorUnknownCodeFormat, vm.Error);
    }

    [Fact]
    public void ImportDialog_ShowsValorantWarning()
    {
        var vm = new ImportDialogViewModel { Text = "0;P;0g;1;0v;9;0l;4;1b;0" };
        Assert.True(vm.CanImport);
        Assert.NotNull(vm.Warning);
    }

    [Fact]
    public void Library_Import_SavesAsNewDocAndSelectsIt()
    {
        using var state = new AppState(new LibraryStore(_root), new SettingsStore(_root), () => 0);
        int before = state.Library.Count;
        var imported = ValorantCode.Parse("0;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0").Doc!;
        var dialogs = new FakeDialogs { ImportAnswer = new ImportOutcome(imported, null) };
        using var library = new LibraryViewModel(state, dialogs, _ => { });

        library.ImportCommand.Execute(null);

        Assert.Equal(before + 1, state.Library.Count);
        Assert.Equal(imported.Id, library.Selected?.Id);
        Assert.Contains(library.Cards, c => c.Id == imported.Id);
    }

    [Fact]
    public void CrosshairFiles_RoundTrip_AssignsNewId_AndReportsErrors()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "x" + CrosshairFiles.Extension);
        var doc = new CrosshairDoc(1, "orig", "File", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1080,
            [new ShapeLayer { Kind = ShapeKind.Ring, StrokeWidth = 2 }]);
        CrosshairFiles.Write(path, doc);

        var result = CrosshairFiles.Read(path);
        Assert.Null(result.Error);
        Assert.NotEqual("orig", result.Doc!.Id);
        Assert.Equal(doc.Layers, result.Doc.Layers);

        File.WriteAllText(path, "{ not json");
        var bad = CrosshairFiles.Read(path);
        Assert.Null(bad.Doc);
        Assert.NotNull(bad.Error);
    }
}
