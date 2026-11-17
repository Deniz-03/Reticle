namespace Reticle.App.ViewModels;

/// <summary>Bounded undo/redo stacks of immutable snapshots. Pushing a new snapshot clears the redo stack.</summary>
public sealed class UndoHistory<T>(int capacity) where T : class
{
    private readonly LinkedList<T> _undo = new();
    private readonly Stack<T> _redo = new();

    public int Capacity { get; } = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;

    /// <summary>Records the state before an edit; drops the oldest snapshot beyond <see cref="Capacity"/>.</summary>
    public void Push(T before)
    {
        ArgumentNullException.ThrowIfNull(before);
        _undo.AddLast(before);
        if (_undo.Count > Capacity) _undo.RemoveFirst();
        _redo.Clear();
    }

    /// <returns>The previous state; <paramref name="current"/> becomes redoable.</returns>
    public T Undo(T current)
    {
        if (_undo.Last is not { } last) throw new InvalidOperationException("Nothing to undo.");
        _undo.RemoveLast();
        _redo.Push(current);
        return last.Value;
    }

    /// <returns>The next state; <paramref name="current"/> becomes undoable.</returns>
    public T Redo(T current)
    {
        var next = _redo.Pop();
        _undo.AddLast(current);
        if (_undo.Count > Capacity) _undo.RemoveFirst();
        return next;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
