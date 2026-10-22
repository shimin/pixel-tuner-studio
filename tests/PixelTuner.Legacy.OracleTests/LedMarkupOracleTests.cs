using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Pt2 = PixelTuner.ViewModel;
using Port = PixelTuner.Core.Layout;

namespace PixelTuner.Legacy.OracleTests
{
    /// <summary>
    /// PixelTuner 2's own LED markup (compiled from legacy/) and the port are given the same random edits; after every
    /// edit the chain and the grid must be the same.
    /// </summary>
    public class LedMarkupOracleTests
    {
        private static void Same(Pt2.LedMarkup legacy, Port.LedMarkup port, string after)
        {
            Assert.True(legacy.Columns == port.Columns && legacy.Rows == port.Rows, "grid after " + after);
            Assert.True(legacy.Chain.SequenceEqual(port.Chain), "chain after " + after + ": legacy " + string.Join(",", legacy.Chain) + " port " + string.Join(",", port.Chain));
        }

        private static List<int> RandomCells(Random random, int cellCount, int max)
        {
            var cells = new List<int>();
            for (int i = random.Next(0, max); i > 0; i--) cells.Add(random.Next(cellCount));
            return cells;
        }

        [Fact]
        public void Random_edits_give_the_same_chain_as_PixelTuner_2()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var random = new Random(seed);
                int columns = random.Next(1, 9), rows = random.Next(1, 9);
                var legacy = new Pt2.LedMarkup(columns, rows);
                var port = new Port.LedMarkup(columns, rows);

