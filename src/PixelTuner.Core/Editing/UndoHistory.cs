namespace PixelTuner.Core.Editing;

/// <summary>
/// The undo and redo stacks of one project, and the "has unsaved changes" rule that goes
/// with them: the project is clean exactly when the state is the one that was saved, so
/// undoing back to the saved state clears the flag again.
/// </summary>
public sealed class UndoHistory
{
    private sealed record Entry(Edit Edit, long LastChange);

    private readonly List<Entry> _done = [];
    private readonly List<Edit> _undone = [];   // the next redo is the last one
    private readonly TimeProvider _time;
    private readonly TimeSpan _mergeWindow;
    private readonly int _limit;

    // How many edits were done when the project was saved; -1 when that state cannot be reached any more.
    private int _savePoint;
    // Edits before this index were done before the last save and are never merged into.
    private int _sealed;

    public UndoHistory(TimeProvider? time = null, TimeSpan? mergeWindow = null, int limit = 500)
    {
        _time = time ?? TimeProvider.System;
        _mergeWindow = mergeWindow ?? TimeSpan.FromSeconds(1.5);
        _limit = limit;
    }

    public bool CanUndo => _done.Count > 0;
    public bool CanRedo => _undone.Count > 0;

    /// <summary>Key of the edit that undo would revert, for the "Undo ..." label.</summary>
    public string? UndoKey => CanUndo ? _done[^1].Edit.Key : null;

    public string? RedoKey => CanRedo ? _undone[^1].Key : null;

    public bool IsDirty => _savePoint != _done.Count;

    public event EventHandler? Changed;

    /// <summary>Applies the edit and records it. If applying throws, nothing is recorded and the project is unchanged.</summary>
    public void Do(Edit edit)
    {
        edit.Apply();

        long now = _time.GetTimestamp();
        if (_undone.Count > 0)
        {
            _undone.Clear();
            if (_savePoint > _done.Count) _savePoint = -1;   // the saved state was in the redo part, now gone
        }

        if (_done.Count > _sealed && _done[^1] is { } top
            && _time.GetElapsedTime(top.LastChange, now) <= _mergeWindow
            && top.Edit.TryMerge(edit))
        {
            _done[^1] = top with { LastChange = now };
        }
        else
        {
            _done.Add(new Entry(edit, now));
            if (_done.Count > _limit)
            {
                _done.RemoveAt(0);
                if (_savePoint >= 0) _savePoint--;   // 0 becomes -1: that state fell off the stack
                _sealed = Math.Max(0, _sealed - 1);
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        var entry = _done[^1];
        entry.Edit.Revert();
        _done.RemoveAt(_done.Count - 1);
        _undone.Add(entry.Edit);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        var edit = _undone[^1];
        edit.Apply();
        _undone.RemoveAt(_undone.Count - 1);
        _done.Add(new Entry(edit, _time.GetTimestamp()));
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>The project was written to disk in its current state.</summary>
    public void MarkSaved()
    {
        _savePoint = _done.Count;
        _sealed = _done.Count;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
