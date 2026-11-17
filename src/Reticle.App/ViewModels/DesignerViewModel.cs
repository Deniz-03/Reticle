using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reticle.App.Rendering;
using Reticle.App.Resources;
using Reticle.App.Services;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

/// <summary>
/// Layer designer for one crosshair. The doc is an immutable snapshot: every edit builds a new doc (whole lists
/// replaced, never mutated), records the previous snapshot for undo (DE-11, 100 steps; rapid edits of the same
/// property coalesce into one step) and saves it debounced 150 ms through <see cref="AppState.SaveDoc"/> so the
/// overlay follows live when the doc is active (DE-1).
/// </summary>
public sealed partial class DesignerViewModel : ObservableObject, IDisposable
{
    public const int HistoryLimit = 100;
    public const int SaveDebounceMs = 150;
    /// <summary>Edits of the same property closer together than this form one undo step (slider drags).</summary>
    public const double CoalesceWindowMs = 500;
    public const int MinZoom = 1;
    public const int MaxZoom = 8;

    private readonly AppState _state;
    private readonly IDialogService _dialogs;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<double> _clockMs;
    private readonly Func<CrosshairDoc, double> _previewSizeFactor;
    private readonly UndoHistory<CrosshairDoc> _history = new(HistoryLimit);
    private readonly DispatcherTimer _saveTimer;
    private CrosshairDoc? _doc;
    private CrosshairDoc? _persisted;
    private bool _savePending;
    private string? _lastEditKey;
    private double _lastEditMs;
    private bool _syncing;

