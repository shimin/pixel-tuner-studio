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
public enum Gesture { None, Paint, Line, Fill, Erase, Select }

/// <summary>
/// The LED markup window of a prop, ported from PixelTuner 2's canvas element window (<c>LedMarkupEditor</c> and <c>LedMarkupCanvas</c>):
/// a working copy of the chain, the gestures that change it, and OK / Cancel that hand it to the document as one edit. The canvas only
/// draws this and forwards the pointer as cells; every rule of the chain is in <see cref="LedMarkup"/>. Undo and redo are inside the window
/// (snapshots of the immutable markup), so Cancel leaves the project as it was.
/// </summary>
public sealed partial class LedMarkupViewModel : ObservableObject
{
    private const int UndoLimit = 300;

    private readonly ProjectDocument _document;
    private readonly LedLayout? _layout;
    private readonly Timeline? _use;
    private readonly Action<bool> _done;
    private readonly LedMarkup _original;
    private readonly string _originalName;

    private readonly LinkedList<(LedMarkup Markup, int Next)> _undo = new();
    private readonly Stack<(LedMarkup Markup, int Next)> _redo = new();

    // The pointer that is down.
    private (LedMarkup Markup, int Next) _gestureStart;
    private (int X, int Y) _pressedAt;
    private (int X, int Y) _lastCell;
    private int _lastPlaced = -1;

