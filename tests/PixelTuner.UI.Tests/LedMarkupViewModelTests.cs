using PixelTuner.Core.Editing;
using PixelTuner.Core.Layout;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.LedLayouts;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public sealed class LedMarkupViewModelTests : IDisposable
{
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly Timeline _video = new() { Kind = TimelineKind.Video, Name = "Video" };
    private bool? _result;

    public LedMarkupViewModelTests() => _doc.Execute(Edits.AddTimeline(_doc.Project, _video));

    public void Dispose() => _doc.Dispose();

    private LedMarkupViewModel New(int columns = 6, int rows = 4)
    {
        var vm = new LedMarkupViewModel(_doc, null, _video, "New", applied => _result = applied)
        {
            ColumnsBox = columns, RowsBox = rows,
        };
        vm.ApplySizeCommand.Execute(null);
        return vm;
    }

    private LedMarkupViewModel Edit(LedLayout layout) => new(_doc, layout, null, layout.Name, applied => _result = applied);

    private LedLayout Existing(int columns, int rows, params int[] leds)
    {
        var layout = new LedLayout { Name = "prop", Columns = columns, Rows = rows, Leds = [.. leds] };
        _doc.Execute(Edits.AddLedLayout(_doc.Project, layout));
        return layout;
    }

    // What an imported project has for a prop that was never marked up: a layout with no grid.
    private LedLayout Unmarked()
    {
        var layout = new LedLayout { Name = "path1" };
        _doc.Project.LedLayouts.Add(layout);
        return layout;
    }

    private static void Click(LedMarkupViewModel vm, int x, int y, GestureKeys keys = GestureKeys.None)
    {
        vm.Press(x, y, keys);
        vm.Release(x, y);
    }

    private static void Drag(LedMarkupViewModel vm, (int X, int Y) from, (int X, int Y) to, GestureKeys keys = GestureKeys.None)
    {
        vm.Press(from.X, from.Y, keys);
        vm.Move(to.X, to.Y);
        vm.Release(to.X, to.Y);
    }

    private static int[] Chain(LedMarkupViewModel vm) => [.. vm.Markup.Chain];

    private static int Cell(LedMarkupViewModel vm, int x, int y) => vm.Markup.Cell(x, y);

    // ---- placing

    [Fact]
    public void Clicks_place_the_next_numbers_and_a_click_on_a_led_never_erases_it()
    {
        var vm = New();
        Click(vm, 0, 0);
        Click(vm, 1, 0);
        Click(vm, 1, 0);   // already LED 2
        Assert.Equal([Cell(vm, 0, 0), Cell(vm, 1, 0)], Chain(vm));
        Assert.Equal(3, vm.Next);
        Assert.Equal(Cell(vm, 1, 0), vm.LastPlaced);
    }

    [Fact]
    public void A_led_placed_behind_an_earlier_one_goes_in_between_and_the_tail_shifts()
    {
        var vm = New();
        foreach (int x in new[] { 0, 1, 2, 3 }) Click(vm, x, 0);
        Click(vm, 1, 0, GestureKeys.Alt);     // the cursor goes behind LED 2
        Assert.Equal(3, vm.Next);
        Click(vm, 0, 1);
        Assert.Equal([Cell(vm, 0, 0), Cell(vm, 1, 0), Cell(vm, 0, 1), Cell(vm, 2, 0), Cell(vm, 3, 0)], Chain(vm));
        Assert.Equal(4, vm.Next);
        Assert.Equal(3, vm.Markup.NumberOf(Cell(vm, 0, 1)));

        vm.NextAtEndCommand.Execute(null);
        Assert.Equal(6, vm.Next);
    }

    [Fact]
    public void A_drag_numbers_every_cell_on_the_way_even_when_the_pointer_jumps_and_is_one_undo_step()
    {
        var vm = New();
        Drag(vm, (0, 0), (4, 0));            // the pointer skipped three cells
        Assert.Equal([0, 1, 2, 3, 4], Chain(vm));
        Assert.Equal(6, vm.Next);

        vm.UndoCommand.Execute(null);
        Assert.Empty(Chain(vm));
        vm.RedoCommand.Execute(null);
        Assert.Equal([0, 1, 2, 3, 4], Chain(vm));
    }

    [Fact]
    public void A_diagonal_drag_passes_every_cell_of_the_diagonal()
    {
        var vm = New();
        Drag(vm, (0, 0), (3, 3));
        Assert.Equal([Cell(vm, 0, 0), Cell(vm, 1, 1), Cell(vm, 2, 2), Cell(vm, 3, 3)], Chain(vm));
    }

    [Fact]
    public void A_press_outside_the_grid_does_nothing_and_a_release_without_a_press_does_not_crash()
    {
        var vm = New();
        bool canUndo = vm.UndoCommand.CanExecute(null);   // the size of the grid is a step already
        Click(vm, -1, 0);
        Click(vm, 6, 0);
        vm.Release(0, 0);
        Assert.Empty(Chain(vm));
        Assert.Equal(canUndo, vm.UndoCommand.CanExecute(null));
    }

    // ---- lines and fills

    [Fact]
    public void Shift_click_draws_a_line_from_the_last_placed_led_at_any_angle()
    {
        var vm = New(9, 6);
        Click(vm, 0, 0);
        Click(vm, 8, 3, GestureKeys.Shift);
        Assert.Equal(9, vm.Markup.Count);                  // one LED per column of the long side
        Assert.Empty(vm.Markup.Jumps());
        Assert.Equal(Cell(vm, 8, 3), vm.LastPlaced);
        Assert.Equal(10, vm.Next);

        Click(vm, 8, 3, GestureKeys.Shift);                // on the last LED itself: nothing new
        Assert.Equal(9, vm.Markup.Count);
    }

    [Fact]
    public void Shift_click_with_nothing_placed_yet_places_that_cell()
    {
        var vm = New();
        Click(vm, 2, 2, GestureKeys.Shift);
        Assert.Equal([Cell(vm, 2, 2)], Chain(vm));
    }

    [Fact]
    public void Shift_drag_shows_the_segment_with_its_numbers_before_it_is_made()
    {
        var vm = New();
        vm.Press(1, 1, GestureKeys.Shift);
        vm.Move(4, 1);
        Assert.Equal([(Cell(vm, 1, 1), 1), (Cell(vm, 2, 1), 2), (Cell(vm, 3, 1), 3), (Cell(vm, 4, 1), 4)], vm.Preview);
        Assert.Empty(Chain(vm));
        vm.Move(1, 3);                         // the pointer found another end
        Assert.Equal([Cell(vm, 1, 1), Cell(vm, 1, 2), Cell(vm, 1, 3)], vm.Preview.Select(p => p.Cell));
        vm.Release(1, 3);
        Assert.Empty(vm.Preview);
        Assert.Equal([Cell(vm, 1, 1), Cell(vm, 1, 2), Cell(vm, 1, 3)], Chain(vm));
    }

    [Fact]
    public void Ctrl_drag_fills_a_rectangle_starting_where_the_drag_began_with_the_direction_and_pattern_that_are_set()
    {
        var vm = New();
        Drag(vm, (2, 1), (0, 0), GestureKeys.Ctrl);      // from the bottom right of the rectangle
        // Rows, snake, starting at the bottom right: bottom row right to left, then the top row left to right.
        Assert.Equal([Cell(vm, 2, 1), Cell(vm, 1, 1), Cell(vm, 0, 1), Cell(vm, 0, 0), Cell(vm, 1, 0), Cell(vm, 2, 0)], Chain(vm));
        Assert.Equal(7, vm.Next);

        var other = New();
        other.FillDirectionIndex = (int)FillDirection.Columns;
        other.FillPatternIndex = (int)FillPattern.Straight;
        Drag(other, (0, 0), (1, 1), GestureKeys.Ctrl);
        Assert.Equal([Cell(other, 0, 0), Cell(other, 0, 1), Cell(other, 1, 0), Cell(other, 1, 1)], Chain(other));
    }

    [Fact]
    public void A_fill_leaves_the_leds_that_are_there_and_numbers_only_the_new_cells_in_the_preview()
    {
        var vm = New();
        Click(vm, 1, 0);
        vm.Press(0, 0, GestureKeys.Ctrl);
        vm.Move(2, 0);
        Assert.Equal([(Cell(vm, 0, 0), 2), (Cell(vm, 2, 0), 3)], vm.Preview);
        vm.Release(2, 0);
        Assert.Equal(3, vm.Markup.Count);
    }

    [Fact]
    public void With_overwrite_a_fill_renumbers_the_leds_that_are_there_and_the_preview_counts_as_the_chain_will()
    {
        var vm = New();
        Drag(vm, (0, 0), (3, 0));                          // LEDs 1..4 on the top row
        vm.Overwrite = true;
        vm.NextAtEndCommand.Execute(null);
        Click(vm, 1, 0, GestureKeys.Alt);                  // the cursor behind LED 2
        vm.Press(3, 0, GestureKeys.Shift);
        vm.Move(0, 0);                                     // a line (3,0) -> (0,0): all four cells again
        // LEDs 1 and 2 stand in front of the place, so the first of the line takes number 3 - 2 = 1.
        Assert.Equal([1, 2, 3, 4], vm.Preview.Select(p => p.Number));
        vm.Release(0, 0);
        Assert.Equal([3, 2, 1, 0], Chain(vm));
    }

    [Fact]
    public void Ctrl_Shift_drag_picks_the_range_of_the_leds_inside_the_rectangle()
    {
        var vm = New();
        Drag(vm, (0, 0), (5, 0));
        Drag(vm, (0, 1), (5, 1));                          // LEDs 7..12 on row 2
        vm.Press(1, 0, GestureKeys.Ctrl | GestureKeys.Shift);
        vm.Move(2, 1);
        Assert.Equal((1, 0, 2, 1), vm.Selection);
        vm.Release(2, 1);
        Assert.Null(vm.Selection);
        Assert.Equal((2, 9), (vm.RangeFrom, vm.RangeTo));    // LEDs in x 1..2, y 0..1: 2, 3 and 8, 9
        Assert.Equal(2, vm.MoveTarget);
        Assert.Equal(12, vm.Markup.Count);

        vm.Press(4, 3, GestureKeys.Ctrl | GestureKeys.Shift);   // no LEDs in here
        vm.Release(5, 3);
        Assert.False(vm.HasRange);
    }

    [Fact]
    public void The_whole_matrix_button_replaces_the_markup_with_a_sweep_of_every_cell()
    {
        var vm = New(3, 2);
        Click(vm, 2, 1);
        vm.FillAllCommand.Execute(null);
        Assert.Equal([0, 1, 2, 5, 4, 3], Chain(vm));
        Assert.Equal(7, vm.Next);
        Assert.Equal(Cell(vm, 0, 1), vm.LastPlaced);
        vm.UndoCommand.Execute(null);
        Assert.Equal([Cell(vm, 2, 1)], Chain(vm));
    }

    // ---- taking out and moving

    [Fact]
    public void A_right_click_takes_a_led_out_and_the_tail_closes_the_gap()
    {
        var vm = New();
        Drag(vm, (0, 0), (3, 0));
        Click(vm, 1, 0, GestureKeys.Erase);
        Assert.Equal([0, 2, 3], Chain(vm));
        Assert.Equal(4, vm.Next);          // the cursor stays behind the last LED
        Click(vm, 5, 3, GestureKeys.Erase);
        Assert.Equal(3, vm.Markup.Count);
    }

    [Fact]
    public void A_right_click_in_front_of_the_cursor_moves_the_cursor_back_with_the_tail()
    {
        var vm = New();
        Drag(vm, (0, 0), (3, 0));
        Click(vm, 1, 0, GestureKeys.Alt);                  // the cursor behind LED 2
        Click(vm, 0, 0, GestureKeys.Erase);                // LED 1 goes
        Assert.Equal(2, vm.Next);
    }

    [Fact]
    public void A_right_drag_takes_out_all_the_leds_it_passes_in_one_step()
    {
        var vm = New();
        Drag(vm, (0, 0), (4, 0));
        Drag(vm, (1, 0), (3, 0), GestureKeys.Erase);
        Assert.Equal([0, 4], Chain(vm));
        vm.UndoCommand.Execute(null);
        Assert.Equal([0, 1, 2, 3, 4], Chain(vm));
    }

    [Fact]
    public void A_double_click_moves_a_led_to_a_typed_number_without_making_a_second_one()
    {
        var vm = New();
        Drag(vm, (0, 0), (3, 0));
        vm.BeginTransfer(0, 0);
        Assert.True(vm.IsTransferring);
        Assert.Equal(1, vm.TransferNumber);
        vm.TransferNumber = 3;
        vm.CommitTransferCommand.Execute(null);
        Assert.False(vm.IsTransferring);
        Assert.Equal([1, 2, 0, 3], Chain(vm));
        Assert.Equal(3, vm.Markup.NumberOf(0));

        vm.BeginTransfer(5, 3);                  // an empty cell has nothing to move
        Assert.False(vm.IsTransferring);
        vm.BeginTransfer(3, 0);
        vm.CancelTransferCommand.Execute(null);
        Assert.Equal([1, 2, 0, 3], Chain(vm));
    }

    [Fact]
    public void Dropping_a_gesture_in_the_middle_of_a_drag_puts_everything_back()
    {
        var vm = New();
        Click(vm, 0, 0);
        vm.Press(1, 0, GestureKeys.None);
        vm.Move(4, 0);
        Assert.True(vm.CancelGesture());
        vm.Release(4, 0);
        Assert.Equal([0], Chain(vm));
        Assert.Equal(2, vm.Next);
        Assert.False(vm.CancelGesture());
    }

    // ---- ranges and the whole markup

    private static LedMarkupViewModel Strip(LedMarkupViewModel vm)
    {
        Drag(vm, (0, 0), (5, 0));                 // LEDs 1..6 on the top row
        return vm;
    }

    [Fact]
    public void Range_commands_reverse_move_delete_and_cut_the_tail()
    {
        var vm = Strip(New());
        Assert.False(vm.HasRange);
        Assert.False(vm.ReverseRangeCommand.CanExecute(null));

        vm.SetRange(2, 4);
        vm.ReverseRangeCommand.Execute(null);
        Assert.Equal([0, 3, 2, 1, 4, 5], Chain(vm));

        vm.MoveTarget = 4;
        vm.MoveRangeCommand.Execute(null);
        Assert.Equal([0, 4, 5, 3, 2, 1], Chain(vm));
        Assert.Equal((4, 6), (vm.RangeFrom, vm.RangeTo));   // the block stays selected where it went

        vm.SetRange(2, 3);
        vm.DeleteRangeCommand.Execute(null);
        Assert.Equal([0, 3, 2, 1], Chain(vm));
        Assert.False(vm.HasRange);                          // as in PixelTuner 2 the selection is gone
        Assert.Equal(5, vm.Next);

        vm.SetRange(1, 2);
        vm.ClearTailCommand.Execute(null);
        Assert.Equal([0, 3], Chain(vm));
        Assert.Equal(3, vm.Next);
    }

    [Fact]
    public void A_range_typed_the_wrong_way_round_or_too_far_still_works_on_what_is_in_the_chain()
    {
        var vm = Strip(New());
        vm.SetRange(4, 2);
        vm.ReverseRangeCommand.Execute(null);
        Assert.Equal([0, 3, 2, 1, 4, 5], Chain(vm));

        vm.SetRange(5, 99);
        vm.DeleteRangeCommand.Execute(null);
        Assert.Equal([0, 3, 2, 1], Chain(vm));

        vm.SetRange(9, 12);                                  // reaches past the chain
        Assert.False(vm.HasRange);
        Assert.False(vm.DeleteRangeCommand.CanExecute(null));
    }

    [Fact]
    public void A_block_moved_too_far_stops_at_the_end_of_the_chain()
    {
        var vm = Strip(New());
        vm.SetRange(1, 2);
        vm.MoveTarget = 50;
        vm.MoveRangeCommand.Execute(null);
        Assert.Equal([2, 3, 4, 5, 0, 1], Chain(vm));
        Assert.Equal((5, 6), (vm.RangeFrom, vm.RangeTo));
    }

    [Fact]
    public void The_range_can_be_taken_off_without_changing_the_chain()
    {
        var vm = Strip(New());
        vm.SetRange(2, 3);
        vm.ClearRangeSelectionCommand.Execute(null);
        Assert.False(vm.HasRange);
        Assert.Equal(6, vm.Markup.Count);
    }

    [Fact]
    public void Whole_operations_mirror_rotate_reverse_and_clear_with_undo()
    {
        var vm = New(3, 2);
        Drag(vm, (0, 0), (2, 0));
        vm.MirrorHorizontallyCommand.Execute(null);
        Assert.Equal([2, 1, 0], Chain(vm));
        vm.MirrorVerticallyCommand.Execute(null);
        Assert.Equal([5, 4, 3], Chain(vm));
        vm.ReverseAllCommand.Execute(null);
        Assert.Equal([3, 4, 5], Chain(vm));
        Assert.Equal(4, vm.Next);              // the cursor stayed at the end

        vm.RotateCommand.Execute(null);
        Assert.Equal((2, 3), (vm.Markup.Columns, vm.Markup.Rows));
        Assert.Equal((2, 3), (vm.ColumnsBox, vm.RowsBox));

        vm.ClearAllCommand.Execute(null);
        Assert.Equal(0, vm.Markup.Count);
        Assert.Equal(1, vm.Next);
        while (vm.UndoCommand.CanExecute(null)) vm.UndoCommand.Execute(null);
        Assert.Empty(Chain(vm));
        Assert.Equal((16, 16), (vm.Markup.Columns, vm.Markup.Rows));   // the first step of the window was the size
    }

    [Fact]
    public void Resizing_the_grid_keeps_leds_where_they_are_and_says_how_many_fell_out()
    {
        var vm = New(4, 2);
        vm.FillAllCommand.Execute(null);
        vm.ColumnsBox = 2; vm.RowsBox = 2;
        vm.ApplySizeCommand.Execute(null);
        Assert.Equal(4, vm.Markup.Count);
        Assert.Contains("4", vm.Hint);

        vm.ColumnsBox = 5000; vm.RowsBox = 5000;
        vm.ApplySizeCommand.Execute(null);
        Assert.Equal((2, 2), (vm.Markup.Columns, vm.Markup.Rows));
        Assert.Equal((2, 2), (vm.ColumnsBox, vm.RowsBox));
        Assert.NotNull(vm.Hint);
    }

    [Fact]
    public void Undo_and_redo_walk_the_history_and_a_new_step_drops_the_redo()
    {
        var vm = New();
        Click(vm, 0, 0);
        Click(vm, 1, 0);
        vm.UndoCommand.Execute(null);
        Assert.Equal([0], Chain(vm));
        Assert.Equal(2, vm.Next);
        Assert.True(vm.RedoCommand.CanExecute(null));
        Click(vm, 0, 1);
        Assert.False(vm.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void The_history_keeps_the_last_300_steps()
    {
        var vm = New(40, 10);
        for (int i = 0; i < 350; i++) Click(vm, i % 40, i / 40);
        int undone = 0;
        while (vm.UndoCommand.CanExecute(null)) { vm.UndoCommand.Execute(null); undone++; }
        Assert.Equal(300, undone);
        Assert.Equal(50, vm.Markup.Count);
    }

    // ---- what the canvas shows

    [Fact]
    public void The_status_and_the_hover_text_name_the_numbers_and_the_cell_under_the_pointer()
    {
        var vm = New();
        Assert.Contains("1", vm.StatusText);
        Click(vm, 2, 1);
        vm.Hover(2, 1);
        Assert.Contains("№ 1", vm.HoverText);
        Assert.Contains("X 2, Y 1", vm.HoverText);
        vm.Hover(0, 0);
        Assert.DoesNotContain("№", vm.HoverText);
        vm.Hover(-5, 0);
        Assert.Equal("", vm.HoverText);
    }

    [Fact]
    public void The_toggles_of_the_view_ask_the_canvas_to_draw_again()
    {
        var vm = New();
        int redraws = 0;
        vm.Redraw += (_, _) => redraws++;
        vm.ShowNumbers = false;
        vm.ShowPath = false;
        vm.ShowGradient = false;
        Assert.Equal(3, redraws);
    }

    // ---- leaving

    [Fact]
    public void Closing_with_changes_asks_first_and_without_them_just_closes()
    {
        var clean = Edit(Existing(3, 2, 0, 1));
        clean.RequestClose();
        Assert.False(clean.IsAskingToLeave);
        Assert.False(_result);

        _result = null;
        var vm = Edit(Existing(3, 2, 0, 1));
        Click(vm, 2, 0);
        vm.RequestClose();
        Assert.True(vm.IsAskingToLeave);
        Assert.Null(_result);                       // nothing is decided yet
        vm.KeepEditingCommand.Execute(null);
        Assert.False(vm.IsAskingToLeave);
        Assert.Null(_result);

        vm.RequestClose();
        vm.ApplyCommand.Execute(null);               // "Save"
        Assert.True(_result);
    }

    // ---- OK and Cancel

    [Fact]
    public void OK_on_a_new_layout_adds_it_to_the_video_timeline_in_one_undo_step()
    {
        var vm = New(4, 3);
        vm.Name = " left pole ";
        vm.FillAllCommand.Execute(null);
        vm.ApplyCommand.Execute(null);

        Assert.True(_result);
        var layout = Assert.Single(_doc.Project.LedLayouts);
        Assert.Equal(("left pole", 4, 3), (layout.Name, layout.Columns, layout.Rows));
        Assert.Equal(12, layout.Leds.Count);
        Assert.Equal(layout.Id, _video.LedLayoutId);

        _doc.Undo();
        Assert.Empty(_doc.Project.LedLayouts);
        Assert.Null(_video.LedLayoutId);
    }

    [Fact]
    public void Cancel_leaves_the_project_alone()
    {
        var layout = Existing(3, 2, 0, 1);
        var vm = Edit(layout);
        Click(vm, 2, 0);
        vm.Name = "other";
        Assert.True(vm.IsDirty);
        vm.CancelCommand.Execute(null);
        Assert.False(_result);
        Assert.Equal([0, 1], layout.Leds);
        Assert.Equal("prop", layout.Name);
    }

    [Fact]
    public void OK_on_an_existing_layout_writes_the_name_and_the_markup_as_one_undo_step()
    {
        var layout = Existing(3, 2, 0, 1);
        var vm = Edit(layout);
        Click(vm, 2, 0);
        vm.Name = "renamed";
        vm.ApplyCommand.Execute(null);

        Assert.True(_result);
        Assert.Equal("renamed", layout.Name);
        Assert.Equal([0, 1, 2], layout.Leds);
        _doc.Undo();
        Assert.Equal("prop", layout.Name);
        Assert.Equal([0, 1], layout.Leds);
    }

    [Fact]
    public void OK_without_changes_adds_nothing_to_the_history()
    {
        var layout = Existing(3, 2, 0, 1);
        bool dirtyBefore = _doc.IsDirty;
        string? undoKey = _doc.UndoKey;
        var vm = Edit(layout);
        Assert.False(vm.IsDirty);
        vm.ApplyCommand.Execute(null);
        Assert.True(_result);
        Assert.Equal(undoKey, _doc.UndoKey);
        Assert.Equal(dirtyBefore, _doc.IsDirty);
    }

    [Fact]
    public void The_space_between_tiles_is_part_of_the_markup_and_goes_in_with_OK_in_one_step()
    {
        var layout = Existing(3, 2, 0, 1);
        layout.TilesMargin = 4;
        var vm = Edit(layout);
        Assert.Equal(4, vm.Spacing);
        Assert.False(vm.IsDirty);

        vm.Spacing = 500;
        Assert.Equal(100, vm.Spacing);                 // PixelTuner 2's range is 0 to 100
        vm.Spacing = -3;
        Assert.Equal(0, vm.Spacing);
        Assert.True(vm.IsDirty);
        vm.Spacing = 9;
        vm.ApplyCommand.Execute(null);
        Assert.Equal(9, layout.TilesMargin);

        _doc.Undo();
        Assert.Equal(4, layout.TilesMargin);
    }

    [Fact]
    public void A_new_layout_starts_with_the_default_space_and_a_changed_space_is_stored_with_it()
    {
        var vm = New();
        Assert.Equal(LedMarkupViewModel.DefaultSpacing, vm.Spacing);
        vm.Spacing = 7;
        vm.ApplyCommand.Execute(null);
        Assert.Equal(7, Assert.Single(_doc.Project.LedLayouts).TilesMargin);
    }

    [Fact]
    public void A_prop_of_the_drawing_that_was_never_marked_up_opens_on_a_new_grid_and_OK_gives_it_its_size()
    {
        var layout = Unmarked();
        var vm = Edit(layout);
        Assert.Equal((LedMarkupViewModel.DefaultSize, LedMarkupViewModel.DefaultSize), (vm.Markup.Columns, vm.Markup.Rows));
        Click(vm, 3, 3);
        vm.ApplyCommand.Execute(null);
        Assert.Equal((16, 16), (layout.Columns, layout.Rows));
        Assert.Equal([3 * 16 + 3], layout.Leds);
    }

    [Fact]
    public void A_prop_of_the_drawing_that_is_left_as_it_was_stays_without_a_grid()
    {
        var layout = Unmarked();
        var vm = Edit(layout);
        Assert.False(vm.IsDirty);
        vm.ApplyCommand.Execute(null);
        Assert.Equal((0, 0), (layout.Columns, layout.Rows));
    }

    [Fact]
    public void The_gaps_and_repeats_a_PixelTuner_2_file_had_are_told_and_OK_compacts_the_numbers_even_when_nothing_else_changed()
    {
        var layout = Existing(3, 2, 0, 1, 2);
        layout.SourceIssues = new LedNumberIssues
        {
            MissingNumbers = [2, 5, 6], DuplicateNumbers = [3], DuplicateCells = [1, 2], CellsAfterGap = [1, 2], MaxNumber = 7,
        };
        var vm = Edit(layout);
        Assert.True(vm.HasSourceIssues);
        Assert.Contains("2, 5–6", vm.SourceIssuesText);
        Assert.Contains("3", vm.SourceIssuesText);
        Assert.True(vm.ShowSourceIssuesOnGrid);
        vm.ShowSourceIssues = false;
        Assert.False(vm.ShowSourceIssuesOnGrid);

        vm.ApplyCommand.Execute(null);
        Assert.Null(layout.SourceIssues);
        Assert.Equal([0, 1, 2], layout.Leds);

        _doc.Undo();                                  // one step brings the report back
        Assert.NotNull(layout.SourceIssues);
    }

    [Fact]
    public void A_clean_layout_has_nothing_to_report()
    {
        var vm = Edit(Existing(3, 2, 0, 1));
        Assert.False(vm.HasSourceIssues);
        Assert.Equal("", vm.SourceIssuesText);
        Assert.False(vm.ShowSourceIssuesOnGrid);
    }

    [Fact]
    public void The_outline_of_the_prop_is_kept_for_the_canvas()
    {
        var outline = new CanvasShape("rect1", "M0,0H10V5H0Z", Affine.Identity, null);
        var vm = new LedMarkupViewModel(_doc, null, _video, "prop", _ => { }, outline);
        Assert.Same(outline, vm.Outline);
        Assert.Null(New().Outline);
    }

    [Fact]
    public void A_stored_chain_with_repeats_and_cells_outside_is_shown_repaired_with_a_notice_and_written_back_on_OK()
    {
        var layout = Existing(3, 2, 0, 1, 1, 9, 2);
        var vm = Edit(layout);
        Assert.NotNull(vm.Notice);
        Assert.Equal([0, 1, 2], Chain(vm));
        Assert.False(vm.IsDirty);

        vm.ApplyCommand.Execute(null);
        Assert.Equal([0, 1, 2], layout.Leds);
    }
}

public sealed class EditorLedLayoutTests : IDisposable
{
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly EditorViewModel _editor;

    public EditorLedLayoutTests() => _editor = new EditorViewModel(_doc);

    public void Dispose() => _editor.Dispose();

    [Fact]
    public void The_layout_buttons_follow_the_selected_video_timeline_and_the_window_replaces_the_editor_while_it_is_open()
    {
        _editor.AddVideoTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[^1];
        _editor.Timelines.Select(timeline);
        Assert.False(_editor.CanEditLayout);

        _editor.NewLayoutCommand.Execute(null);
        var markup = _editor.MarkupEditor;
        Assert.NotNull(markup);
        Assert.True(_editor.IsEditingMarkup);
        markup.FillAllCommand.Execute(null);
        markup.ApplyCommand.Execute(null);

        Assert.False(_editor.IsEditingMarkup);
        Assert.True(_editor.CanEditLayout);
        Assert.Equal(256, Assert.Single(_doc.Project.LedLayouts).Leds.Count);   // a new layout starts as a 16 x 16 grid
        Assert.Equal(1, _editor.LayoutIndex);

        _editor.EditLayoutCommand.Execute(null);
        Assert.Equal(256, _editor.MarkupEditor!.Markup.Count);
        _editor.MarkupEditor.CancelCommand.Execute(null);
        Assert.Null(_editor.MarkupEditor);

        _editor.DeleteLayoutCommand.Execute(null);
        Assert.Empty(_doc.Project.LedLayouts);
        Assert.Equal(0, _editor.LayoutIndex);
        Assert.False(_editor.CanEditLayout);

        _doc.Undo();   // delete
        Assert.Equal(1, _editor.LayoutIndex);
    }

    [Fact]
    public void New_layout_needs_a_selected_video_timeline()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        _editor.Timelines.Select(_doc.Project.Timelines[^1]);
        _editor.NewLayoutCommand.Execute(null);
        Assert.Null(_editor.MarkupEditor);
    }
}
