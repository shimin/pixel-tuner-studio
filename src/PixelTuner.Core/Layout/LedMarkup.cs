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
    /// Cells that are marked already are left where they are, or, with <paramref name="overwrite"/>, taken out first and put in again,
    /// so that the first of them has exactly <paramref name="number"/> afterwards.
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
        if (overwrite)
        {
            foreach (int cell in wanted) chain.Remove(cell);
        }
        // The number is the place the first cell ends up at, so it counts the chain without the cells that were taken out.
        int at = Math.Clamp(number, 1, chain.Count + 1) - 1;
        chain.InsertRange(at, wanted);
        return With(chain);
    }

    /// <summary>Puts one cell at a number; a cell that is marked already moves there (the manual number of a double click).</summary>
    public LedMarkup Place(int cell, int number) => Insert([cell], number, overwrite: true);

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
    /// The cells from one cell to another along a row, a column or a diagonal, both included, in that order; empty when the
    /// two are not on one such line (a Shift click away from the last LED does nothing).
    /// </summary>
    public IReadOnlyList<int> LineCells(int from, int to)
    {
        var (x0, y0) = Position(from);
        var (x1, y1) = Position(to);
        int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
        if (x1 != x0 && y1 != y0 && Math.Abs(x1 - x0) != Math.Abs(y1 - y0)) return [];
        int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        var cells = new List<int>(steps + 1);
        for (int i = 0; i <= steps; i++) cells.Add(Cell(x0 + dx * i, y0 + dy * i));
        return cells;
    }

    /// <summary>The cells of a rectangle in the order a fill numbers them.</summary>
    /// <param name="x">Left column of the rectangle.</param>
    /// <param name="start">The corner the fill starts at.</param>
    public IReadOnlyList<int> FillCells(int x, int y, int width, int height, Corner start, FillDirection direction, FillPattern pattern)
    {
        if (width < 1 || height < 1 || x < 0 || y < 0 || x + width > Columns || y + height > Rows)
            throw new ArgumentOutOfRangeException(nameof(width), "The rectangle is not inside the grid.");
        bool fromRight = start is Corner.TopRight or Corner.BottomRight;
        bool fromBottom = start is Corner.BottomLeft or Corner.BottomRight;

        // "Lines" run along the direction; "steps" go from one line to the next.
        int lines = direction == FillDirection.Rows ? height : width;
        int lineLength = direction == FillDirection.Rows ? width : height;
        bool lineReversed = direction == FillDirection.Rows ? fromRight : fromBottom;   // the first line runs from the high end
        bool stepsReversed = direction == FillDirection.Rows ? fromBottom : fromRight;

        var cells = new List<int>(width * height);
        for (int line = 0; line < lines; line++)
        {
            bool back = lineReversed ^ (pattern == FillPattern.Snake && line % 2 == 1);
            int across = stepsReversed ? lines - 1 - line : line;
            for (int i = 0; i < lineLength; i++)
            {
                int along = back ? lineLength - 1 - i : i;
                cells.Add(direction == FillDirection.Rows ? Cell(x + along, y + across) : Cell(x + across, y + along));
            }
        }
        return cells;
    }

    /// <summary>All 16 sweeps of the whole grid: four corners, rows or columns, snake or straight.</summary>
    public IReadOnlyList<int> SweepCells(Corner start, FillDirection direction, FillPattern pattern) =>
        FillCells(0, 0, Columns, Rows, start, direction, pattern);

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