                for (int step = 0; step < 40; step++)
                {
                    int count = legacy.Count, cells = legacy.CellCount;
                    string what;
                    switch (random.Next(11))
                    {
                        case 0:
                        case 1:
                        {
                            var list = RandomCells(random, cells, 12);
                            int at = random.Next(0, count + 4);
                            bool overwrite = random.Next(2) == 0;
                            what = "Insert " + string.Join(",", list) + " at " + at + (overwrite ? " overwrite" : "");
                            legacy.Insert(list, at, overwrite);
                            port = port.Insert(list, at, overwrite);
                            break;
                        }
                        case 2:
                        {
                            var list = RandomCells(random, cells, 5);
                            what = "Remove " + string.Join(",", list);
                            legacy.Remove(list);
                            port = port.Remove(list);
                            break;
                        }
                        case 3:
                            if (count == 0) continue;
                            {
                                int from = random.Next(1, count + 1), to = random.Next(from, count + 1);
                                what = "RemoveRange " + from + ".." + to;
                                legacy.RemoveRange(from, to);
                                port = port.DeleteRange(from, to);
                            }
                            break;
                        case 4:
                            if (count == 0) continue;
                            {
                                int from = random.Next(1, count + 1), to = random.Next(from, count + 1);
                                what = "ReverseRange " + from + ".." + to;
                                legacy.ReverseRange(from, to);
                                port = port.Reverse(from, to);
                            }
                            break;
                        case 5:
                            if (count == 0) continue;
                            {
                                int from = random.Next(1, count + 1), to = random.Next(from, count + 1);
                                int newStart = random.Next(1, count - (to - from + 1) + 2);
                                what = "MoveRange " + from + ".." + to + " to " + newStart;
                                legacy.MoveRange(from, to, newStart);
                                port = port.MoveRange(from, to, newStart);
                            }
                            break;
                        case 6:
                        {
                            int cell = random.Next(cells);
                            int number = legacy.IsNumbered(cell) ? random.Next(1, count + 1) : random.Next(0, count + 4);
                            what = "MoveCellTo " + cell + " -> " + number;
                            legacy.MoveCellTo(cell, number);
                            port = port.Place(cell, number);
                            break;
                        }
                        case 7:
                            switch (random.Next(4))
                            {
                                case 0: what = "ReverseAll"; legacy.ReverseAll(); port = port.ReverseAll(); break;
                                case 1: what = "MirrorHorizontal"; legacy.MirrorHorizontal(); port = port.MirrorHorizontally(); break;
                                case 2: what = "MirrorVertical"; legacy.MirrorVertical(); port = port.MirrorVertically(); break;
                                default:
                                    what = "Rotate";
                                    // PixelTuner 2 turns square grids only (the outline of the element stays put); the port also turns the others.
                                    if (legacy.Columns != legacy.Rows) continue;
                                    legacy.RotateClockwise();
                                    port = port.RotateClockwise();
                                    break;
                            }
                            break;
                        case 8:
                        {
                            int newColumns = random.Next(1, 10), newRows = random.Next(1, 10);
                            what = "Resize " + newColumns + "x" + newRows;
                            int dropped = legacy.Resize(newColumns, newRows);
                            var (resized, portDropped) = port.Resize(newColumns, newRows);
                            port = resized;
                            Assert.Equal(dropped, portDropped);
                            break;
                        }
                        case 9:
                            what = "Clear";
                            if (random.Next(4) != 0) continue;
                            legacy.Clear();
                            port = port.Clear();
                            break;
                        default:
                        {
                            // Where the chain was, the LEDs can be read back one by one.
                            what = "Read";
                            for (int number = 1; number <= count; number++) Assert.Equal(legacy.GetCell(number), port.CellOf(number));
                            for (int cell = 0; cell < cells; cell++) Assert.Equal(legacy.GetNumber(cell), port.NumberOf(cell));
                            break;
                        }
                    }
                    Same(legacy, port, "seed " + seed + " step " + step + ": " + what);
                }
            }
        }

        [Fact]
        public void Lines_and_rectangles_pick_the_same_cells_as_PixelTuner_2()
        {
            var random = new Random(7);
            for (int round = 0; round < 500; round++)
            {
                int columns = random.Next(1, 12), rows = random.Next(1, 12);
                var legacy = new Pt2.LedMarkup(columns, rows);
                var port = new Port.LedMarkup(columns, rows);
                // Corners may lie outside the grid: both clip.
                int x0 = random.Next(-2, columns + 2), y0 = random.Next(-2, rows + 2), x1 = random.Next(-2, columns + 2), y1 = random.Next(-2, rows + 2);

                Assert.Equal(Pt2.LedMarkup.LineCells(legacy, x0, y0, x1, y1), port.LineCells(x0, y0, x1, y1));

                bool byColumns = random.Next(2) == 0, snake = random.Next(2) == 0;
                var direction = byColumns ? Port.FillDirection.Columns : Port.FillDirection.Rows;
                var pattern = snake ? Port.FillPattern.Snake : Port.FillPattern.Straight;
                Assert.Equal(Pt2.LedMarkup.RectCells(legacy, x0, y0, x1, y1, byColumns, snake), port.RectCells(x0, y0, x1, y1, direction, pattern));
            }
        }

        [Fact]
        public void The_whole_matrix_sweeps_from_each_corner_as_the_fill_all_button_of_PixelTuner_2()
        {
            var random = new Random(11);
            for (int round = 0; round < 100; round++)
            {
                int columns = random.Next(1, 10), rows = random.Next(1, 10);
                var legacy = new Pt2.LedMarkup(columns, rows);
                var port = new Port.LedMarkup(columns, rows);
                foreach (var corner in Enum.GetValues(typeof(Port.Corner)).Cast<Port.Corner>())
                    foreach (bool byColumns in new[] { false, true })
                        foreach (bool snake in new[] { false, true })
                        {
                            // The code of LedMarkupEditor.FillAll.
                            int startCorner = (int)corner;
                            int maxX = columns - 1, maxY = rows - 1;
                            int x0 = (startCorner == 1 || startCorner == 3) ? maxX : 0;
                            int y0 = (startCorner == 2 || startCorner == 3) ? maxY : 0;
                            var expected = Pt2.LedMarkup.RectCells(legacy, x0, y0, maxX - x0, maxY - y0, byColumns, snake);
                            var actual = port.SweepCells(corner, byColumns ? Port.FillDirection.Columns : Port.FillDirection.Rows, snake ? Port.FillPattern.Snake : Port.FillPattern.Straight);
                            Assert.Equal(expected, actual);
                        }
            }
        }

        [Fact]
        public void Stored_numbers_make_the_same_chain_and_the_same_report_as_PixelTuner_2()
        {
            for (int seed = 0; seed < 400; seed++)
            {
                var random = new Random(seed);
                int columns = random.Next(1, 7), rows = random.Next(1, 7);
                var pairs = new List<(int Cell, int Number)>();
                for (int i = random.Next(0, 20); i > 0; i--)
                    pairs.Add((random.Next(-1, columns * rows + 2), random.Next(-1, 14)));

                var legacy = Pt2.LedMarkup.FromNumbers(columns, rows, pairs.Select(p => new KeyValuePair<int, int>(p.Cell, p.Number)), out var legacyIssues);
                var port = Port.LedMarkup.FromNumbers(columns, rows, pairs, out var portIssues);

                Assert.True(legacy.Chain.SequenceEqual(port.Chain), "chain, seed " + seed);
                Assert.True(legacyIssues.MissingNumbers.SequenceEqual(portIssues.MissingNumbers), "missing, seed " + seed);
                Assert.True(legacyIssues.DuplicateNumbers.SequenceEqual(portIssues.DuplicateNumbers), "repeats, seed " + seed);
                Assert.True(legacyIssues.DuplicateCells.OrderBy(c => c).SequenceEqual(portIssues.DuplicateCells), "repeated cells, seed " + seed);
                Assert.True(legacyIssues.CellsAfterGap.OrderBy(c => c).SequenceEqual(portIssues.CellsAfterGap), "cells after a gap, seed " + seed);
                Assert.Equal(legacyIssues.MaxNumber, portIssues.MaxNumber);
                Assert.Equal(legacyIssues.IsClean, portIssues.IsClean);

                var numbers = pairs.Select(p => p.Number).Distinct().OrderBy(n => n).ToList();
                Assert.Equal(Pt2.LedMarkupIssues.FormatRanges(numbers), Port.LedNumberIssues.FormatRanges(numbers));
                Assert.Equal(Pt2.LedMarkupIssues.FormatRanges(numbers, 2), Port.LedNumberIssues.FormatRanges(numbers, 2));
            }
        }
    }
}
