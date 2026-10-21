namespace PixelTuner.Core.Editing;

/// <summary>
/// One undoable change of a project. An edit is built with everything it needs (the objects,
/// the new value, the old value read at that moment), applied once by <see cref="UndoHistory.Do"/>,
/// and then reverted and re-applied by undo and redo. No reflection: values move through delegates.
/// </summary>
public abstract class Edit(string key)
{
    /// <summary>
    /// Stable name of the kind of change ("RenameProject"). The UI turns it into a localised
    /// "Undo ..." label; the core has no texts of its own.
    /// </summary>
    public string Key { get; } = key;

    public abstract void Apply();
    public abstract void Revert();

    /// <summary>
    /// Folds a later edit into this one, so typing a name is one undo step and not one per letter.
    /// Called after <paramref name="next"/> was applied; true when this edit now stands for both.
    /// </summary>
    public virtual bool TryMerge(Edit next) => false;
}

/// <summary>Sets one value. Edits of the same property of the same object in quick succession merge.</summary>
public sealed class SetValueEdit<T> : Edit
{
    private readonly object _target;
    private readonly string _property;
    private readonly Action<T> _set;
    private readonly bool _mergeable;
    private readonly T _old;
    private T _new;

    public SetValueEdit(string key, object target, string property, Func<T> get, Action<T> set, T value, bool mergeable)
        : base(key)
    {
        _target = target;
        _property = property;
        _set = set;
        _mergeable = mergeable;
        _old = get();
        _new = value;
    }

    public override void Apply() => _set(_new);
    public override void Revert() => _set(_old);

    public override bool TryMerge(Edit next)
    {
        if (!_mergeable || next is not SetValueEdit<T> other || !other._mergeable
            || !ReferenceEquals(other._target, _target) || other._property != _property) return false;
        _new = other._new;
        return true;
    }
}

public sealed class InsertEdit<T>(string key, IList<T> list, int index, T item) : Edit(key)
{
    public override void Apply() => list.Insert(index, item);
    public override void Revert() => list.RemoveAt(index);
}

public sealed class RemoveEdit<T> : Edit
{
    private readonly IList<T> _list;
    private readonly int _index;
    private readonly T _item;

    public RemoveEdit(string key, IList<T> list, T item) : base(key)
    {
        _list = list;
        _item = item;
        _index = list.IndexOf(item);
        if (_index < 0) throw new InvalidOperationException("The item is not in the list.");
    }

    public override void Apply() => _list.RemoveAt(_index);
    public override void Revert() => _list.Insert(_index, _item);
}

public sealed class MoveEdit<T>(string key, IList<T> list, int from, int to) : Edit(key)
{
    public override void Apply() => Move(from, to);
    public override void Revert() => Move(to, from);

    private void Move(int source, int target)
    {
        var item = list[source];
        list.RemoveAt(source);
        list.Insert(target, item);
    }
}

/// <summary>Several edits that undo and redo as one ("delete this timeline and its items").</summary>
public sealed class GroupEdit(string key, IReadOnlyList<Edit> edits) : Edit(key)
{
    public override void Apply()
    {
        int done = 0;
        try
        {
            foreach (var edit in edits)
            {
                edit.Apply();
                done++;
            }
        }
        catch
        {
            for (int i = done - 1; i >= 0; i--) edits[i].Revert();   // all or nothing
            throw;
        }
    }

    public override void Revert()
    {
        for (int i = edits.Count - 1; i >= 0; i--) edits[i].Revert();
    }
}
