namespace PixelTuner.Core.Layout;

/// <summary>Where a gesture starts filling a rectangle.</summary>
public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>Which way a fill runs first: along the rows or down the columns.</summary>
public enum FillDirection { Rows, Columns }

/// <summary>How a fill turns at the end of a row: back the other way (a snake, as LED strips are wired) or to the same side again.</summary>
public enum FillPattern { Snake, Straight }

/// <summary>What is wrong with a chain that came from a file, as the editor tells the user.</summary>
/// <param name="Duplicates">Cells that are in the chain more than once.</param>
/// <param name="OutOfGrid">Positions (1-based) of entries that lie outside the grid.</param>
public sealed record LedDiagnosis(IReadOnlyList<int> Duplicates, IReadOnlyList<int> OutOfGrid)
{
    public bool IsClean => Duplicates.Count == 0 && OutOfGrid.Count == 0;
}

/// <summary>
/// The LED markup of a prop as an ordered chain of cells on a grid: the first cell is LED 1 on the strip, the next LED 2, and so on.
/// A chain cannot have gaps or repeats, because the number of a LED is its place in the chain; every operation inserts or removes in the
/// chain and shifts the tail. Immutable: an operation gives a new markup, so undo is a list of them. No drawing in here.
/// </summary>
public sealed class LedMarkup
{
    private readonly int[] _chain;
    private readonly int[] _numberOf;   // per cell: 1-based place in the chain, 0 when not in it

    public LedMarkup(int columns, int rows, IEnumerable<int>? chain = null)
    {
        if (columns < 1 || rows < 1) throw new ArgumentOutOfRangeException(nameof(columns), "A grid has at least one cell.");
        Columns = columns;
        Rows = rows;
        _numberOf = new int[checked(columns * rows)];
        var list = new List<int>();
        foreach (int cell in chain ?? [])
        {
            if ((uint)cell >= (uint)_numberOf.Length) throw new ArgumentOutOfRangeException(nameof(chain), $"Cell {cell} is outside the grid.");
            if (_numberOf[cell] != 0) throw new ArgumentException($"Cell {cell} is in the chain twice.", nameof(chain));
            list.Add(cell);
            _numberOf[cell] = list.Count;
        }
        _chain = [.. list];
    }

    public int Columns { get; }
    public int Rows { get; }

    /// <summary>The cells in LED order.</summary>
    public IReadOnlyList<int> Chain => _chain;

    /// <summary>Number of LEDs.</summary>
    public int Count => _chain.Length;

    public int Cell(int x, int y) => y * Columns + x;
    public (int X, int Y) Position(int cell) => (cell % Columns, cell / Columns);
    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows;

    /// <summary>1-based number of the LED at a cell; 0 when the cell has none.</summary>
    public int NumberOf(int cell) => _numberOf[cell];

    public bool IsMarked(int cell) => _numberOf[cell] != 0;

    /// <summary>The cell of LED <paramref name="number"/> (1-based).</summary>
    public int CellOf(int number) => _chain[number - 1];

    private LedMarkup With(IEnumerable<int> chain) => new(Columns, Rows, chain);

    // ---- reading what a file holds

    /// <summary>What is wrong with a stored chain (cells repeated, cells outside the grid), without changing it.</summary>
    public static LedDiagnosis Diagnose(int columns, int rows, IReadOnlyList<int> leds)
    {
        var seen = new HashSet<int>();
        var duplicates = new List<int>();
        var outside = new List<int>();
        for (int i = 0; i < leds.Count; i++)
        {
            int cell = leds[i];
            if (cell < 0 || cell >= columns * rows) outside.Add(i + 1);
            else if (!seen.Add(cell)) duplicates.Add(i + 1);
        }
        return new LedDiagnosis(duplicates, outside);
    }

    /// <summary>
    /// The markup a stored chain stands for. A repeated cell keeps its first place, and cells outside the grid are dropped:
    /// the editor says so (<see cref="Diagnose"/>) and the user decides to keep that.
    /// </summary>
    public static LedMarkup Load(int columns, int rows, IReadOnlyList<int> leds)
    {
        var seen = new HashSet<int>();
        return new LedMarkup(columns, rows, leds.Where(c => c >= 0 && c < columns * rows && seen.Add(c)));
    }

    // ---- placing and removing