    /// <param name="layout">The layout to edit; null makes a new one.</param>
    /// <param name="use">For a new layout: the video timeline that takes it.</param>
    /// <param name="done">Called with true when the markup went into the document, false when the user gave up.</param>
    /// <param name="outline">The outline of the prop in the canvas drawing, shown under the grid; null when the layout has none.</param>
    public LedMarkupViewModel(ProjectDocument document, LedLayout? layout, Timeline? use, string name, Action<bool> done, CanvasShape? outline = null)
    {
        _document = document;
        _layout = layout;
        _use = use;
        _done = done;
        Outline = outline;
        _originalSpacing = layout?.TilesMargin ?? DefaultSpacing;
        _spacing = _originalSpacing;
        if (layout is not null && layout.SourceIssues is { IsClean: false } issues) SourceIssues = issues;
        if (layout is null || layout.Columns < 1 || layout.Rows < 1)
        {
            // A prop of the drawing that was never marked up has no grid yet.
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
        _lastPlaced = Markup.Count > 0 ? Markup.CellOf(Markup.Count) : -1;
        ColumnsBox = Markup.Columns;
        RowsBox = Markup.Rows;
    }

    public const int DefaultSize = 16;
    public const int DefaultSpacing = 2;

    private readonly int _originalSpacing;

    // ---- the outline and the problems of a PixelTuner 2 file

    /// <summary>The outline of the prop in the drawing of the canvas, shown under the grid; null when there is none.</summary>
    public CanvasShape? Outline { get; }

    /// <summary>Gaps and repeats in the numbers the project was stored with; until OK compacts them to 1..N.</summary>
    public LedNumberIssues? SourceIssues { get; }

    public bool HasSourceIssues => SourceIssues is { IsClean: false };

    /// <summary>The cells to mark in the grid: where numbers repeat, and the ones that come after a gap.</summary>
    public bool ShowSourceIssuesOnGrid => ShowSourceIssues && HasSourceIssues;

    [ObservableProperty] private bool _showSourceIssues = true;

    partial void OnShowSourceIssuesChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSourceIssuesOnGrid));
        Redraw?.Invoke(this, EventArgs.Empty);
    }

    public string SourceIssuesText
    {
        get
        {
            if (SourceIssues is not { IsClean: false } issues) return "";
            var parts = new List<string>();
            if (issues.MissingNumbers.Count > 0) parts.Add(string.Format(Strings.Markup_SourceGaps, LedNumberIssues.FormatRanges(issues.MissingNumbers)));
            if (issues.DuplicateNumbers.Count > 0) parts.Add(string.Format(Strings.Markup_SourceRepeats, LedNumberIssues.FormatRanges(issues.DuplicateNumbers)));
            return string.Format(Strings.Markup_SourceIssues, string.Join("; ", parts));
        }
    }

    /// <summary>Space between the tiles when they are drawn over the outline, in pixels (0 to 100). Only for looking at.</summary>
    public int Spacing
    {
        get => _spacing;
        set
        {
            int clamped = Math.Clamp(value, 0, Edits.MaxTilesMargin);
            if (clamped == _spacing) return;
            _spacing = clamped;
            OnPropertyChanged();
            Changed();
        }
    }

    private int _spacing;

    // ---- the working copy

    public LedMarkup Markup { get; private set; }

    /// <summary>Raised whenever something the canvas draws changed.</summary>
    public event EventHandler? Redraw;

    [ObservableProperty] private string _name;

    /// <summary>The number the next LED gets: an LED placed now goes in here and the ones behind it shift. Count + 1 appends.</summary>
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

    /// <summary>The last cell a numbering gesture placed: where a Shift click draws its line from.</summary>
    public int LastPlaced => _lastPlaced;

    public bool IsDirty => !Markup.Equals(_original) || Name != _originalName || Spacing != _originalSpacing;

    /// <summary>What was wrong with the stored chain, until the user has looked at it.</summary>
    [ObservableProperty] private string? _notice;

    [ObservableProperty] private string? _hint;

    // What the canvas shows.
    [ObservableProperty] private bool _showNumbers = true;
    [ObservableProperty] private bool _showPath = true;
    [ObservableProperty] private bool _showGradient = true;

    partial void OnShowNumbersChanged(bool value) => Redraw?.Invoke(this, EventArgs.Empty);
    partial void OnShowPathChanged(bool value) => Redraw?.Invoke(this, EventArgs.Empty);
    partial void OnShowGradientChanged(bool value) => Redraw?.Invoke(this, EventArgs.Empty);

    /// <summary>Group operations (lines, fills) renumber the LEDs that are already there instead of leaving them alone.</summary>
    [ObservableProperty] private bool _overwrite;

    private void Changed()
    {
        OnPropertyChanged(nameof(Next));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasLeds));
        OnPropertyChanged(nameof(HoverText));
        OnPropertyChanged(nameof(HasRange));
        OnPropertyChanged(nameof(CanClearTail));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        ReverseRangeCommand.NotifyCanExecuteChanged();
        MoveRangeCommand.NotifyCanExecuteChanged();
        DeleteRangeCommand.NotifyCanExecuteChanged();
        ClearTailCommand.NotifyCanExecuteChanged();
        ClearRangeSelectionCommand.NotifyCanExecuteChanged();
        Redraw?.Invoke(this, EventArgs.Empty);
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(IsDirty));

    private void PushUndo((LedMarkup Markup, int Next) state)
    {
        _undo.AddLast(state);
        if (_undo.Count > UndoLimit) _undo.RemoveFirst();
        _redo.Clear();
    }

    private void SetMarkup(LedMarkup markup, int next)
    {
        if (markup.Columns != Markup.Columns || markup.Rows != Markup.Rows) HoverCell = -1;   // cell numbers mean other places now
        Markup = markup;
        TransferCell = -1;
        _next = Math.Clamp(next, 1, markup.Count + 1);
        if (_lastPlaced < 0 || _lastPlaced >= markup.Columns * markup.Rows || !markup.IsMarked(_lastPlaced))
            _lastPlaced = markup.Count > 0 ? markup.CellOf(markup.Count) : -1;
        ColumnsBox = markup.Columns;
        RowsBox = markup.Rows;
        Hint = null;
        Changed();
    }

    /// <summary>
    /// Runs one operation as one step of the history. A cursor that stood at the end of the chain stays at the end;
    /// otherwise it stays where it was, as far as the chain still reaches.
    /// </summary>
    private void Do(Func<LedMarkup, LedMarkup> operation)
    {
        bool atEnd = _next > Markup.Count;
        var markup = operation(Markup);
        if (markup.Equals(Markup)) return;
        PushUndo((Markup, _next));
        SetMarkup(markup, atEnd ? markup.Count + 1 : _next);
    }

    public bool HasLeds => Markup.Count > 0;

    public string StatusText
    {
        get
        {
            string text = Markup.Count == 0
                ? string.Format(Strings.Markup_StatusEmpty, Next)
                : string.Format(Strings.Markup_Status, Markup.Count, Next);
            int jumps = Markup.Jumps().Count;
            return jumps == 0 ? text : text + " · " + string.Format(Strings.Markup_Jumps, jumps);
        }
    }

    // ---- history

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _redo.Push((Markup, _next));
        var (markup, next) = _undo.Last!.Value;
        _undo.RemoveLast();
        SetMarkup(markup, next);
    }

    private bool CanUndo() => _undo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undo.AddLast((Markup, _next));
        var (markup, next) = _redo.Pop();
        SetMarkup(markup, next);
    }

    private bool CanRedo() => _redo.Count > 0;

    // ---- the pointer

    public Gesture Gesture { get; private set; }

    /// <summary>Cells a line or a fill in the making would add, with the numbers they would get, for the canvas to draw over the markup.</summary>
    public IReadOnlyList<(int Cell, int Number)> Preview { get; private set; } = [];

    /// <summary>The corners (x0, y0, x1, y1) of the rectangle a Ctrl + Shift drag picks a range with; null when there is none.</summary>
    public (int X0, int Y0, int X1, int Y1)? Selection { get; private set; }

    /// <summary>The cell under the pointer, for the status line; -1 when the pointer is away.</summary>
    public int HoverCell { get; private set; } = -1;

    public string HoverText
    {
        get
        {
            if (HoverCell < 0) return "";
            var (x, y) = Markup.Position(HoverCell);
            int number = Markup.NumberOf(HoverCell);
            return number == 0 ? string.Format(Strings.Markup_HoverEmpty, x, y) : string.Format(Strings.Markup_HoverLed, x, y, number);
        }
    }

    public void Hover(int x, int y)
    {
        int cell = Markup.Contains(x, y) ? Markup.Cell(x, y) : -1;
        if (cell == HoverCell) return;
        HoverCell = cell;
        OnPropertyChanged(nameof(HoverText));
        if (Gesture == Gesture.None) Redraw?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The pointer went down on a cell. A plain press places the next LED at once and a drag goes on placing; Shift draws a line
    /// (from the last placed LED when it is only a click, from here to where it is let go when dragged); Ctrl fills a rectangle;
    /// Ctrl + Shift picks the range of LEDs inside a rectangle; Alt moves the cursor behind the LED under it; the right button takes
    /// LEDs out.
    /// </summary>
    public void Press(int x, int y, GestureKeys keys)
    {
        if (Gesture != Gesture.None || !Markup.Contains(x, y)) return;
        _gestureStart = (Markup, _next);
        _pressedAt = _lastCell = (x, y);
        Hint = null;
        int cell = Markup.Cell(x, y);

        if (keys.HasFlag(GestureKeys.Erase))
        {
            Gesture = Gesture.Erase;
            EraseCells([cell]);
        }
        else if (keys.HasFlag(GestureKeys.Alt))
        {
            ContinueAfter(cell);   // no gesture: nothing to finish
        }
        else if (keys.HasFlag(GestureKeys.Ctrl) && keys.HasFlag(GestureKeys.Shift))
        {
            Gesture = Gesture.Select;
            Selection = (x, y, x, y);
            Redraw?.Invoke(this, EventArgs.Empty);
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
            PlaceCells([cell], false);
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
                PlaceCells(Markup.LineCells(_lastCell.X, _lastCell.Y, x, y).Skip(1).ToList(), false);
                break;
            case Gesture.Erase:
                EraseCells(Markup.LineCells(_lastCell.X, _lastCell.Y, x, y));
                break;
            case Gesture.Select:
                Selection = (_pressedAt.X, _pressedAt.Y, x, y);
                Redraw?.Invoke(this, EventArgs.Empty);
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
        var gesture = Gesture;
        if (gesture == Gesture.None) return;
        var end = Markup.Contains(x, y) ? (x, y) : _lastCell;
        var preview = Preview;
        Gesture = Gesture.None;
        Preview = [];
        Selection = null;

        if (gesture is Gesture.Line or Gesture.Fill)
        {
            if (preview.Count > 0) PlaceCells(GestureCells(gesture, end.Item1, end.Item2), Overwrite);
        }
        else if (gesture == Gesture.Select)
        {
            SelectRangeFromCells(Markup.RectCells(_pressedAt.X, _pressedAt.Y, end.Item1, end.Item2, FillDirection.Rows, FillPattern.Straight));
        }

        if (!Markup.Equals(_gestureStart.Markup)) PushUndo(_gestureStart);
        Changed();
    }

    /// <summary>Esc while the pointer is down: the gesture is dropped as if it had not been made. False when there was none.</summary>
    public bool CancelGesture()
    {
        if (Gesture == Gesture.None) return false;
        Gesture = Gesture.None;
        Preview = [];
        Selection = null;
        SetMarkup(_gestureStart.Markup, _gestureStart.Next);
        return true;
    }

    /// <summary>Numbers cells in the order given from <see cref="Next"/> on.</summary>
    private void PlaceCells(IReadOnlyList<int> cells, bool overwrite)
    {
        var wanted = new List<int>();
        foreach (int cell in cells)
            if ((uint)cell < (uint)(Markup.Columns * Markup.Rows) && !wanted.Contains(cell) && (overwrite || !Markup.IsMarked(cell))) wanted.Add(cell);
        if (wanted.Count == 0) return;

        bool atEnd = _next > Markup.Count;
        var markup = Markup.Insert(cells, _next, overwrite);
        _lastPlaced = wanted[^1];
        SetMarkup(markup, atEnd ? markup.Count + 1 : markup.NumberOf(_lastPlaced) + 1);
    }

    private void EraseCells(IEnumerable<int> cells)
    {
        var list = cells.Where(c => Markup.IsMarked(c)).Distinct().ToList();
        if (list.Count == 0) return;
        bool atEnd = _next > Markup.Count;
        int before = list.Count(c => Markup.NumberOf(c) < _next);
        var markup = Markup.Remove(list);
        SetMarkup(markup, atEnd ? markup.Count + 1 : _next - before);
        // The cursor keeps following the LED in front of it.
        if (markup.Count > 0 && !markup.IsMarked(_lastPlaced)) _lastPlaced = markup.CellOf(Math.Clamp(_next - 1, 1, markup.Count));
    }

    /// <summary>Alt + click: numbering goes on right after this LED.</summary>
    private void ContinueAfter(int cell)
    {
        int number = Markup.NumberOf(cell);
        if (number == 0) return;
        _lastPlaced = cell;
        Next = number + 1;
        Changed();
    }

    private IReadOnlyList<int> GestureCells(Gesture gesture, int x, int y)
    {
        if (gesture == Gesture.Line)
        {
            // Dragged, the line goes from where the pointer went down; a click draws it from the last placed LED.
            var from = _pressedAt;
            if ((x, y) == _pressedAt && _lastPlaced >= 0 && Markup.IsMarked(_lastPlaced) && _lastPlaced != Markup.Cell(x, y))
                from = Markup.Position(_lastPlaced);
            return Markup.LineCells(from.X, from.Y, x, y);
        }
        return Markup.RectCells(_pressedAt.X, _pressedAt.Y, x, y, FillDirection, FillPattern);
    }

    private void UpdatePreview(int x, int y)
    {
        var cells = GestureCells(Gesture, x, y);
        // The numbers the cells would get: LEDs that are there are skipped, or, overwriting, taken out first (which moves the place back).
        int number = _next;
        var seen = new HashSet<int>();
        if (Overwrite) number -= cells.Count(c => Markup.IsMarked(c) && Markup.NumberOf(c) < _next && seen.Add(c));
        seen.Clear();
        var preview = new List<(int Cell, int Number)>();
        foreach (int cell in cells)
        {
            if (!seen.Add(cell) || (Markup.IsMarked(cell) && !Overwrite)) continue;
            preview.Add((cell, number++));
        }
        Preview = preview;
        Redraw?.Invoke(this, EventArgs.Empty);
    }

    // ---- moving one LED to a typed number (a double click)

    [ObservableProperty] private int _transferCell = -1;
    [ObservableProperty] private int _transferNumber;

    public bool IsTransferring => TransferCell >= 0;

    partial void OnTransferCellChanged(int value)
    {
        OnPropertyChanged(nameof(IsTransferring));
        Redraw?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The window asks for a new number of the LED under the pointer.</summary>
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
        Do(markup => markup.Place(cell, Math.Clamp(TransferNumber, 1, markup.Count)));
    }

    [RelayCommand]
    private void CancelTransfer() => TransferCell = -1;

    // ---- the fill

    [ObservableProperty] private FillDirection _fillDirection = FillDirection.Rows;
    [ObservableProperty] private FillPattern _fillPattern = FillPattern.Snake;

    /// <summary>The corner the whole matrix starts at; a rectangle starts where the drag began.</summary>
    [ObservableProperty] private Corner _fillCorner = Corner.TopLeft;

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

    /// <summary>The "whole matrix" button: every cell of the grid, in the sweep that is set, replaces the markup.</summary>
    [RelayCommand]
    private void FillAll()
    {
        var cells = Markup.SweepCells(FillCorner, FillDirection, FillPattern);
        Do(markup => markup.Clear().Insert(cells, 1, true));
        _lastPlaced = Markup.Count > 0 ? Markup.CellOf(Markup.Count) : -1;
        Next = Markup.Count + 1;
    }

    // ---- ranges of numbers

    [ObservableProperty] private int _rangeFrom;
    [ObservableProperty] private int _rangeTo;
    [ObservableProperty] private int _moveTarget = 1;

    partial void OnRangeFromChanged(int value) => Changed();
    partial void OnRangeToChanged(int value) => Changed();

    /// <summary>A range is set when both ends are numbers and it reaches into the chain.</summary>
    public bool HasRange => RangeFrom >= 1 && RangeTo >= 1 && Math.Min(RangeFrom, RangeTo) <= Markup.Count;

    public bool IsInRange(int number) => HasRange && number >= Math.Min(RangeFrom, RangeTo) && number <= Math.Max(RangeFrom, RangeTo);

    public bool CanClearTail => HasRange && Math.Max(RangeFrom, RangeTo) < Markup.Count;

    // The ends in either order, held inside the chain.
    private (int From, int To) Range() =>
        (Math.Clamp(Math.Min(RangeFrom, RangeTo), 1, Markup.Count), Math.Clamp(Math.Max(RangeFrom, RangeTo), 1, Markup.Count));

    public void SetRange(int from, int to)
    {
        RangeFrom = Math.Max(0, from);
        RangeTo = Math.Max(0, to);
    }

    /// <summary>A Ctrl + Shift drag: the range becomes the numbers of the LEDs inside the rectangle, and "move to" its first.</summary>
    private void SelectRangeFromCells(IEnumerable<int> cells)
    {
        var numbers = cells.Select(Markup.NumberOf).Where(n => n > 0).ToList();
        if (numbers.Count == 0)
        {
            SetRange(0, 0);
            return;
        }
        SetRange(numbers.Min(), numbers.Max());
        MoveTarget = numbers.Min();
    }

    [RelayCommand(CanExecute = nameof(HasRange))]
    private void ReverseRange()
    {
        var (from, to) = Range();
        Do(markup => markup.Reverse(from, to));
    }

    [RelayCommand(CanExecute = nameof(HasRange))]
    private void DeleteRange()
    {
        var (from, to) = Range();
        bool atEnd = _next > Markup.Count;
        Do(markup => markup.DeleteRange(from, to));
        // The cursor behind the deleted LEDs moves back by as many.
        if (!atEnd && _next > from) Next = Math.Max(from, _next - (to - from + 1));
        SetRange(0, 0);
    }

    /// <summary>The range moves as one block so that it starts at "move to"; it stays selected where it went.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveRange))]
    private void MoveRange()
    {
        var (from, to) = Range();
        int length = to - from + 1;
        int start = Math.Clamp(MoveTarget, 1, Markup.Count - length + 1);
        Do(markup => markup.MoveRange(from, to, start));
        SetRange(start, start + length - 1);
    }

    private bool CanMoveRange() => HasRange && MoveTarget > 0;

    partial void OnMoveTargetChanged(int value) => MoveRangeCommand.NotifyCanExecuteChanged();

    /// <summary>Erases everything after the LED in "to", to mark the end of the strip again.</summary>
    [RelayCommand(CanExecute = nameof(CanClearTail))]
    private void ClearTail()
    {
        var (_, to) = Range();
        Do(markup => markup.ClearTail(to));
        Next = Markup.Count + 1;
    }

    [RelayCommand(CanExecute = nameof(HasRange))]
    private void ClearRangeSelection() => SetRange(0, 0);

    /// <summary>The cursor goes to the end of the chain: the next LED is appended.</summary>
    [RelayCommand]
    private void NextAtEnd() => Next = Markup.Count + 1;

    // ---- the whole markup

    [RelayCommand] private void MirrorHorizontally() => Do(markup => markup.MirrorHorizontally());
    [RelayCommand] private void MirrorVertically() => Do(markup => markup.MirrorVertically());

    /// <summary>A quarter turn clockwise; a grid that is not square turns with it (PixelTuner 2 turned square grids only, because of the outline).</summary>
    [RelayCommand] private void Rotate() => Do(markup => markup.RotateClockwise());

    [RelayCommand] private void ReverseAll() => Do(markup => markup.ReverseAll());
    [RelayCommand] private void ClearAll() => Do(markup => markup.Clear());

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
        int dropped = 0;
        Do(markup =>
        {
            var (resized, count) = markup.Resize(columns, rows);
            dropped = count;
            return resized;
        });
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
            var layout = new LedLayout { Name = name, Columns = Markup.Columns, Rows = Markup.Rows, Leds = [.. Markup.Chain], TilesMargin = Spacing };
            _document.Execute(Edits.AddLedLayout(_document.Project, layout, _use));
        }
        else if (IsDirty || Notice is not null || HasSourceIssues)
        {
            var edits = new List<Edit>();
            if (name != _layout.Name) edits.Add(Edits.RenameLedLayout(_layout, name));
            // A repaired chain and numbers with gaps are written even when nothing else changed: what is stored is not what the editor showed.
            bool markupChanged = !Markup.Equals(_original) || Spacing != _originalSpacing || Notice is not null || HasSourceIssues
                || Markup.Columns != _layout.Columns || Markup.Rows != _layout.Rows;
            if (markupChanged) edits.Add(Edits.SetLedMarkup(_layout, Markup, Spacing != _layout.TilesMargin ? Spacing : null));
            if (edits.Count > 0) _document.Execute(edits.Count == 1 ? edits[0] : new GroupEdit("SetLedMarkup", edits));
        }
        _done(true);
    }

    /// <summary>The Cancel button: the window closes and the project is as it was.</summary>
    [RelayCommand]
    private void Cancel() => _done(false);

    // ---- leaving with changes

    [ObservableProperty] private bool _isAskingToLeave;

    /// <summary>Esc, or the window being closed: with changes the window asks first, without them it just closes.</summary>
    public void RequestClose()
    {
        if (IsDirty) IsAskingToLeave = true;
        else _done(false);
    }

    [RelayCommand]
    private void KeepEditing() => IsAskingToLeave = false;
}
