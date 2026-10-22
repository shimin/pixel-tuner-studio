namespace PixelTuner.Core.Layout;

/// <summary>
/// What is wrong with the LED numbers a PixelTuner 2 project stored (its tiles carried a number each, and nothing kept them from leaving gaps
/// or using one twice). The device always got the LEDs in number order, so the chain Pixel Tuner builds from them is fine; the editor tells the
/// user what was there and compacts the numbers to 1..N on OK. Kept with the layout until then.
/// </summary>
public sealed class LedNumberIssues
{
    /// <summary>Numbers in 1..<see cref="MaxNumber"/> that no cell has.</summary>
    public List<int> MissingNumbers { get; set; } = [];

    /// <summary>Numbers used by more than one cell (and numbers below 1).</summary>
    public List<int> DuplicateNumbers { get; set; } = [];

    /// <summary>The cells that share a number or have a number below 1.</summary>
    public List<int> DuplicateCells { get; set; } = [];

    /// <summary>The cells whose number follows a missing one.</summary>
    public List<int> CellsAfterGap { get; set; } = [];

    public int MaxNumber { get; set; }

    public bool IsClean => MissingNumbers.Count == 0 && DuplicateNumbers.Count == 0;

    /// <summary>Analyzes (cell, number) pairs, as PixelTuner 2's <c>LedMarkupIssues.Analyze</c>. A number below 1 counts as a gap at 0.</summary>
    public static LedNumberIssues Analyze(IEnumerable<(int Cell, int Number)> cellNumbers)
    {
        var issues = new LedNumberIssues();
        var byNumber = cellNumbers.GroupBy(p => p.Number).ToDictionary(g => g.Key, g => g.Select(p => p.Cell).ToList());
        if (byNumber.Count == 0) return issues;

        var duplicateCells = new SortedSet<int>();
        var afterGap = new SortedSet<int>();
        issues.MaxNumber = byNumber.Keys.Max();
        foreach (var (number, cells) in byNumber.OrderBy(k => k.Key))
        {
            if (cells.Count > 1)
            {
                issues.DuplicateNumbers.Add(number);
                duplicateCells.UnionWith(cells);
            }
            if (number < 1)
            {
                // A numbered cell without a valid number is treated as a repeat.
                if (!issues.DuplicateNumbers.Contains(number)) issues.DuplicateNumbers.Add(number);
                duplicateCells.UnionWith(cells);
            }
            else if (number > 1 && !byNumber.ContainsKey(number - 1))
            {
                afterGap.UnionWith(cells);
            }
        }
        for (int n = 1; n <= issues.MaxNumber; n++)
            if (!byNumber.ContainsKey(n)) issues.MissingNumbers.Add(n);
        issues.DuplicateCells = [.. duplicateCells];
        issues.CellsAfterGap = [.. afterGap];
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
            while (i + 1 < numbers.Count && numbers[i + 1] == end + 1)
            {
                i++;
                end = numbers[i];
            }
            parts.Add(start == end ? start.ToString() : start + "–" + end);
            i++;
        }
        string text = string.Join(", ", parts);
        return i < numbers.Count ? text + ", …" : text;
    }
}