    /// <summary>
    /// Puts cells into the chain from <paramref name="number"/> on, in the order given, and shifts the tail after them.
    /// Cells that are marked already are left where they are, or, with <paramref name="overwrite"/>, taken out first and put in again
    /// (as PixelTuner 2 did): <paramref name="number"/> counts the chain as it was, so taking out cells in front of the place moves the
    /// new cells back by as many.
    /// </summary>
    /// <param name="number">1-based place of the first new LED; more than <c>Count + 1</c> is the end of the chain.</param>
    public LedMarkup Insert(IEnumerable<int> cells, int number, bool overwrite = false)
    {
        var wanted = new List<int>();
        var seen = new HashSet<int>();
        foreach (int cell in cells)
        {
            if ((uint)cell >= (uint)_numberOf.Length) throw new ArgumentOutOfRangeException(nameof(cells), $"Cell {cell} is outside the grid.");
            if (seen.Add(cell) && (overwrite || _numberOf[cell] == 0)) wanted.Add(cell);
        }
        if (wanted.Count == 0) return this;

        var chain = new List<int>(_chain);
        int at = Math.Clamp(number, 1, chain.Count + 1) - 1;
        if (overwrite)
        {
            foreach (int cell in wanted)
            {
                int index = chain.IndexOf(cell);
                if (index < 0) continue;
                chain.RemoveAt(index);
                if (index < at) at--;
            }
        }
        chain.InsertRange(at, wanted);
        return With(chain);
    }

    /// <summary>
    /// Gives a cell a number: a cell that is not marked is inserted there, one that is marked moves there, and the LEDs in between
    /// shift by one (the manual number of a double click). Afterwards the cell has exactly that number.
    /// </summary>
    public LedMarkup Place(int cell, int number)
    {
        if (_numberOf[cell] == 0) return Insert([cell], number);
        int current = _numberOf[cell];
        int target = Math.Clamp(number, 1, Count);
        return current == target ? this : MoveRange(current, current, target);
    }

    public LedMarkup Append(int cell) => Insert([cell], Count + 1);

    /// <summary>Takes cells out of the chain; the tail shifts to close the gap.</summary>
    public LedMarkup Remove(IEnumerable<int> cells)
    {
        var gone = cells.Where(c => (uint)c < (uint)_numberOf.Length && _numberOf[c] != 0).ToHashSet();
        return gone.Count == 0 ? this : With(_chain.Where(c => !gone.Contains(c)));
    }

    public LedMarkup Remove(int cell) => Remove([cell]);

    // ---- shapes

