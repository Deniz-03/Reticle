using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Reticle.Core.Animations;
using Reticle.Core.Hotkeys;
using Reticle.Core.Model;
using Reticle.Core.Storage;

namespace Reticle.App;

/// <summary>
/// Single source of truth for settings, the crosshair library and transient overlay state.
/// Settings are persisted debounced (500 ms) and flushed on <see cref="Dispose"/>; docs are saved immediately.
/// Not thread-safe: call from the UI thread.
/// </summary>
public sealed class AppState : ObservableObject, IDisposable
{
    private const int SaveDebounceMs = 500;
    private const double ScaleStepSize = 0.05;
    private const double MinScale = 0.25;
    private const double MaxScale = 4;

    private readonly LibraryStore _libraryStore;
    private readonly SettingsStore _settingsStore;
    private readonly Func<double> _clock;
    private readonly Timer _saveTimer;
    private readonly object _saveLock = new();
    private Settings? _pendingSave;
    private List<CrosshairDoc> _library;
    private Settings _settings;
    private bool _hidden;

    /// <param name="clock">Monotonic milliseconds used for animations; defaults to a process stopwatch.</param>
    public AppState(LibraryStore library, SettingsStore settings, Func<double>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(settings);
        _libraryStore = library;
        _settingsStore = settings;
        if (clock is null)
        {
            var sw = Stopwatch.StartNew();
            clock = () => sw.Elapsed.TotalMilliseconds;
        }
        _clock = clock;
        _saveTimer = new Timer(_ => FlushSettings(), null, Timeout.Infinite, Timeout.Infinite);

        Presets.SeedIfEmpty(library);
        _library = library.LoadAll().ToList();
        _settings = settings.Load();
        if (Active is null && _library.Count > 0)
            Update(s => s with { ActiveCrosshairId = _library[0].Id });
    }

    /// <summary>Raised after any effective change of settings, library, active doc, hidden state or animation triggers.</summary>
    public event Action? Changed;

    public Settings Settings => _settings;

    public IReadOnlyList<CrosshairDoc> Library => _library;

    public CrosshairDoc? Active => _library.FirstOrDefault(d => d.Id == _settings.ActiveCrosshairId);

    /// <summary>Transient hide (e.g. hide-while-held); never persisted.</summary>
    public bool Hidden => _hidden;

    public AnimationState Animations { get; } = new();

    /// <summary>Current time on the animation clock in milliseconds.</summary>
    public double NowMs => _clock();

    public void Update(Func<Settings, Settings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var next = change(_settings);
        if (next == _settings) return;
        var previousActive = _settings.ActiveCrosshairId;
        _settings = next;
        ScheduleSave(next);
        OnPropertyChanged(nameof(Settings));
        if (previousActive != next.ActiveCrosshairId) OnPropertyChanged(nameof(Active));
        Changed?.Invoke();
    }

    /// <summary>Saves (insert or replace by id) and keeps the library ordered like <see cref="LibraryStore.LoadAll"/>.</summary>
    public void SaveDoc(CrosshairDoc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        _libraryStore.Save(doc);
        _library.RemoveAll(d => d.Id == doc.Id);
        _library.Add(doc);
        _library = _library
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();
        RaiseLibraryChanged();
    }

    /// <summary>Deletes the doc; if it was active, the first remaining doc becomes active.</summary>
    public void DeleteDoc(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_library.RemoveAll(d => d.Id == id) == 0) return;
        _libraryStore.Delete(id);
        if (Active is null)
        {
            string? first = _library.Count > 0 ? _library[0].Id : null;
            if (first != _settings.ActiveCrosshairId)
            {
                Update(s => s with { ActiveCrosshairId = first });
                OnPropertyChanged(nameof(Library));
                return; // Update raised Changed
            }
        }
        RaiseLibraryChanged();
    }

    public void Apply(OverlayCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        switch (command)
        {
            case ToggleOverlay:
                Update(s => s with { OverlayEnabled = !s.OverlayEnabled });
                break;
            case Cycle c:
                if (_library.Count == 0) break;
                int idx = _library.FindIndex(d => d.Id == _settings.ActiveCrosshairId);
                int n = _library.Count;
                int next = idx < 0 ? 0 : (((idx + c.Delta) % n) + n) % n;
                Update(s => s with { ActiveCrosshairId = _library[next].Id });
                break;
            case SelectCrosshair sel:
                if (_library.Any(d => d.Id == sel.Id))
                    Update(s => s with { ActiveCrosshairId = sel.Id });
                break;
            case SetHidden h:
                if (_hidden == h.Hidden) break;
                _hidden = h.Hidden;
                OnPropertyChanged(nameof(Hidden));
                Changed?.Invoke();
                break;
            case CenterOffset:
                Update(s => s with { Offset = new Vec2(0, 0) });
                break;
            case ScaleStep step:
                Update(s => s with
                {
                    GlobalScale = Math.Clamp(Math.Round(s.GlobalScale + step.Delta * ScaleStepSize, 4), MinScale, MaxScale),
                });
                break;
            case AnimationTrigger t:
                TriggerAnimations(t);
                break;
        }
    }

    private void TriggerAnimations(AnimationTrigger t)
    {
        if (Active is not { } doc) return;
        var trigger = new InputTrigger(t.Kind, t.Code);
        double now = _clock();
        bool any = false;
        foreach (var layer in doc.Layers)
        {
            if (!layer.Visible || layer.Animation?.Trigger != trigger) continue;
            Animations.OnTrigger(layer.Id, t.Down, now);
            any = true;
        }
        if (any) Changed?.Invoke();
    }

    private void RaiseLibraryChanged()
    {
        OnPropertyChanged(nameof(Library));
        OnPropertyChanged(nameof(Active));
        Changed?.Invoke();
    }

    private void ScheduleSave(Settings settings)
    {
        lock (_saveLock) _pendingSave = settings;
        _saveTimer.Change(SaveDebounceMs, Timeout.Infinite);
    }

    private void FlushSettings()
    {
        lock (_saveLock)
        {
            if (_pendingSave is null) return;
            try { _settingsStore.Save(_pendingSave); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Saving settings failed: {ex.Message}");
                return; // keep pending; the next change or Dispose retries
            }
            _pendingSave = null;
        }
    }

    /// <summary>Stops the debounce timer and writes any pending settings.</summary>
    public void Dispose()
    {
        _saveTimer.Dispose();
        FlushSettings();
    }
}
