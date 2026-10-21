using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Tests.Editing;

internal sealed class ManualTime : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}

public class UndoHistoryTests
{
    private readonly ManualTime _time = new();
    private readonly ShowProject _project = new() { Name = "A" };

    private UndoHistory History(int limit = 500) => new(_time, TimeSpan.FromSeconds(1), limit);

    [Fact]
    public void Do_applies_undo_reverts_redo_applies_again()
    {
        var history = History();
        history.Do(Edits.RenameProject(_project, "B"));
        Assert.Equal("B", _project.Name);
        Assert.Equal("RenameProject", history.UndoKey);

        Assert.True(history.Undo());
        Assert.Equal("A", _project.Name);
        Assert.True(history.CanRedo);
        Assert.Equal("RenameProject", history.RedoKey);

        Assert.True(history.Redo());
        Assert.Equal("B", _project.Name);
        Assert.False(history.Undo() && history.Undo());   // only one step was there
    }

    [Fact]
    public void Undo_and_redo_on_empty_stacks_do_nothing()
    {
        var history = History();
        Assert.False(history.Undo());
        Assert.False(history.Redo());
        Assert.False(history.IsDirty);
    }

    [Fact]
    public void The_project_is_clean_again_when_undo_returns_to_the_saved_state()
    {
        var history = History();
        Assert.False(history.IsDirty);

        history.Do(Edits.RenameProject(_project, "B"));
        Assert.True(history.IsDirty);

        history.Undo();
        Assert.False(history.IsDirty);
        history.Redo();
        Assert.True(history.IsDirty);

        history.MarkSaved();
        Assert.False(history.IsDirty);
        history.Undo();
        Assert.True(history.IsDirty);
        history.Redo();
        Assert.False(history.IsDirty);
    }

    [Fact]
    public void A_new_edit_after_undo_drops_redo_and_the_saved_state_if_it_was_there()
    {
        var history = History();
        history.Do(Edits.RenameProject(_project, "B"));
        _time.Advance(TimeSpan.FromSeconds(5));
        history.Do(Edits.AddMarker(_project, new Marker { Name = "m" }));
        history.MarkSaved();                 // saved with two edits
        history.Undo();                      // back to one

        history.Do(Edits.AddMarker(_project, new Marker { Name = "other" }));   // two edits again, but another state

        Assert.False(history.CanRedo);
        Assert.True(history.IsDirty);        // must not look like the saved state
        history.Undo();
        history.Undo();
        Assert.True(history.IsDirty);        // the initial state is not the saved one either
    }

    [Fact]
    public void Quick_edits_of_one_value_merge_into_one_step()
    {
        var history = History();
        history.Do(Edits.RenameProject(_project, "AB"));
        _time.Advance(TimeSpan.FromMilliseconds(300));
        history.Do(Edits.RenameProject(_project, "ABC"));
        _time.Advance(TimeSpan.FromMilliseconds(300));
        history.Do(Edits.RenameProject(_project, "ABCD"));

        history.Undo();

        Assert.Equal("A", _project.Name);    // all three letters in one step
        Assert.False(history.CanUndo);
        history.Redo();
        Assert.Equal("ABCD", _project.Name);
    }

    [Fact]
    public void A_pause_ends_the_merge()
    {
        var history = History();
        history.Do(Edits.RenameProject(_project, "B"));
        _time.Advance(TimeSpan.FromSeconds(3));
        history.Do(Edits.RenameProject(_project, "C"));

        history.Undo();

        Assert.Equal("B", _project.Name);
    }

    [Fact]
    public void Edits_of_different_objects_do_not_merge()
    {
        var timeline = new Timeline { Name = "T" };
        _project.Timelines.Add(timeline);
        var history = History();
        history.Do(Edits.RenameProject(_project, "B"));
        history.Do(Edits.RenameTimeline(timeline, "T2"));

        history.Undo();

        Assert.Equal("T", timeline.Name);
        Assert.Equal("B", _project.Name);
    }

    [Fact]
    public void An_edit_never_merges_into_one_that_was_saved()
    {
        var history = History();
        history.Do(Edits.RenameProject(_project, "B"));
        history.MarkSaved();
        history.Do(Edits.RenameProject(_project, "C"));   // right after the save, well inside the window

        Assert.True(history.IsDirty);
        history.Undo();

        Assert.Equal("B", _project.Name);                 // the saved name is still reachable
        Assert.False(history.IsDirty);
    }

    [Fact]
    public void An_edit_that_throws_is_not_recorded()
    {
        var history = History();
        var broken = Edits.Group("Broken", Edits.RenameProject(_project, "B"), new ThrowingEdit());

        Assert.Throws<InvalidOperationException>(() => history.Do(broken));

        Assert.Equal("A", _project.Name);                 // the first part was rolled back
        Assert.False(history.CanUndo);
        Assert.False(history.IsDirty);
    }

    private sealed class ThrowingEdit() : Edit("Throw")
    {
        public override void Apply() => throw new InvalidOperationException();
        public override void Revert() { }
    }

    [Fact]
    public void The_stack_has_a_limit_and_the_saved_state_can_fall_off_it()
    {
        var history = History(limit: 3);
        history.MarkSaved();                              // saved at the very start
        for (int i = 0; i < 5; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            history.Do(Edits.AddMarker(_project, new Marker { Name = $"m{i}", Position = TimeSpan.FromSeconds(i) }));
        }

        Assert.Equal(5, _project.Markers.Count);
        while (history.Undo()) { }

        Assert.Equal(2, _project.Markers.Count);          // only the last three steps could be undone
        Assert.True(history.IsDirty);                     // and the saved state is no longer reachable
    }

    [Fact]
    public void Changed_is_raised_for_every_step()
    {
        var history = History();
        int raised = 0;
        history.Changed += (_, _) => raised++;

        history.Do(Edits.RenameProject(_project, "B"));
        history.Undo();
        history.Redo();
        history.MarkSaved();

        Assert.Equal(4, raised);
    }
}