    /// <summary>
    /// The cells on a straight line from one cell to another, both included, in that order (Bresenham, as PixelTuner 2 drew it);
    /// cells of the line that are off the grid are left out.
    /// </summary>
    public IReadOnlyList<int> LineCells(int x0, int y0, int x1, int y1)
    {
        var cells = new List<int>();
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            if (Contains(x0, y0)) cells.Add(Cell(x0, y0));
            if (x0 == x1 && y0 == y1) break;
            int doubled = 2 * error;
            if (doubled >= dy) { error += dy; x0 += sx; }
            if (doubled <= dx) { error += dx; y0 += sy; }
        }
        return cells;
    }

    public IReadOnlyList<int> LineCells(int from, int to)
    {
        var (x0, y0) = Position(from);
        var (x1, y1) = Position(to);
        return LineCells(x0, y0, x1, y1);
    }

    /// <summary>
    /// The cells of the rectangle between two corners the way a drag numbers them: the strip starts at the first corner and runs
    /// toward the second, along the rows or down the columns, and with a snake every other line runs back. Cells off the grid are left out.
    /// </summary>
    public IReadOnlyList<int> RectCells(int x0, int y0, int x1, int y1, FillDirection direction, FillPattern pattern)
    {
        int sx = x1 >= x0 ? 1 : -1, sy = y1 >= y0 ? 1 : -1;
        int width = Math.Abs(x1 - x0) + 1, height = Math.Abs(y1 - y0) + 1;
        bool byColumns = direction == FillDirection.Columns;
        int lines = byColumns ? width : height, along = byColumns ? height : width;
        var cells = new List<int>(width * height);
        for (int line = 0; line < lines; line++)
        {
            bool back = pattern == FillPattern.Snake && line % 2 == 1;
            for (int i = 0; i < along; i++)
            {
                int k = back ? along - 1 - i : i;
                int x = byColumns ? x0 + line * sx : x0 + k * sx;
                int y = byColumns ? y0 + k * sy : y0 + line * sy;
                if (Contains(x, y)) cells.Add(Cell(x, y));
            }
        }
        return cells;
    }

    /// <summary>All the cells of the grid in one sweep: it starts at a corner, runs along the rows or down the columns, and turns as a snake or not.</summary>
    public IReadOnlyList<int> SweepCells(Corner start, FillDirection direction, FillPattern pattern)
    {
        int maxX = Columns - 1, maxY = Rows - 1;
        int x0 = start is Corner.TopRight or Corner.BottomRight ? maxX : 0;
        int y0 = start is Corner.BottomLeft or Corner.BottomRight ? maxY : 0;
        return RectCells(x0, y0, maxX - x0, maxY - y0, direction, pattern);
    }

    // ---- ranges of numbers

    private void CheckRange(int from, int to)
    {
        if (from < 1 || to > Count || from > to) throw new ArgumentOutOfRangeException(nameof(from), $"Numbers {from} to {to} are not in 1 to {Count}.");
    }

    /// <summary>LEDs <paramref name="from"/> to <paramref name="to"/> run the other way on the same cells (41..80 becomes 80..41).</summary>
    public LedMarkup Reverse(int from, int to)
    {
        CheckRange(from, to);
        var chain = new List<int>(_chain);
        chain.Reverse(from - 1, to - from + 1);
        return With(chain);
    }

    /// <summary>A piece of the chain moves whole so that it starts at <paramref name="newStart"/> (its number once it has moved).</summary>
    public LedMarkup MoveRange(int from, int to, int newStart)
    {
        CheckRange(from, to);
        int length = to - from + 1;
        if (newStart < 1 || newStart > Count - length + 1) throw new ArgumentOutOfRangeException(nameof(newStart));
        var chain = new List<int>(_chain);
        var piece = chain.GetRange(from - 1, length);
        chain.RemoveRange(from - 1, length);
        chain.InsertRange(newStart - 1, piece);
        return With(chain);
    }

    public LedMarkup DeleteRange(int from, int to)
    {
        CheckRange(from, to);
        return With(_chain.Where((_, i) => i + 1 < from || i + 1 > to));
    }

    /// <summary>Erases everything after LED <paramref name="number"/>, to mark the end of the strip again.</summary>
    public LedMarkup ClearTail(int number)
    {
        if (number < 0 || number > Count) throw new ArgumentOutOfRangeException(nameof(number));
        return With(_chain.Take(number));
    }

    // ---- the whole markup

    public LedMarkup Clear() => Count == 0 ? this : new LedMarkup(Columns, Rows);

    /// <summary>The strip starts at its other end: LED 1 becomes the last.</summary>
    public LedMarkup ReverseAll() => Count < 2 ? this : With(_chain.Reverse());

    public LedMarkup MirrorHorizontally() => With(_chain.Select(c => { var (x, y) = Position(c); return Cell(Columns - 1 - x, y); }));

    public LedMarkup MirrorVertically() => With(_chain.Select(c => { var (x, y) = Position(c); return Cell(x, Rows - 1 - y); }));

    /// <summary>Turned a quarter turn clockwise; the grid gets as many columns as it had rows.</summary>
    public LedMarkup RotateClockwise()
    {
        // (x, y) of the old grid goes to (Rows - 1 - y, x) of the new one.
        return new LedMarkup(Rows, Columns, _chain.Select(c => { var (x, y) = Position(c); return (x * Rows) + (Rows - 1 - y); }));
    }

    /// <summary>
    /// The same markup on a grid of another size: every LED stays at its (x, y), those that fall outside are dropped from the chain
    /// and the tail closes the gap (the caller says how many went).
    /// </summary>
    public (LedMarkup Markup, int Dropped) Resize(int columns, int rows)
    {
        if (columns < 1 || rows < 1) throw new ArgumentOutOfRangeException(nameof(columns));
        var kept = new List<int>();
        foreach (int c in _chain)
        {
            var (x, y) = Position(c);
            if (x < columns && y < rows) kept.Add(y * columns + x);
        }
        return (new LedMarkup(columns, rows, kept), _chain.Length - kept.Count);
    }

    // ---- what the editor shows

    /// <summary>Neighbours in the chain that are not neighbours in the grid (a jump of more than one cell): not wrong, but worth a look.</summary>
    public IReadOnlyList<int> Jumps()
    {
        var jumps = new List<int>();
        for (int i = 1; i < _chain.Length; i++)
        {
            var (x0, y0) = Position(_chain[i - 1]);
            var (x1, y1) = Position(_chain[i]);
            if (Math.Abs(x1 - x0) > 1 || Math.Abs(y1 - y0) > 1) jumps.Add(i + 1);
        }
        return jumps;
    }

    public override bool Equals(object? obj) =>
        obj is LedMarkup other && other.Columns == Columns && other.Rows == Rows && other._chain.AsSpan().SequenceEqual(_chain);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Columns);
        hash.Add(Rows);
        foreach (int c in _chain) hash.Add(c);
        return hash.ToHashCode();
    }
}