    /// <param name="previewSizeFactor">Size factor for the preview at zoom 1 (as on the overlay monitor).</param>
    /// <param name="clockMs">Monotonic milliseconds for undo coalescing.</param>
    public DesignerViewModel(AppState state, IDialogService dialogs, Func<CrosshairDoc, double>? previewSizeFactor = null,
        Func<DateTimeOffset>? now = null, Func<double>? clockMs = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(dialogs);
        _state = state;
        _dialogs = dialogs;
        _previewSizeFactor = previewSizeFactor ?? CrosshairRasterizer.NominalSizeFactor;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        if (clockMs is null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            clockMs = () => sw.Elapsed.TotalMilliseconds;
        }
        _clockMs = clockMs;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SaveDebounceMs) };
        _saveTimer.Tick += (_, _) => FlushSave();
        _state.Changed += OnStateChanged;
    }

    /// <summary>The crosshair being edited; null when the library is empty.</summary>
    public CrosshairDoc? Doc => _doc;

    public bool HasDoc => _doc is not null;

    public ObservableCollection<LayerItemViewModel> Layers { get; } = new();

    [ObservableProperty]
    private LayerItemViewModel? _selectedLayer;

    [ObservableProperty]
    private LayerEditorViewModel? _editor;

    [ObservableProperty]
    private int _zoom = 4;

    [ObservableProperty]
    private PreviewBackground _background = PreviewBackground.Sample;

    public double PreviewSizeFactor => _doc is null ? 1 : _previewSizeFactor(_doc);

    public string DocName
    {
        get => _doc?.Name ?? "";
        set
        {
            var name = value?.Trim();
            if (string.IsNullOrEmpty(name)) { OnPropertyChanged(); return; }
            Edit(d => d with { Name = name }, "doc.name");
        }
    }

    /// <summary>IM-3: sizes refer to a 1080 px tall screen and scale with the monitor height.</summary>
    public bool ScalesWithResolution
    {
        get => _doc?.ReferenceHeight is not null;
        set => Edit(d => d with { ReferenceHeight = value ? 1080 : null });
    }

    public bool IsActiveDoc => _doc is not null && _doc.Id == _state.Settings.ActiveCrosshairId;

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    partial void OnZoomChanged(int value)
    {
        int clamped = Math.Clamp(value, MinZoom, MaxZoom);
        if (clamped != value) Zoom = clamped;
    }

    partial void OnSelectedLayerChanged(LayerItemViewModel? value)
    {
        if (_syncing) return;
        _lastEditKey = null;
        RefreshEditor();
        DeleteLayerCommand.NotifyCanExecuteChanged();
        DuplicateLayerCommand.NotifyCanExecuteChanged();
        RenameLayerCommand.NotifyCanExecuteChanged();
        MoveLayerUpCommand.NotifyCanExecuteChanged();
        MoveLayerDownCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Starts editing <paramref name="doc"/> with a fresh undo history.</summary>
    public void Open(CrosshairDoc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        FlushSave();
        _history.Clear();
        _lastEditKey = null;
        _persisted = doc;
        SetDoc(doc, save: false);
        SelectedLayer = Layers.FirstOrDefault();
    }

    /// <summary>Opens the active (else first) crosshair when nothing valid is open.</summary>
    public void EnsureDoc()
    {
        if (_doc is not null && _state.Library.Any(d => d.Id == _doc.Id)) return;
        var doc = _state.Active ?? _state.Library.FirstOrDefault();
        if (doc is not null) Open(doc);
        else Close();
    }

    /// <summary>Applies an edit as one undo step (or merges it into the previous step for the same <paramref name="coalesceKey"/>).</summary>
    public void Edit(Func<CrosshairDoc, CrosshairDoc> change, string? coalesceKey = null)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_doc is null) return;
        var next = change(_doc);
        if (next.Equals(_doc)) return;
        double now = _clockMs();
        bool merge = coalesceKey is not null && coalesceKey == _lastEditKey && now - _lastEditMs <= CoalesceWindowMs;
        if (!merge) _history.Push(_doc);
        _lastEditKey = coalesceKey;
        _lastEditMs = now;
        SetDoc(next, save: true);
    }

    /// <summary>Replaces the layer with the same id.</summary>
    public void ReplaceLayer(Layer layer, string? coalesceKey = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        Edit(d => d with { Layers = d.Layers.Select(l => l.Id == layer.Id ? layer : l).ToArray() }, coalesceKey);
    }

    public void SetLayerVisible(string layerId, bool visible)
    {
        if (_doc?.Layers.FirstOrDefault(l => l.Id == layerId) is { } layer && layer.Visible != visible)
            ReplaceLayer(layer with { Visible = visible });
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_doc is null || !_history.CanUndo) return;
        _lastEditKey = null;
        SetDoc(_history.Undo(_doc), save: true);
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_doc is null || !_history.CanRedo) return;
        _lastEditKey = null;
        SetDoc(_history.Redo(_doc), save: true);
    }

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(MaxZoom, Zoom + 1);

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(MinZoom, Zoom - 1);

    [RelayCommand]
    private void Activate()
    {
        if (_doc is null) return;
        FlushSave();
        var id = _doc.Id;
        _state.Update(s => s with { ActiveCrosshairId = id });
    }

    /// <param name="kind">"Classic", "Radial", "Shape" or "Image".</param>
    [RelayCommand]
    private void AddLayer(string kind)
    {
        if (_doc is null) return;
        Layer? layer = kind switch
        {
            "Classic" => new ClassicLayer { Name = Strings.LayerTypeClassic },
            "Radial" => new RadialLayer { Name = Strings.LayerTypeRadial },
            "Shape" => new ShapeLayer { Name = Strings.LayerTypeShape, Kind = ShapeKind.Ring, StrokeWidth = 1 },
            "Image" => PickImageLayer(),
            _ => null,
        };
        if (layer is null) return;
        Edit(d => d with { Layers = [.. d.Layers, layer] });
        SelectLayer(layer.Id);
    }

    private ImageLayer? PickImageLayer()
    {
        var image = PickImage();
        return image is null ? null : ImageImport.Apply(new ImageLayer { Name = Strings.LayerTypeImage }, image);
    }

    /// <summary>Asks for an image file; shows errors. Null when cancelled or invalid.</summary>
    internal ImageImport.Result? PickImage()
    {
        var path = _dialogs.OpenFile($"{Strings.FileFilterImages} ({ImageImport.FileFilterPattern})|{ImageImport.FileFilterPattern}");
        if (path is null) return null;
        var image = ImageImport.Load(path);
        if (image.Error is not null)
        {
            _dialogs.ShowError(image.Error);
            return null;
        }
        return image;
    }

    private bool HasSelection() => SelectedLayer is not null && _doc is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteLayer()
    {
        if (SelectedLayer is not { } item || _doc is null) return;
        int displayIndex = Layers.IndexOf(item);
        Edit(d => d with { Layers = d.Layers.Where(l => l.Id != item.Id).ToArray() });
        SelectedLayer = Layers.Count == 0 ? null : Layers[Math.Clamp(displayIndex, 0, Layers.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateLayer()
    {
        if (SelectedLayer is not { } item || _doc is null) return;
        var source = _doc.Layers.First(l => l.Id == item.Id);
        var copy = source with { Id = Guid.NewGuid().ToString("N"), Name = string.Format(Strings.CopyNameFormat, source.Name) };
        Edit(d =>
        {
            var list = d.Layers.ToList();
            list.Insert(list.FindIndex(l => l.Id == source.Id) + 1, copy);
            return d with { Layers = list };
        });
        SelectLayer(copy.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RenameLayer()
    {
        if (SelectedLayer is not { } item || _doc?.Layers.FirstOrDefault(l => l.Id == item.Id) is not { } layer) return;
        var name = _dialogs.PromptText(Strings.RenameLayerTitle, Strings.NameLabel, layer.Name)?.Trim();
        if (!string.IsNullOrEmpty(name)) ReplaceLayer(layer with { Name = name });
    }

    private bool CanMoveUp() => SelectedLayer is { } s && Layers.IndexOf(s) > 0;
    private bool CanMoveDown() => SelectedLayer is { } s && Layers.IndexOf(s) is var i && i >= 0 && i < Layers.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveLayerUp()
    {
        if (SelectedLayer is { } s) MoveLayer(s.Id, Layers.IndexOf(s) - 1);
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveLayerDown()
    {
        if (SelectedLayer is { } s) MoveLayer(s.Id, Layers.IndexOf(s) + 1);
    }

    /// <summary>Moves a layer to a position in the list as displayed (topmost layer first).</summary>
    public void MoveLayer(string layerId, int displayIndex)
    {
        if (_doc is null) return;
        int n = _doc.Layers.Count;
        int from = _doc.Layers.ToList().FindIndex(l => l.Id == layerId);
        if (from < 0) return;
        int to = n - 1 - Math.Clamp(displayIndex, 0, n - 1);
        if (to == from) return;
        Edit(d =>
        {
            var list = d.Layers.ToList();
            var layer = list[from];
            list.RemoveAt(from);
            list.Insert(to, layer);
            return d with { Layers = list };
        });
        SelectLayer(layerId);
    }

    /// <summary>Writes a pending debounced save now.</summary>
    public void FlushSave()
    {
        _saveTimer.Stop();
        if (!_savePending || _doc is null) return;
        _savePending = false;
        var saved = _doc with { UpdatedAt = _now() };
        _persisted = saved;
        try
        {
            _state.SaveDoc(saved);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(string.Format(Strings.ErrorSave, ex.Message));
        }
    }

    public void Dispose()
    {
        FlushSave();
        _state.Changed -= OnStateChanged;
    }

    private void SelectLayer(string id) => SelectedLayer = Layers.FirstOrDefault(l => l.Id == id) ?? SelectedLayer;

    private void Close()
    {
        _history.Clear();
        _persisted = null;
        _doc = null;
        Layers.Clear();
        Editor = null;
        RaiseDocChanged();
    }

    private void SetDoc(CrosshairDoc doc, bool save)
    {
        _doc = doc;
        SyncLayers();
        RefreshEditor();
        RaiseDocChanged();
        if (save)
        {
            _savePending = true;
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    private void RaiseDocChanged()
    {
        OnPropertyChanged(nameof(Doc));
        OnPropertyChanged(nameof(HasDoc));
        OnPropertyChanged(nameof(DocName));
        OnPropertyChanged(nameof(ScalesWithResolution));
        OnPropertyChanged(nameof(PreviewSizeFactor));
        OnPropertyChanged(nameof(IsActiveDoc));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        MoveLayerUpCommand.NotifyCanExecuteChanged();
        MoveLayerDownCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Mirrors the doc's layers (topmost first) while keeping item instances and the selection.</summary>
    private void SyncLayers()
    {
        var desired = (_doc?.Layers ?? []).Reverse().ToList();
        string? selectedId = SelectedLayer?.Id;
        _syncing = true;
        try
        {
            if (desired.Select(l => l.Id).SequenceEqual(Layers.Select(i => i.Id)))
            {
                for (int i = 0; i < desired.Count; i++) Layers[i].Update(desired[i]);
            }
            else
            {
                var existing = Layers.ToDictionary(i => i.Id);
                Layers.Clear();
                foreach (var layer in desired)
                {
                    if (existing.TryGetValue(layer.Id, out var item)) item.Update(layer);
                    else item = new LayerItemViewModel(this, layer);
                    Layers.Add(item);
                }
            }
            SelectedLayer = Layers.FirstOrDefault(i => i.Id == selectedId) ?? Layers.FirstOrDefault();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RefreshEditor()
    {
        var layer = SelectedLayer is { } item ? _doc?.Layers.FirstOrDefault(l => l.Id == item.Id) : null;
        if (layer is null) Editor = null;
        else if (Editor is not null && Editor.Layer.Id == layer.Id && Editor.Layer.GetType() == layer.GetType()) Editor.Refresh(layer);
        else Editor = LayerEditorViewModel.Create(this, layer);
    }

    /// <summary>Follows changes made elsewhere (library rename/delete, active switch by hotkey).</summary>
    private void OnStateChanged()
    {
        OnPropertyChanged(nameof(IsActiveDoc));
        OnPropertyChanged(nameof(PreviewSizeFactor));
        if (_doc is null || _savePending) return;
        var current = _state.Library.FirstOrDefault(d => d.Id == _doc.Id);
        if (current is null)
        {
            _doc = null;
            EnsureDoc();
        }
        else if (!ReferenceEquals(current, _persisted))
        {
            _persisted = current;
            SetDoc(current, save: false);
        }
    }
}
