using System.IO;
using Reticle.App.ViewModels;
using Reticle.Core.Codecs;
using Reticle.Core.Model;
using Reticle.Core.Storage;

namespace Reticle.App.Tests;

public sealed class DesignerViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "reticle-designer-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeDialogs _dialogs = new();
    private double _clock;
    private AppState? _state;
    private DesignerViewModel? _designer;

    public void Dispose()
    {
        _designer?.Dispose();
        _state?.Dispose();
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private static CrosshairDoc Doc(string id, params Layer[] layers) => new(
        1, id, "Doc " + id, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, layers);

    private DesignerViewModel Create(params CrosshairDoc[] docs)
    {
        var store = new LibraryStore(_root);
        foreach (var d in docs) store.Save(d);
        _state = new AppState(store, new SettingsStore(_root), () => 0);
        _designer = new DesignerViewModel(_state, _dialogs, clockMs: () => _clock);
        _designer.Open(_state.Library.First(d => d.Id == docs[0].Id));
        return _designer;
    }

    private static ClassicLayer Classic(DesignerViewModel d) => (ClassicLayer)d.Doc!.Layers.Single();

    [Fact]
    public void DesignerViewModel_UndoRedo_RestoresDoc()
    {
        var d = Create(Doc("a", new ClassicLayer { Id = "l", Thickness = 2, Gap = 3 }));
        var original = d.Doc!;
        var editor = Assert.IsType<ClassicEditorViewModel>(d.Editor);

        // A slider drag: many edits of one property in quick succession form one undo step.
        editor.Thickness = 3;
        _clock += 40;
        editor.Thickness = 4;
        _clock += 40;
        editor.Thickness = 5;
        var afterThickness = d.Doc!;
        _clock += 1000;
        editor.Gap = 7;
        Assert.Equal((5, 7), (Classic(d).Thickness, Classic(d).Gap));

        d.UndoCommand.Execute(null);
        Assert.Same(afterThickness, d.Doc);
        d.UndoCommand.Execute(null);
        Assert.Same(original, d.Doc);
        Assert.False(d.CanUndo);

        d.RedoCommand.Execute(null);
        Assert.Same(afterThickness, d.Doc);
        Assert.Equal(5, ((ClassicEditorViewModel)d.Editor!).Thickness); // editor follows the restored doc
        d.RedoCommand.Execute(null);
        Assert.Equal(7, Classic(d).Gap);
        Assert.False(d.CanRedo);

        // A new edit after undo drops the redo branch.
        d.UndoCommand.Execute(null);
        _clock += 1000;
        ((ClassicEditorViewModel)d.Editor!).Length = 9;
        Assert.False(d.CanRedo);
    }

    [Fact]
    public void Designer_History_KeepsLast100Steps()
    {
        var d = Create(Doc("a", new ClassicLayer { Id = "l" }));
        for (int i = 0; i < 150; i++)
        {
            _clock += 1000;
            int n = i;
            d.Edit(doc => doc with { Name = "n" + n });
        }
        int undos = 0;
        while (d.CanUndo)
        {
            d.UndoCommand.Execute(null);
            undos++;
        }
        Assert.Equal(DesignerViewModel.HistoryLimit, undos);
        Assert.Equal("n49", d.Doc!.Name);
    }

    [Fact]
    public void Designer_FlushSave_UpdatesLibraryAndActiveOverlayDoc()
    {
        var d = Create(Doc("a", new ClassicLayer { Id = "l", Thickness = 2 }), Doc("b", new ClassicLayer { Id = "m" }));
        Assert.Equal("a", _state!.Active!.Id);
        ((ClassicEditorViewModel)d.Editor!).Thickness = 6;
        Assert.Equal(2, ((ClassicLayer)_state.Active!.Layers[0]).Thickness); // debounced, not saved yet

        d.FlushSave();
        Assert.Equal(6, ((ClassicLayer)_state.Active!.Layers[0]).Thickness);
        var reloaded = new LibraryStore(_root).LoadAll().Single(x => x.Id == "a");
        Assert.Equal(6, ((ClassicLayer)reloaded.Layers[0]).Thickness);
    }

    [Fact]
    public void Designer_PropertyPanel_ClampsInvalidInput()
    {
        var d = Create(Doc("a",
            new ClassicLayer { Id = "c" },
            new RadialLayer { Id = "r" },
            new ShapeLayer { Id = "s", Kind = ShapeKind.Chevron, Height = 4, StrokeWidth = 1 }));

        d.SelectedLayer = d.Layers.Single(l => l.Id == "c");
        var classic = (ClassicEditorViewModel)d.Editor!;
        classic.Length = 0;
        classic.Thickness = -3;
        classic.Opacity = 1.7;
        Assert.Equal((1.0, 1.0, 1.0), (classic.Length, classic.Thickness, classic.Opacity));

        d.SelectedLayer = d.Layers.Single(l => l.Id == "r");
        var radial = (RadialEditorViewModel)d.Editor!;
        radial.Arms = 12;
        Assert.Equal(8, radial.Arms);
        radial.Arms = 1;
        Assert.Equal(2, radial.Arms);

        d.SelectedLayer = d.Layers.Single(l => l.Id == "s");
        var shape = (ShapeEditorViewModel)d.Editor!;
        shape.StrokeWidth = 9;
        Assert.Equal(4, shape.StrokeWidth); // chevron stroke <= height
        shape.StrokeWidth = 0;
        Assert.Equal(1, shape.StrokeWidth);
        shape.StrokeWidth = 3;
        shape.Height = 2;
        Assert.Equal(2, shape.StrokeWidth);
    }

    [Fact]
    public void Designer_KeyframeEdit_ReplacesListWithoutMutatingImport()
    {
        var imported = ValorantCode.Parse("0;P;c;1;0f;1;1b;0").Doc!;
        var sharedFrames = imported.Layers[0].Animation!.Keyframes;
        var before = sharedFrames[0];
        var d = Create(imported);

        var animation = d.Editor!.Animation!;
        animation.Keyframes[0].GapAdd = 9;
        animation.AddKeyframeCommand.Execute(null);

        Assert.Same(before, sharedFrames[0]);
        Assert.Single(sharedFrames);
        var frames = d.Doc!.Layers[0].Animation!.Keyframes;
        Assert.Equal(2, frames.Count);
        Assert.Equal(9, frames[0].GapAdd);
    }

    [Fact]
    public void Designer_LayerList_AddReorderDelete()
    {
        var d = Create(Doc("a", new ClassicLayer { Id = "c", Name = "C" }));
        d.AddLayerCommand.Execute("Radial");
        Assert.Equal(2, d.Doc!.Layers.Count);
        Assert.IsType<RadialLayer>(d.Doc.Layers[1]);
        Assert.Equal(d.Doc.Layers[1].Id, d.SelectedLayer!.Id); // new layer selected, listed on top
        Assert.Equal(d.Doc.Layers[1].Id, d.Layers[0].Id);

        d.MoveLayer("c", 0); // drag "C" to the top of the list
        Assert.Equal("c", d.Doc!.Layers[^1].Id);

        d.Layers.Single(l => l.Id == "c").Visible = false;
        Assert.False(d.Doc!.Layers.Single(l => l.Id == "c").Visible);

        d.SelectedLayer = d.Layers.Single(l => l.Id == "c");
        d.DuplicateLayerCommand.Execute(null);
        Assert.Equal(3, d.Doc!.Layers.Count);
        d.DeleteLayerCommand.Execute(null);
        d.SelectedLayer = d.Layers.Single(l => l.Id == "c");
        d.DeleteLayerCommand.Execute(null);
        Assert.IsType<RadialLayer>(Assert.Single(d.Doc!.Layers));
    }
}
