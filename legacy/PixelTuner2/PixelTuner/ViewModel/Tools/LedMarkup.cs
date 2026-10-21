using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelTuner.ViewModel
{
    /// <summary>
    /// LED strip markup as an ordered chain of grid cells.
    /// LED number = position in the chain + 1, so numbers are always 1..N
    /// with no gaps and no duplicates. Cell index = y * Columns + x.
    /// Pure logic, no WPF dependencies.
    /// </summary>
    public class LedMarkup
    {
        private readonly List<int> _chain = new List<int>();
        private int[] _position = new int[0]; // cell -> position in chain, -1 if not numbered

        public LedMarkup(int columns, int rows)
        {
            Columns = Math.Max(0, columns);
            Rows = Math.Max(0, rows);
            _position = NewPositions(Columns * Rows);
        }

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int CellCount { get { return Columns * Rows; } }

        /// <summary>Number of LEDs in the chain (the last LED number).</summary>
        public int Count { get { return _chain.Count; } }

        /// <summary>Cells in strip order.</summary>
        public IList<int> Chain { get { return _chain.AsReadOnly(); } }

        public int CellIndex(int x, int y) { return y * Columns + x; }
        public int CellX(int cell) { return Columns == 0 ? 0 : cell % Columns; }
        public int CellY(int cell) { return Columns == 0 ? 0 : cell / Columns; }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Columns && y < Rows;
        }

        public bool IsValidCell(int cell)
        {
            return cell >= 0 && cell < CellCount;
        }

        /// <summary>1-based LED number of the cell, 0 when the cell is not numbered.</summary>
        public int GetNumber(int cell)
        {
            if (!IsValidCell(cell)) return 0;
            return _position[cell] + 1;
        }

        public bool IsNumbered(int cell)
        {
            return GetNumber(cell) > 0;
        }

        /// <summary>Cell of the given 1-based LED number, -1 when out of range.</summary>
        public int GetCell(int number)
        {
            if (number < 1 || number > _chain.Count) return -1;
            return _chain[number - 1];
        }

        #region Editing

        /// <summary>
        /// Inserts cells into the chain so that the first of them gets number <paramref name="atNumber"/>.
        /// LEDs from that number on shift forward. Cells already numbered are skipped,
        /// or taken out of the chain first when <paramref name="overwrite"/> is set.
        /// Returns the number right after the last inserted LED (the natural "next number").
        /// </summary>
        public int Insert(IEnumerable<int> cells, int atNumber, bool overwrite)
        {
            var toInsert = new List<int>();
            var seen = new HashSet<int>();
            foreach (var c in cells)
            {
                if (!IsValidCell(c) || !seen.Add(c)) continue;
                if (IsNumbered(c) && !overwrite) continue;
                toInsert.Add(c);
            }

            var at = Clamp(atNumber, 1, _chain.Count + 1) - 1;
            if (toInsert.Count == 0) return at + 1;

            if (overwrite)
            {
                // Removing cells that sit before the insertion point moves that point back.
                var removedBefore = toInsert.Count(c => IsNumbered(c) && _position[c] < at);
                var remove = new HashSet<int>(toInsert.Where(IsNumbered));
                if (remove.Count > 0) _chain.RemoveAll(remove.Contains);
                at -= removedBefore;
            }

            _chain.InsertRange(at, toInsert);
            Reindex();
            return at + toInsert.Count + 1;
        }

        /// <summary>Takes cells out of the chain; the following LEDs move back.</summary>
        public int Remove(IEnumerable<int> cells)
        {
            var remove = new HashSet<int>(cells.Where(IsNumbered));
            if (remove.Count == 0) return 0;
            _chain.RemoveAll(remove.Contains);
            Reindex();
            return remove.Count;
        }

        /// <summary>Removes LEDs with numbers from..to (inclusive).</summary>
        public int RemoveRange(int from, int to)
        {
            if (!NormalizeRange(ref from, ref to)) return 0;
            _chain.RemoveRange(from - 1, to - from + 1);
            Reindex();
            return to - from + 1;
        }

        /// <summary>Reverses the order of LEDs from..to on the same cells.</summary>
        public bool ReverseRange(int from, int to)
        {
            if (!NormalizeRange(ref from, ref to) || from == to) return false;
            _chain.Reverse(from - 1, to - from + 1);
            Reindex();
            return true;
        }

        /// <summary>
        /// Moves LEDs from..to as one block so that it starts at <paramref name="newStart"/>
        /// (numbering after the move). Returns false when nothing changed.
        /// </summary>
        public bool MoveRange(int from, int to, int newStart)
        {
            if (!NormalizeRange(ref from, ref to)) return false;
            var length = to - from + 1;
            var block = _chain.GetRange(from - 1, length);
            _chain.RemoveRange(from - 1, length);
            var at = Clamp(newStart, 1, _chain.Count + 1) - 1;
            _chain.InsertRange(at, block);
            Reindex();
            return at != from - 1;
        }

        /// <summary>Moves one cell to the given LED number.</summary>
        public bool MoveCellTo(int cell, int number)
        {
            var current = GetNumber(cell);
            if (current == 0)
            {
                Insert(new[] { cell }, number, false);
                return true;
            }
            return MoveRange(current, current, number);
        }

        public void Clear()
        {
            _chain.Clear();
            Reindex();
        }

        public void ReverseAll()
        {
            _chain.Reverse();
            Reindex();
        }

        public void MirrorHorizontal()
        {
            RemapCells((x, y) => CellIndex(Columns - 1 - x, y));
        }

        public void MirrorVertical()
        {
            RemapCells((x, y) => CellIndex(x, Rows - 1 - y));
        }

        /// <summary>Rotates markup 90° clockwise. Only for square grids, because the element shape stays put.</summary>
        public bool RotateClockwise()
        {
            if (Columns != Rows || Columns == 0) return false;
            var n = Columns;
            RemapCells((x, y) => CellIndex(n - 1 - y, x));
            return true;
        }

        /// <summary>
        /// Changes grid size keeping every LED at its (X, Y). LEDs outside the new size leave the chain.
        /// Returns how many LEDs were dropped.
        /// </summary>
        public int Resize(int columns, int rows)
        {
            columns = Math.Max(0, columns);
            rows = Math.Max(0, rows);
            var oldColumns = Columns;
            var newChain = new List<int>();
            var dropped = 0;
            foreach (var cell in _chain)
            {
                var x = oldColumns == 0 ? 0 : cell % oldColumns;
                var y = oldColumns == 0 ? 0 : cell / oldColumns;
                if (x < columns && y < rows) newChain.Add(y * columns + x);
                else dropped++;
            }
            Columns = columns;
            Rows = rows;
            _chain.Clear();
            _chain.AddRange(newChain);
            _position = NewPositions(CellCount);
            Reindex();
            return dropped;
        }

        #endregion

        #region Snapshots

        public LedMarkupSnapshot Save()
        {
            return new LedMarkupSnapshot(Columns, Rows, _chain.ToArray());
        }

        public void Restore(LedMarkupSnapshot snapshot)
        {
            Columns = snapshot.Columns;
            Rows = snapshot.Rows;
            _chain.Clear();
            _chain.AddRange(snapshot.Chain);
            _position = NewPositions(CellCount);
            Reindex();
        }

        public bool SameAs(LedMarkupSnapshot snapshot)
        {
            return snapshot.Columns == Columns && snapshot.Rows == Rows && snapshot.Chain.SequenceEqual(_chain);
        }

        #endregion

        #region Loading from stored numbers

        /// <summary>
        /// Builds the chain from stored (cell, number) pairs. The order is the one the
        /// export already uses: by number, ties by cell index. Gaps and duplicates in the
        /// source are reported in <paramref name="issues"/> and disappear from the chain.
        /// </summary>
        public static LedMarkup FromNumbers(int columns, int rows, IEnumerable<KeyValuePair<int, int>> cellNumbers, out LedMarkupIssues issues)
        {
            var markup = new LedMarkup(columns, rows);
            var valid = cellNumbers
                .Where(p => markup.IsValidCell(p.Key))
                .GroupBy(p => p.Key).Select(g => g.First()) // one entry per cell
                .OrderBy(p => p.Value).ThenBy(p => p.Key)
                .ToList();

            issues = LedMarkupIssues.Analyze(valid);
            markup._chain.AddRange(valid.Select(p => p.Key));
            markup.Reindex();
            return markup;
        }

        #endregion

        #region Shape generators

        /// <summary>Cells on a straight line from (x0, y0) to (x1, y1), both ends included (Bresenham).</summary>
        public static List<int> LineCells(LedMarkup m, int x0, int y0, int x1, int y1)
        {
            var result = new List<int>();
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                if (m.IsInside(x0, y0)) result.Add(m.CellIndex(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
            return result;
        }

        /// <summary>
        /// Cells of the rectangle with corners (x0, y0) and (x1, y1), in strip order.
        /// The strip starts at (x0, y0) and runs toward (x1, y1): along rows, or along
        /// columns when <paramref name="byColumns"/>; every other line reversed when <paramref name="snake"/>.
        /// </summary>
        public static List<int> RectCells(LedMarkup m, int x0, int y0, int x1, int y1, bool byColumns, bool snake)
        {
            var result = new List<int>();
            int sx = x1 >= x0 ? 1 : -1, sy = y1 >= y0 ? 1 : -1;
            int w = Math.Abs(x1 - x0) + 1, h = Math.Abs(y1 - y0) + 1;
            int lines = byColumns ? w : h;
            int along = byColumns ? h : w;
            for (int line = 0; line < lines; line++)
            {
                bool reversed = snake && (line % 2 == 1);
                for (int i = 0; i < along; i++)
                {
                    int k = reversed ? along - 1 - i : i;
                    int x = byColumns ? x0 + line * sx : x0 + k * sx;
                    int y = byColumns ? y0 + k * sy : y0 + line * sy;
                    if (m.IsInside(x, y)) result.Add(m.CellIndex(x, y));
                }
            }
            return result;
        }

        #endregion

        private void RemapCells(Func<int, int, int> map)
        {
            for (int i = 0; i < _chain.Count; i++)
            {
                var c = _chain[i];
                _chain[i] = map(CellX(c), CellY(c));
            }
            Reindex();
        }

        private bool NormalizeRange(ref int from, ref int to)
        {
            if (_chain.Count == 0) return false;
            if (from > to) { var t = from; from = to; to = t; }
            if (to < 1 || from > _chain.Count) return false;
            from = Clamp(from, 1, _chain.Count);
            to = Clamp(to, 1, _chain.Count);
            return true;
        }

        private void Reindex()
        {
            for (int i = 0; i < _position.Length; i++) _position[i] = -1;
            for (int i = 0; i < _chain.Count; i++) _position[_chain[i]] = i;
        }

        private static int[] NewPositions(int size)
        {
            var p = new int[size];
            for (int i = 0; i < size; i++) p[i] = -1;
            return p;
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }

    public class LedMarkupSnapshot
    {
        public LedMarkupSnapshot(int columns, int rows, int[] chain)
        {
            Columns = columns;
            Rows = rows;
            Chain = chain;
        }

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int[] Chain { get; private set; }
    }

    /// <summary>Problems found in stored LED numbers: gaps, duplicates, zero numbers.</summary>
    public class LedMarkupIssues
    {
        public LedMarkupIssues()
        {
            MissingNumbers = new List<int>();
            DuplicateNumbers = new List<int>();
            DuplicateCells = new HashSet<int>();
            CellsAfterGap = new HashSet<int>();
        }

        /// <summary>Numbers in 1..max that no cell has.</summary>
        public List<int> MissingNumbers { get; private set; }
        /// <summary>Numbers used by more than one cell.</summary>
        public List<int> DuplicateNumbers { get; private set; }
        public HashSet<int> DuplicateCells { get; private set; }
        /// <summary>Cells whose number follows a missing one.</summary>
        public HashSet<int> CellsAfterGap { get; private set; }
        public int MaxNumber { get; private set; }

        public bool IsClean { get { return MissingNumbers.Count == 0 && DuplicateNumbers.Count == 0; } }

        /// <summary>Analyzes (cell, number) pairs. Numbers below 1 count as a gap at 0.</summary>
        public static LedMarkupIssues Analyze(IEnumerable<KeyValuePair<int, int>> cellNumbers)
        {
            var issues = new LedMarkupIssues();
            var byNumber = cellNumbers.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.Select(p => p.Key).ToList());
            if (byNumber.Count == 0) return issues;

            issues.MaxNumber = byNumber.Keys.Max();
            foreach (var kv in byNumber.OrderBy(k => k.Key))
            {
                if (kv.Value.Count > 1)
                {
                    issues.DuplicateNumbers.Add(kv.Key);
                    foreach (var c in kv.Value) issues.DuplicateCells.Add(c);
                }
                if (kv.Key < 1)
                {
                    // Numbered cell without a valid number: treat as a duplicate-like problem.
                    if (!issues.DuplicateNumbers.Contains(kv.Key)) issues.DuplicateNumbers.Add(kv.Key);
                    foreach (var c in kv.Value) issues.DuplicateCells.Add(c);
                }
                else if (kv.Key > 1 && !byNumber.ContainsKey(kv.Key - 1))
                {
                    foreach (var c in kv.Value) issues.CellsAfterGap.Add(c);
                }
            }
            for (int n = 1; n <= issues.MaxNumber; n++)
                if (!byNumber.ContainsKey(n)) issues.MissingNumbers.Add(n);
            return issues;
        }

        /// <summary>Compact text of number ranges: 3, 7–9, 15.</summary>
        public static string FormatRanges(IList<int> numbers, int maxItems = 8)
        {
            var parts = new List<string>();
            int i = 0;
            while (i < numbers.Count && parts.Count < maxItems)
            {
                int start = numbers[i], end = start;
                while (i + 1 < numbers.Count && numbers[i + 1] == end + 1) { i++; end = numbers[i]; }
                parts.Add(start == end ? start.ToString() : start + "–" + end);
                i++;
            }
            var text = string.Join(", ", parts);
            if (i < numbers.Count) text += ", …";
            return text;
        }
    }
}
