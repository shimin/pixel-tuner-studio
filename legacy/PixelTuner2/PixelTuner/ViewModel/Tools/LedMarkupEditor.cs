using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace PixelTuner.ViewModel
{
    /// <summary>
    /// Editing session of the LED markup window: working copy of the markup,
    /// numbering settings, undo/redo, validation texts.
    /// </summary>
    public class LedMarkupEditor : INotifyPropertyChanged
    {
        private const int UndoLimit = 300;

        private readonly Stack<LedMarkupSnapshot> _undo = new Stack<LedMarkupSnapshot>();
        private readonly Stack<LedMarkupSnapshot> _redo = new Stack<LedMarkupSnapshot>();
        private LedMarkupSnapshot _gestureStart;
        private readonly LedMarkupSnapshot _initial;

        public LedMarkupEditor(LedMarkup markup, LedMarkupIssues sourceIssues, int spacing)
        {
            Markup = markup;
            SourceIssues = sourceIssues ?? new LedMarkupIssues();
            _initial = markup.Save();
            _spacing = spacing;
            _columnsInput = markup.Columns;
            _rowsInput = markup.Rows;
            _nextNumber = markup.Count + 1;
            LastPlacedCell = markup.Count > 0 ? markup.GetCell(markup.Count) : -1;

            UndoCommand = new RelayCommand(o => Undo(), o => CanUndo);
            RedoCommand = new RelayCommand(o => Redo(), o => CanRedo);
            ApplySizeCommand = new RelayCommand(o => ApplySize());
            FillAllCommand = new RelayCommand(o => FillAll());
            ClearAllCommand = new RelayCommand(o => Do(() => Markup.Clear()), o => Markup.Count > 0);
            ReverseAllCommand = new RelayCommand(o => Do(() => Markup.ReverseAll()), o => Markup.Count > 1);
            MirrorHorizontalCommand = new RelayCommand(o => Do(() => Markup.MirrorHorizontal()), o => Markup.Count > 0);
            MirrorVerticalCommand = new RelayCommand(o => Do(() => Markup.MirrorVertical()), o => Markup.Count > 0);
            RotateCommand = new RelayCommand(o => Do(() => Markup.RotateClockwise()), o => Markup.Count > 0 && Markup.Columns == Markup.Rows);
            ReverseRangeCommand = new RelayCommand(o => ReverseRange(), o => HasRange);
            DeleteRangeCommand = new RelayCommand(o => DeleteRange(), o => HasRange);
            MoveRangeCommand = new RelayCommand(o => MoveRange(), o => HasRange && MoveTarget > 0);
            ClearRangeSelectionCommand = new RelayCommand(o => SetRange(0, 0), o => HasRange);
            NextAtEndCommand = new RelayCommand(o => NextNumber = Markup.Count + 1);
        }

        public event EventHandler MarkupChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public LedMarkup Markup { get; private set; }

        /// <summary>Problems of the numbers stored in the project before this session.</summary>
        public LedMarkupIssues SourceIssues { get; private set; }

        /// <summary>Last cell placed by a numbering gesture (start of a Shift+click line).</summary>
        public int LastPlacedCell { get; private set; }

        public bool IsChanged { get { return !Markup.SameAs(_initial); } }

        #region Settings

        private int _nextNumber;
        /// <summary>Number the next placed LED gets. Count+1 appends to the end of the chain.</summary>
        public int NextNumber
        {
            get { return _nextNumber; }
            set
            {
                var v = Math.Max(1, Math.Min(value, Markup.Count + 1));
                if (_nextNumber == v) { Raise("NextNumber"); return; }
                _nextNumber = v;
                Raise("NextNumber");
                Raise("StatusText");
            }
        }

        private bool _overwrite;
        /// <summary>Group operations renumber already numbered cells instead of skipping them.</summary>
        public bool Overwrite { get { return _overwrite; } set { _overwrite = value; Raise("Overwrite"); } }

        private bool _snake = true;
        public bool Snake { get { return _snake; } set { _snake = value; Raise("Snake"); } }

        private bool _byColumns;
        public bool ByColumns { get { return _byColumns; } set { _byColumns = value; Raise("ByColumns"); } }

        private int _startCorner;
        /// <summary>Start corner for "fill whole matrix": 0 top-left, 1 top-right, 2 bottom-left, 3 bottom-right.</summary>
        public int StartCorner { get { return _startCorner; } set { _startCorner = value; Raise("StartCorner"); } }

        private bool _showNumbers = true;
        public bool ShowNumbers { get { return _showNumbers; } set { _showNumbers = value; Raise("ShowNumbers"); OnChanged(false); } }

        private bool _showPath = true;
        public bool ShowPath { get { return _showPath; } set { _showPath = value; Raise("ShowPath"); OnChanged(false); } }

        private bool _showGradient = true;
        public bool ShowGradient { get { return _showGradient; } set { _showGradient = value; Raise("ShowGradient"); OnChanged(false); } }

        private bool _showSourceIssues = true;
        public bool ShowSourceIssues { get { return _showSourceIssues; } set { _showSourceIssues = value; Raise("ShowSourceIssues"); OnChanged(false); } }

        private int _spacing;
        public int Spacing
        {
            get { return _spacing; }
            set { _spacing = Math.Max(0, Math.Min(100, value)); Raise("Spacing"); OnChanged(false); }
        }

        private int _columnsInput;
        public int ColumnsInput { get { return _columnsInput; } set { _columnsInput = Math.Max(0, value); Raise("ColumnsInput"); } }

        private int _rowsInput;
        public int RowsInput { get { return _rowsInput; } set { _rowsInput = Math.Max(0, value); Raise("RowsInput"); } }

        private int _rangeFrom;
        public int RangeFrom { get { return _rangeFrom; } set { _rangeFrom = Math.Max(0, value); RangeChanged(); } }

        private int _rangeTo;
        public int RangeTo { get { return _rangeTo; } set { _rangeTo = Math.Max(0, value); RangeChanged(); } }

        private int _moveTarget = 1;
        public int MoveTarget { get { return _moveTarget; } set { _moveTarget = Math.Max(1, value); Raise("MoveTarget"); } }

        public bool HasRange
        {
            get { return _rangeFrom >= 1 && _rangeTo >= 1 && Math.Min(_rangeFrom, _rangeTo) <= Markup.Count; }
        }

        public bool IsInRange(int number)
        {
            if (!HasRange || number < 1) return false;
            return number >= Math.Min(_rangeFrom, _rangeTo) && number <= Math.Max(_rangeFrom, _rangeTo);
        }

        public void SetRange(int from, int to)
        {
            _rangeFrom = Math.Max(0, from);
            _rangeTo = Math.Max(0, to);
            RangeChanged();
        }

        private void RangeChanged()
        {
            Raise("RangeFrom");
            Raise("RangeTo");
            Raise("HasRange");
            OnChanged(false);
        }

        #endregion

        #region Texts

        private string _hoverText = "";
        public string HoverText { get { return _hoverText; } set { _hoverText = value; Raise("HoverText"); } }

        private string _message = "";
        /// <summary>Last operation result worth telling (e.g. LEDs dropped by resize).</summary>
        public string Message { get { return _message; } set { _message = value; Raise("Message"); } }

        public string StatusText
        {
            get
            {
                var count = Markup.Count;
                if (count == 0) return L("Markup_StatusEmpty", "No LEDs numbered");
                return string.Format(L("Markup_Status", "LEDs: {0} (1–{0}), no gaps or repeats · next: {1}"), count, NextNumber);
            }
        }

        public bool HasSourceIssues { get { return !SourceIssues.IsClean; } }

        public string SourceIssuesText
        {
            get
            {
                if (SourceIssues.IsClean) return "";
                var parts = new List<string>();
                if (SourceIssues.MissingNumbers.Count > 0)
                    parts.Add(string.Format(L("Markup_SourceGaps", "gaps: {0}"), LedMarkupIssues.FormatRanges(SourceIssues.MissingNumbers)));
                if (SourceIssues.DuplicateNumbers.Count > 0)
                    parts.Add(string.Format(L("Markup_SourceRepeats", "repeats: {0}"), LedMarkupIssues.FormatRanges(SourceIssues.DuplicateNumbers)));
                return string.Format(L("Markup_SourceIssues",
                    "Saved numbering had {0}. The strip already received LEDs in number order, so on OK numbers are compacted to 1–N in the same order. Marked cells are highlighted."),
                    string.Join("; ", parts));
            }
        }

        public void SetHover(int cell)
        {
            if (!Markup.IsValidCell(cell)) { HoverText = ""; return; }
            var n = Markup.GetNumber(cell);
            HoverText = string.Format("X {0}, Y {1}", Markup.CellX(cell), Markup.CellY(cell))
                        + (n > 0 ? "  →  № " + n : "");
        }

        #endregion

        #region Undo

        public bool CanUndo { get { return _undo.Count > 0; } }
        public bool CanRedo { get { return _redo.Count > 0; } }

        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            _redo.Push(Markup.Save());
            Markup.Restore(_undo.Pop());
            AfterRestore();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            PushUndo(Markup.Save(), false);
            Markup.Restore(_redo.Pop());
            AfterRestore();
        }

        private void AfterRestore()
        {
            ColumnsInput = Markup.Columns;
            RowsInput = Markup.Rows;
            NextNumber = Markup.Count + 1;
            LastPlacedCell = Markup.Count > 0 ? Markup.GetCell(Markup.Count) : -1;
            OnChanged(true);
        }

        private void PushUndo(LedMarkupSnapshot s, bool clearRedo)
        {
            _undo.Push(s);
            if (_undo.Count > UndoLimit)
            {
                var keep = _undo.Take(UndoLimit).Reverse().ToList();
                _undo.Clear();
                foreach (var k in keep) _undo.Push(k);
            }
            if (clearRedo) _redo.Clear();
        }

        #endregion

        #region Gestures (used by the canvas)

        public bool IsGestureActive { get { return _gestureStart != null; } }

        public void BeginGesture()
        {
            if (_gestureStart == null) _gestureStart = Markup.Save();
        }

        public void EndGesture()
        {
            if (_gestureStart == null) return;
            if (!Markup.SameAs(_gestureStart)) PushUndo(_gestureStart, true);
            _gestureStart = null;
            OnChanged(true);
        }

        public void CancelGesture()
        {
            if (_gestureStart == null) return;
            Markup.Restore(_gestureStart);
            _gestureStart = null;
            AfterRestore();
        }

        /// <summary>Numbers cells in the given order starting at NextNumber.</summary>
        public void PlaceCells(IList<int> cells, bool overwrite)
        {
            if (cells == null || cells.Count == 0) return;
            var placed = cells.Where(c => Markup.IsValidCell(c) && (overwrite || !Markup.IsNumbered(c))).ToList();
            var atEnd = NextNumber > Markup.Count;
            var next = Markup.Insert(cells, NextNumber, overwrite);
            if (placed.Count > 0) LastPlacedCell = placed[placed.Count - 1];
            _nextNumber = atEnd ? Markup.Count + 1 : next;
            NextNumber = _nextNumber;
            OnChanged(false);
        }

        /// <summary>Takes cells out of the chain; numbering after them closes up.</summary>
        public void EraseCells(IEnumerable<int> cells)
        {
            var list = cells.Where(Markup.IsNumbered).Distinct().ToList();
            if (list.Count == 0) return;
            var atEnd = NextNumber > Markup.Count;
            var before = list.Count(c => Markup.GetNumber(c) < NextNumber);
            Markup.Remove(list);
            NextNumber = atEnd ? Markup.Count + 1 : NextNumber - before;
            if (!Markup.IsNumbered(LastPlacedCell)) LastPlacedCell = Markup.Count > 0 ? Markup.GetCell(Math.Max(1, NextNumber - 1)) : -1;
            OnChanged(false);
        }

        /// <summary>Alt+click: continue numbering right after this LED.</summary>
        public void ContinueAfter(int cell)
        {
            var n = Markup.GetNumber(cell);
            if (n == 0) return;
            NextNumber = n + 1;
            LastPlacedCell = cell;
            OnChanged(false);
        }

        /// <summary>Sets the range to the numbers found in the given cells.</summary>
        public void SelectRangeFromCells(IEnumerable<int> cells)
        {
            var numbers = cells.Select(Markup.GetNumber).Where(n => n > 0).ToList();
            if (numbers.Count == 0) { SetRange(0, 0); return; }
            SetRange(numbers.Min(), numbers.Max());
            MoveTarget = numbers.Min();
        }

        /// <summary>Moves a single LED to a new number (double click).</summary>
        public void MoveCellTo(int cell, int number)
        {
            Do(() => Markup.MoveCellTo(cell, number));
        }

        #endregion

        #region Commands

        public ICommand ApplySizeCommand { get; private set; }
        public ICommand FillAllCommand { get; private set; }
        public ICommand ClearAllCommand { get; private set; }
        public ICommand ReverseAllCommand { get; private set; }
        public ICommand MirrorHorizontalCommand { get; private set; }
        public ICommand MirrorVerticalCommand { get; private set; }
        public ICommand RotateCommand { get; private set; }
        public ICommand ReverseRangeCommand { get; private set; }
        public ICommand DeleteRangeCommand { get; private set; }
        public ICommand MoveRangeCommand { get; private set; }
        public ICommand ClearRangeSelectionCommand { get; private set; }
        public ICommand NextAtEndCommand { get; private set; }

        /// <summary>Runs one operation as a single undo step.</summary>
        public void Do(Action action)
        {
            var before = Markup.Save();
            var atEnd = NextNumber > Markup.Count;
            action();
            if (Markup.SameAs(before)) return;
            PushUndo(before, true);
            if (atEnd || NextNumber > Markup.Count + 1) NextNumber = Markup.Count + 1;
            if (!Markup.IsNumbered(LastPlacedCell)) LastPlacedCell = Markup.Count > 0 ? Markup.GetCell(Markup.Count) : -1;
            OnChanged(true);
        }

        public void ApplySize()
        {
            if (ColumnsInput == Markup.Columns && RowsInput == Markup.Rows) return;
            var dropped = 0;
            Do(() => dropped = Markup.Resize(ColumnsInput, RowsInput));
            Message = dropped > 0
                ? string.Format(L("Markup_ResizeDropped", "{0} LEDs outside the new size were removed, numbering closed up (Ctrl+Z to undo)."), dropped)
                : "";
            Raise("GridSize");
        }

        public void FillAll()
        {
            if (Markup.CellCount == 0) return;
            int maxX = Markup.Columns - 1, maxY = Markup.Rows - 1;
            int x0 = (StartCorner == 1 || StartCorner == 3) ? maxX : 0;
            int y0 = (StartCorner == 2 || StartCorner == 3) ? maxY : 0;
            var cells = LedMarkup.RectCells(Markup, x0, y0, maxX - x0, maxY - y0, ByColumns, Snake);
            Do(() =>
            {
                Markup.Clear();
                Markup.Insert(cells, 1, true);
            });
            NextNumber = Markup.Count + 1;
            LastPlacedCell = Markup.Count > 0 ? Markup.GetCell(Markup.Count) : -1;
        }

        private void ReverseRange()
        {
            Do(() => Markup.ReverseRange(RangeFrom, RangeTo));
        }

        private void DeleteRange()
        {
            var from = Math.Min(RangeFrom, RangeTo);
            var to = Math.Max(RangeFrom, RangeTo);
            var atEnd = NextNumber > Markup.Count;
            var removed = 0;
            Do(() => removed = Markup.RemoveRange(from, to));
            if (!atEnd && NextNumber > from) NextNumber = Math.Max(from, NextNumber - removed);
            SetRange(0, 0);
        }

        private void MoveRange()
        {
            var from = Math.Min(RangeFrom, RangeTo);
            var to = Math.Max(RangeFrom, RangeTo);
            var length = Math.Min(to, Markup.Count) - from + 1;
            Do(() => Markup.MoveRange(from, to, MoveTarget));
            var start = Math.Max(1, Math.Min(MoveTarget, Markup.Count - length + 1));
            SetRange(start, start + length - 1);
        }

        #endregion

        public LedMarkupSnapshot Result { get { return Markup.Save(); } }

        private void OnChanged(bool structural)
        {
            if (structural)
            {
                Raise("CanUndo");
                Raise("CanRedo");
                Raise("HasRange");
                CommandManager.InvalidateRequerySuggested();
            }
            Raise("StatusText");
            var h = MarkupChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void Raise(string name)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }

        internal static string L(string key, string fallback)
        {
            var app = Application.Current;
            var s = app == null ? null : app.TryFindResource(key) as string;
            return string.IsNullOrEmpty(s) ? fallback : s;
        }
    }
}
