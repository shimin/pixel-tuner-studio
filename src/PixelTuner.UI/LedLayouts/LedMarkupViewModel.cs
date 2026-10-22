using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Layout;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.LedLayouts;

/// <summary>The keys held while the pointer goes down; Ctrl stands for Cmd on a Mac.</summary>
[Flags]
public enum GestureKeys
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4,

    /// <summary>The right button: takes LEDs out.</summary>
    Erase = 8,
}

/// <summary>What a pointer that is down is doing.</summary>
public enum Gesture { None, Paint, Line, Fill, Erase }

/// <summary>
/// The LED markup window of a prop: a working copy of the chain, the gestures that change it, and OK / Cancel that hand it to the
/// document as one edit. The canvas only draws this and forwards the pointer as cells; every rule is in <see cref="LedMarkup"/>.
/// Undo and redo are inside the window (snapshots of the immutable markup), so Cancel leaves the project as it was.
/// </summary>
public sealed partial class LedMarkupViewModel : ObservableObject
{
    private readonly ProjectDocument _document;
    private readonly LedLayout? _layout;
    private readonly Timeline? _use;
    private readonly Action<bool> _done;
    private readonly LedMarkup _original;
    private readonly string _originalName;

    private readonly Stack<(LedMarkup Markup, int Next)> _undo = new();
    private readonly Stack<(LedMarkup Markup, int Next)> _redo = new();

    // The pointer that is down.
    private (LedMarkup Markup, int Next) _gestureStart;
    private (int X, int Y) _pressedAt;
    private (int X, int Y) _lastCell;

    /// <param name="layout">The layout to edit; null makes a new one.</param>
    /// <param name="use">For a new layout: the video timeline that takes it.</param>
    /// <param name="done">Called with true when the markup went into the document, false when the user gave up.</param>
    public LedMarkupViewModel(ProjectDocument document, LedLayout? layout, Timeline? use, string name, Action<bool> done)
    {
        _document = document;
        _layout = layout;
        _use = use;
        _done = done;
        if (layout is null)
        {
            _original = new LedMarkup(DefaultSize, DefaultSize);
        }
        else
        {
            var diagnosis = LedMarkup.Diagnose(layout.Columns, layout.Rows, layout.Leds);
            _original = LedMarkup.Load(layout.Columns, layout.Rows, layout.Leds);
            if (!diagnosis.IsClean)
                Notice = string.Format(Strings.Markup_Repaired, diagnosis.Duplicates.Count, diagnosis.OutOfGrid.Count);
        }
        _originalName = name;
        _name = name;
        Markup = _original;
        _next = Markup.Count + 1;
        ColumnsBox = Markup.Columns;
        RowsBox = Markup.Rows;
        RangeFrom = 1;
        RangeTo = Markup.Count;
    }

    public const int DefaultSize = 16;

    // ---- the working copy

    public LedMarkup Markup { get; private set; }

    /// <summary>Raised whenever something the canvas draws changed.</summary>
    public event EventHandler? Redraw;

    [ObservableProperty] private string _name;

    /// <summary>The number the next LED gets: an LED placed now goes in here and the ones behind it shift.</summary>
    public int Next
    {
        get => _next;
        set
        {
            int clamped = Math.Clamp(value, 1, Markup.Count + 1);
            if (clamped == _next) return;
            _next = clamped;
            Changed();
        }
    }

    private int _next;

    public bool IsDirty => !Markup.Equals(_original) || Name != _originalName;

    /// <summary>What was wrong with the stored chain, until the user has looked at it.</summary>
    [ObservableProperty] private string? _notice;

    [ObservableProperty] private string? _hint;

    private void Changed()
    {
        OnPropertyChanged(nameof(Next));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasLeds));
        OnPropertyChanged(nameof(HoverText));
        OnPropertyChanged(nameof(RangeValid));
        OnPropertyChanged(nameof(CanMoveRange));
        OnPropertyChanged(nameof(CanClearTail));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        ReverseRangeCommand.NotifyCanExecuteChanged();
        MoveRangeCommand.NotifyCanExecuteChanged();
        DeleteRangeCommand.NotifyCanExecuteChanged();
        ClearTailCommand.NotifyCanExecuteChanged();
        Redraw?.Invoke(this, EventArgs.Empty);
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(IsDirty));

    /// <summary>Takes a new markup in as one step of the window's own history.</summary>
    private void Do(LedMarkup markup, int next)
    {
        if (markup.Equals(Markup) && next == _next) return;
        _undo.Push((Markup, _next));
        _redo.Clear();
        SetMarkup(markup, next);
    }

    private void SetMarkup(LedMarkup markup, int next)
    {
        if (markup.Columns != Markup.Columns || markup.Rows != Markup.Rows) HoverCell = -1;   // cell numbers mean other places now
        Markup = markup;
        TransferCell = -1;
        _next = Math.Clamp(next, 1, markup.Count + 1);
        ColumnsBox = markup.Columns;
        RowsBox = markup.Rows;
        Hint = null;
        Changed();
    }

    public bool HasLeds => Markup.Count > 0;

    public string StatusText
    {
        get
        {
            string text = string.Format(Strings.Markup_Status, Markup.Count, Next);
            int jumps = Markup.Jumps().Count;
            return jumps == 0 ? text : text + " · " + string.Format(Strings.Markup_Jumps, jumps);
        }
    }

    // ---- history

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _redo.Push((Markup, _next));
        var (markup, next) = _undo.Pop();
        SetMarkup(markup, next);
    }

    private bool CanUndo() => _undo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undo.Push((Markup, _next));
        var (markup, next) = _redo.Pop();
        SetMarkup(markup, next);
    }

    private bool CanRedo() => _redo.Count > 0;

    // ---- the pointer

    public Gesture Gesture { get; private set; }

    /// <summary>Cells a gesture would add, in the order they get numbers, for the canvas to draw over the markup.</summary>
    public IReadOnlyList<int> Preview { get; private set; } = [];

    /// <summary>The number the first cell of the <see cref="Preview"/> would get.</summary>
    public int PreviewFrom => _gestureStart.Next;

    /// <summary>The cell under the pointer, for the status line; -1 when the pointer is away.</summary>
    public int HoverCell { get; private set; } = -1;

    public string HoverText
    {
        get
        {
            if (HoverCell < 0) return "";
            var (x, y) = Markup.Position(HoverCell);
            int number = Markup.NumberOf(HoverCell);
            return number == 0 ? string.Format(Strings.Markup_HoverEmpty, x + 1, y + 1) : string.Format(Strings.Markup_HoverLed, x + 1, y + 1, number);
        }
    }

    public void Hover(int x, int y)
    {
        int cell = Markup.Contains(x, y) ? Markup.Cell(x, y) : -1;
        if (cell == HoverCell) return;
        HoverCell = cell;
        OnPropertyChanged(nameof(HoverText));
    }

    /// <summary>
    /// The pointer went down on a cell. A plain press places the next LED at once and a drag goes on placing; Shift draws a line
    /// (from the last LED when it is only a click, from here to where it is let go when dragged); Ctrl fills a rectangle; Alt moves the
    /// cursor behind the LED under it; the right button takes LEDs out.
    /// </summary>
    public void Press(int x, int y, GestureKeys keys)
    {
        if (!Markup.Contains(x, y)) return;
        _gestureStart = (Markup, _next);
        _pressedAt = _lastCell = (x, y);
        Hint = null;
        int cell = Markup.Cell(x, y);

        if (keys.HasFlag(GestureKeys.Erase))
        {
            Gesture = Gesture.Erase;
            Erase(cell);
        }
        else if (keys.HasFlag(GestureKeys.Alt))
        {
            // The cursor goes behind this LED: the next one placed gets the number after it.
            Gesture = Gesture.None;
            if (Markup.NumberOf(cell) is > 0 and var number) Next = number + 1;
        }
        else if (keys.HasFlag(GestureKeys.Ctrl))
        {
            Gesture = Gesture.Fill;
            UpdatePreview(x, y);
        }
        else if (keys.HasFlag(GestureKeys.Shift))
        {
            Gesture = Gesture.Line;
            UpdatePreview(x, y);
        }
        else
        {
            Gesture = Gesture.Paint;
            PaintCell(cell);
        }
    }

    /// <summary>The pointer moved over the grid, button down or not.</summary>
    public void Move(int x, int y)
    {
        Hover(x, y);
        if (Gesture == Gesture.None || !Markup.Contains(x, y) || (x, y) == _lastCell) return;
        switch (Gesture)
        {
            case Gesture.Paint:
                foreach (var (cx, cy) in CellsBetween(_lastCell.X, _lastCell.Y, x, y)) PaintCell(Markup.Cell(cx, cy));
                break;
            case Gesture.Erase:
                foreach (var (cx, cy) in CellsBetween(_lastCell.X, _lastCell.Y, x, y)) Erase(Markup.Cell(cx, cy));
                break;
            default:
                UpdatePreview(x, y);
                break;
        }
        _lastCell = (x, y);
    }

    /// <summary>The pointer went up: a line or a fill is made now, and the whole gesture is one step of the history.</summary>
    public void Release(int x, int y)
    {
        if (Gesture == Gesture.None) return;
        if (Gesture is Gesture.Line or Gesture.Fill)
        {
            var end = Markup.Contains(x, y) ? (x, y) : _lastCell;
            var cells = GestureCells(end.Item1, end.Item2);
            Preview = [];
            if (cells.Count == 0 && Gesture == Gesture.Line)
            {
                Hint = Strings.Markup_NotOnALine;
            }
            else
            {
                var markup = Markup.Insert(cells, _next);
                SetMarkup(markup, _next + (markup.Count - Markup.Count));
            }
        }

        Gesture = Gesture.None;
        Preview = [];
        if (!Markup.Equals(_gestureStart.Markup))
        {
            _undo.Push(_gestureStart);
            _redo.Clear();
        }
        Changed();
    }

    /// <summary>Esc while the pointer is down: the gesture is dropped as if it had not been made.</summary>
    public void CancelGesture()
    {
        if (Gesture == Gesture.None) return;
        Gesture = Gesture.None;
        Preview = [];
        SetMarkup(_gestureStart.Markup, _gestureStart.Next);
    }

    private void PaintCell(int cell)
    {
        if (Markup.IsMarked(cell)) return;   // a click on a LED never erases it
        var markup = Markup.Insert([cell], _next);
        SetMarkup(markup, _next + 1);
    }

    private void Erase(int cell)
    {
        if (!Markup.IsMarked(cell)) return;
        int number = Markup.NumberOf(cell);
        var markup = Markup.Remove(cell);
        // The cursor keeps its LED: when the erased one was in front of it, it moves back with the tail.
        SetMarkup(markup, number < _next ? _next - 1 : _next);
    }

    private IReadOnlyList<int> GestureCells(int x, int y)
    {
        if (Gesture == Gesture.Line)
        {
            if ((x, y) != _pressedAt) return Markup.LineCells(Markup.Cell(_pressedAt.X, _pressedAt.Y), Markup.Cell(x, y));
            // Only a click: from the last LED to here, and that LED is there already.
            int last = _next > 1 && _next - 1 <= Markup.Count ? Markup.CellOf(_next - 1) : -1;
            return last < 0 ? [Markup.Cell(x, y)] : Markup.LineCells(last, Markup.Cell(x, y));
        }

        int left = Math.Min(_pressedAt.X, x), top = Math.Min(_pressedAt.Y, y);
        int width = Math.Abs(x - _pressedAt.X) + 1, height = Math.Abs(y - _pressedAt.Y) + 1;
        var corner = FillFromDragStart
            ? (_pressedAt.X > x, _pressedAt.Y > y) switch
            {
                (false, false) => Corner.TopLeft,
                (true, false) => Corner.TopRight,
                (false, true) => Corner.BottomLeft,
                _ => Corner.BottomRight,
            }
            : FillCorner;
        return Markup.FillCells(left, top, width, height, corner, FillDirection, FillPattern);
    }

    private void UpdatePreview(int x, int y)
    {
        // Cells that are LEDs already are left alone by the insert, so they are not shown as new.
        Preview = GestureCells(x, y).Where(c => !Markup.IsMarked(c)).ToList();
        Redraw?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The cells a straight pointer move passes between two cells, the first one left out (a fast drag does not skip cells).</summary>
    public static IEnumerable<(int X, int Y)> CellsBetween(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (x0 != x1 || y0 != y1)
        {
            int doubled = 2 * error;
            if (doubled >= dy) { error += dy; x0 += sx; }
            if (doubled <= dx) { error += dx; y0 += sy; }
            yield return (x0, y0);
        }
    }

    // ---- moving one LED to a typed number (a double click)

    [ObservableProperty] private int _transferCell = -1;
    [ObservableProperty] private int _transferNumber;

    public bool IsTransferring => TransferCell >= 0;

    partial void OnTransferCellChanged(int value) => OnPropertyChanged(nameof(IsTransferring));

    public void BeginTransfer(int x, int y)
    {
        if (!Markup.Contains(x, y) || !Markup.IsMarked(Markup.Cell(x, y))) return;
        TransferCell = Markup.Cell(x, y);
        TransferNumber = Markup.NumberOf(TransferCell);
    }

    /// <summary>The LED takes the typed number and the ones between move over; it is not copied, so no number is used twice.</summary>
    [RelayCommand]
    private void CommitTransfer()
    {
        if (TransferCell < 0) return;
        int cell = TransferCell;
        TransferCell = -1;
        Do(Markup.Place(cell, Math.Clamp(TransferNumber, 1, Markup.Count)), _next);
    }

    [RelayCommand]
    private void CancelTransfer() => TransferCell = -1;

    // ---- the fill

    [ObservableProperty] private bool _fillFromDragStart = true;
    [ObservableProperty] private Corner _fillCorner = Corner.TopLeft;
    [ObservableProperty] private FillDirection _fillDirection = FillDirection.Rows;
    [ObservableProperty] private FillPattern _fillPattern = FillPattern.Snake;

    public bool FillCornerIsFree => !FillFromDragStart;

    // The boxes of the window pick by position; the order of the items there is the order of the enums.
    public int FillCornerIndex
    {
        get => (int)FillCorner;
        set { if (value >= 0) FillCorner = (Corner)value; }
    }

    public int FillDirectionIndex
    {
        get => (int)FillDirection;
        set { if (value >= 0) FillDirection = (FillDirection)value; }
    }

    public int FillPatternIndex
    {
        get => (int)FillPattern;
        set { if (value >= 0) FillPattern = (FillPattern)value; }
    }

    partial void OnFillCornerChanged(Corner value) => OnPropertyChanged(nameof(FillCornerIndex));
    partial void OnFillDirectionChanged(FillDirection value) => OnPropertyChanged(nameof(FillDirectionIndex));
    partial void OnFillPatternChanged(FillPattern value) => OnPropertyChanged(nameof(FillPatternIndex));

    partial void OnFillFromDragStartChanged(bool value) => OnPropertyChanged(nameof(FillCornerIsFree));

    /// <summary>The "whole matrix" button: every cell of the grid, in the sweep that is set, replaces the markup.</summary>
    [RelayCommand]
    private void FillAll()
    {
        var cells = Markup.SweepCells(FillCorner, FillDirection, FillPattern);
        var markup = Markup.Clear().Insert(cells, 1);
        Do(markup, markup.Count + 1);
    }

    // ---- ranges of numbers

    [ObservableProperty] private int _rangeFrom = 1;
    [ObservableProperty] private int _rangeTo;
    [ObservableProperty] private int _rangeMoveTo = 1;

    partial void OnRangeFromChanged(int value) => Changed();
    partial void OnRangeToChanged(int value) => Changed();
    partial void OnRangeMoveToChanged(int value) => Changed();

    public bool RangeValid => RangeFrom >= 1 && RangeTo <= Markup.Count && RangeFrom <= RangeTo;
    public bool CanMoveRange => RangeValid && RangeMoveTo >= 1 && RangeMoveTo <= Markup.Count - (RangeTo - RangeFrom + 1) + 1;
    public bool CanClearTail => HasLeds && RangeTo >= 0 && RangeTo < Markup.Count;

    [RelayCommand(CanExecute = nameof(RangeValid))]
    private void ReverseRange() => Do(Markup.Reverse(RangeFrom, RangeTo), _next);

    [RelayCommand(CanExecute = nameof(CanMoveRange))]
    private void MoveRange() => Do(Markup.MoveRange(RangeFrom, RangeTo, RangeMoveTo), _next);

    [RelayCommand(CanExecute = nameof(RangeValid))]
    private void DeleteRange()
    {
        var markup = Markup.DeleteRange(RangeFrom, RangeTo);
        Do(markup, markup.Count + 1);
    }

    /// <summary>Erases everything after the LED in "to", to mark the end of the strip again.</summary>
    [RelayCommand(CanExecute = nameof(CanClearTail))]
    private void ClearTail()
    {
        var markup = Markup.ClearTail(RangeTo);
        Do(markup, markup.Count + 1);
    }

    // ---- the whole markup

    [RelayCommand] private void MirrorHorizontally() => Do(Markup.MirrorHorizontally(), _next);
    [RelayCommand] private void MirrorVertically() => Do(Markup.MirrorVertically(), _next);
    [RelayCommand] private void Rotate() => Do(Markup.RotateClockwise(), _next);
    [RelayCommand] private void ReverseAll() => Do(Markup.ReverseAll(), _next);

    [RelayCommand]
    private void ClearAll() => Do(Markup.Clear(), 1);

    // ---- the grid

    [ObservableProperty] private int _columnsBox;
    [ObservableProperty] private int _rowsBox;

    /// <summary>Gives the grid another size; LEDs keep their column and row, those that fall outside are dropped (and counted).</summary>
    [RelayCommand]
    private void ApplySize()
    {
        int columns = ColumnsBox, rows = RowsBox;
        if (columns < 1 || rows < 1 || (long)columns * rows > Edits.MaxLedGridCells)
        {
            Hint = string.Format(Strings.Markup_BadSize, Edits.MaxLedGridCells);
            ColumnsBox = Markup.Columns;
            RowsBox = Markup.Rows;
            return;
        }
        if (columns == Markup.Columns && rows == Markup.Rows) return;
        var (markup, dropped) = Markup.Resize(columns, rows);
        Do(markup, _next);
        Hint = dropped == 0 ? null : string.Format(Strings.Markup_Dropped, dropped);
    }

    // ---- the end

    /// <summary>Hands the markup to the document as one edit (a new layout is added in the same step).</summary>
    [RelayCommand]
    private void Apply()
    {
        Gesture = Gesture.None;
        string name = Name.Trim();
        if (_layout is null)
        {
            var layout = new LedLayout { Name = name, Columns = Markup.Columns, Rows = Markup.Rows, Leds = [.. Markup.Chain] };
            _document.Execute(Edits.AddLedLayout(_document.Project, layout, _use));
        }
        else if (IsDirty || Notice is not null)
        {
            var edits = new List<Edit>();
            if (name != _layout.Name) edits.Add(Edits.RenameLedLayout(_layout, name));
            // A repaired chain is written even when the user changed nothing else: the stored one was not what the editor showed.
            if (!Markup.Equals(_original) || Notice is not null) edits.Add(Edits.SetLedMarkup(_layout, Markup));
            if (edits.Count > 0) _document.Execute(edits.Count == 1 ? edits[0] : new GroupEdit("SetLedMarkup", edits));
        }
        _done(true);
    }

    [RelayCommand]
    private void Cancel() => _done(false);
}
