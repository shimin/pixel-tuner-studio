using PixelTuner.Core.Layout;
using Xunit;

namespace PixelTuner.Core.Tests.Layout;

public class LedMarkupTests
{
    private static LedMarkup Grid(int columns, int rows, params int[] chain) => new(columns, rows, chain);

    private static int[] Chain(LedMarkup m) => [.. m.Chain];

    // ---- construction and reading

    [Fact]
    public void ChainNumbersCellsFromOne()
    {
        var m = Grid(4, 3, 5, 0, 11);
        Assert.Equal(3, m.Count);
        Assert.Equal(1, m.NumberOf(5));
        Assert.Equal(2, m.NumberOf(0));
        Assert.Equal(3, m.NumberOf(11));
        Assert.Equal(0, m.NumberOf(1));
        Assert.Equal(11, m.CellOf(3));
        Assert.True(m.IsMarked(0));
        Assert.False(m.IsMarked(2));
    }

    [Fact]
    public void ConstructorRefusesRepeatsAndCellsOutsideTheGrid()
    {
        Assert.Throws<ArgumentException>(() => Grid(4, 3, 1, 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Grid(4, 3, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Grid(4, 3, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LedMarkup(0, 3));
    }

    [Fact]
    public void CellAndPositionAreInverse()
    {
        var m = new LedMarkup(7, 5);
        for (int cell = 0; cell < 35; cell++)
        {
            var (x, y) = m.Position(cell);
            Assert.Equal(cell, m.Cell(x, y));
        }
        Assert.True(m.Contains(6, 4));
        Assert.False(m.Contains(7, 0));
        Assert.False(m.Contains(0, -1));
    }

    [Fact]
    public void DiagnoseFindsRepeatsAndCellsOutsideWithoutChangingAnything()
    {
        var d = LedMarkup.Diagnose(4, 3, [0, 1, 1, 12, 2, 0, -3]);
        Assert.Equal([3, 6], d.Duplicates);
        Assert.Equal([4, 7], d.OutOfGrid);
        Assert.False(d.IsClean);
        Assert.True(LedMarkup.Diagnose(4, 3, [3, 2, 1]).IsClean);
    }

    [Fact]
    public void LoadKeepsTheFirstPlaceOfARepeatedCellAndDropsCellsOutside()
    {
        var m = LedMarkup.Load(4, 3, [0, 1, 1, 12, 2, 0, -3]);
        Assert.Equal([0, 1, 2], Chain(m));
    }

    // ---- placing and removing

    [Fact]
    public void InsertInTheMiddleShiftsTheTail()
    {
        var m = Grid(4, 3, 0, 1, 2, 3).Insert([8, 9], 2);
        Assert.Equal([0, 8, 9, 1, 2, 3], Chain(m));
        Assert.Equal(4, m.NumberOf(1));
    }

    [Fact]
    public void InsertPastTheEndAppendsAndBelowOneGoesToTheFront()
    {
        var m = Grid(4, 3, 0, 1);
        Assert.Equal([0, 1, 5], Chain(m.Insert([5], 99)));
        Assert.Equal([5, 0, 1], Chain(m.Insert([5], 0)));
        Assert.Equal([0, 1, 7], Chain(m.Append(7)));
    }

    [Fact]
    public void InsertLeavesMarkedCellsAloneAndNeverErasesWithoutOverwrite()
    {
        var m = Grid(4, 3, 0, 1, 2);
        var after = m.Insert([1, 6, 0], 1);
        Assert.Equal([6, 0, 1, 2], Chain(after));
        Assert.Same(m, m.Insert([0, 1], 1));
    }

    [Fact]
    public void InsertIgnoresACellGivenTwice()
    {
        Assert.Equal([0, 5, 6], Chain(Grid(4, 3, 0).Insert([5, 6, 5], 2)));
    }

    [Fact]
    public void InsertWithOverwriteMovesMarkedCellsToTheNewPlace()
    {
        var m = Grid(4, 3, 0, 1, 2, 3, 4);
        // Cell 3 (LED 4) goes to place 2; the cells behind it keep their order.
        Assert.Equal([0, 3, 1, 2, 4], Chain(m.Insert([3], 2, overwrite: true)));
        // Cell 0 (LED 1) goes in front of what was LED 4, as PixelTuner 2 counted: the place is in the chain as it was.
        Assert.Equal([1, 2, 0, 3, 4], Chain(m.Insert([0], 4, overwrite: true)));
    }

    [Fact]
    public void PlaceTransfersALedWithoutMakingADuplicate()
    {
        var m = Grid(4, 3, 0, 1, 2, 3);
        var moved = m.Place(0, 3);
        Assert.Equal([1, 2, 0, 3], Chain(moved));
        Assert.Equal(3, moved.NumberOf(0));
        Assert.Equal(m.Count, moved.Count);
        var added = m.Place(9, 1);
        Assert.Equal([9, 0, 1, 2, 3], Chain(added));
    }

    [Fact]
    public void InsertRefusesACellOutsideTheGrid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Grid(4, 3).Insert([12], 1));
    }

    [Fact]
    public void RemoveClosesTheGap()
    {
        var m = Grid(4, 3, 0, 1, 2, 3, 4);
        Assert.Equal([0, 1, 3, 4], Chain(m.Remove(2)));
        Assert.Equal([1, 4], Chain(m.Remove([0, 2, 3])));
        Assert.Same(m, m.Remove(11));
        Assert.Same(m, m.Remove([-1, 99]));
    }

    // ---- shapes

    [Fact]
    public void LineRunsAlongRowsColumnsAndDiagonalsBothWays()
    {
        var m = new LedMarkup(5, 5);
        Assert.Equal([m.Cell(1, 2), m.Cell(2, 2), m.Cell(3, 2)], m.LineCells(m.Cell(1, 2), m.Cell(3, 2)));
        Assert.Equal([m.Cell(3, 2), m.Cell(2, 2), m.Cell(1, 2)], m.LineCells(m.Cell(3, 2), m.Cell(1, 2)));
        Assert.Equal([m.Cell(2, 0), m.Cell(2, 1), m.Cell(2, 2)], m.LineCells(m.Cell(2, 0), m.Cell(2, 2)));
        Assert.Equal([m.Cell(0, 0), m.Cell(1, 1), m.Cell(2, 2)], m.LineCells(m.Cell(0, 0), m.Cell(2, 2)));
        Assert.Equal([m.Cell(3, 0), m.Cell(2, 1), m.Cell(1, 2)], m.LineCells(m.Cell(3, 0), m.Cell(1, 2)));
        Assert.Equal([m.Cell(2, 2)], m.LineCells(m.Cell(2, 2), m.Cell(2, 2)));
    }

    [Fact]
    public void LineAtAnyAngleIsOneUnbrokenStepwiseRunFromTheFirstCellToTheLast()
    {
        var m = new LedMarkup(9, 6);
        var cells = m.LineCells(m.Cell(0, 0), m.Cell(8, 3));
        Assert.Equal(9, cells.Count);                       // one cell per column: the long side sets the count
        Assert.Equal((m.Cell(0, 0), m.Cell(8, 3)), (cells[0], cells[^1]));
        Assert.Empty(m.Insert(cells, 1).Jumps());           // neighbours always touch
        Assert.Equal(m.LineCells(m.Cell(0, 0), m.Cell(8, 3)), m.LineCells(0, 0, 8, 3));
    }

    [Fact]
    public void FillSnakeByRowsFromTopLeft()
    {
        var m = new LedMarkup(3, 2);
        Assert.Equal([0, 1, 2, 5, 4, 3], m.RectCells(0, 0, 2, 1, FillDirection.Rows, FillPattern.Snake));
    }

    [Fact]
    public void FillStraightByRowsFromTopLeft()
    {
        var m = new LedMarkup(3, 2);
        Assert.Equal([0, 1, 2, 3, 4, 5], m.RectCells(0, 0, 2, 1, FillDirection.Rows, FillPattern.Straight));
    }

    [Fact]
    public void FillByColumnsRunsDownThenAcross()
    {
        var m = new LedMarkup(3, 2);
        Assert.Equal([0, 3, 4, 1, 2, 5], m.RectCells(0, 0, 2, 1, FillDirection.Columns, FillPattern.Snake));
        Assert.Equal([0, 3, 1, 4, 2, 5], m.RectCells(0, 0, 2, 1, FillDirection.Columns, FillPattern.Straight));
    }

    [Fact]
    public void FillStartsAtEachCorner()
    {
        var m = new LedMarkup(3, 2);
        Assert.Equal([2, 1, 0, 3, 4, 5], m.SweepCells(Corner.TopRight, FillDirection.Rows, FillPattern.Snake));
        Assert.Equal([3, 4, 5, 2, 1, 0], m.SweepCells(Corner.BottomLeft, FillDirection.Rows, FillPattern.Snake));
        Assert.Equal([5, 4, 3, 0, 1, 2], m.SweepCells(Corner.BottomRight, FillDirection.Rows, FillPattern.Snake));
        Assert.Equal([3, 0, 1, 4, 5, 2], m.SweepCells(Corner.BottomLeft, FillDirection.Columns, FillPattern.Snake));
        Assert.Equal([2, 5, 4, 1, 0, 3], m.SweepCells(Corner.TopRight, FillDirection.Columns, FillPattern.Snake));
        Assert.Equal([5, 2, 4, 1, 3, 0], m.SweepCells(Corner.BottomRight, FillDirection.Columns, FillPattern.Straight));
    }

    [Fact]
    public void FillInsideAGridUsesTheRectangleOnly()
    {
        var m = new LedMarkup(5, 4);
        Assert.Equal([m.Cell(2, 1), m.Cell(3, 1), m.Cell(3, 2), m.Cell(2, 2)],
            m.RectCells(2, 1, 3, 2, FillDirection.Rows, FillPattern.Snake));
    }

    [Fact]
    public void ARectangleHangingOutOfTheGridIsClippedToIt()
    {
        var m = new LedMarkup(3, 2);
        Assert.Equal([2, 5], m.RectCells(2, 0, 4, 1, FillDirection.Rows, FillPattern.Straight));
        Assert.Empty(m.RectCells(5, 5, 6, 6, FillDirection.Rows, FillPattern.Snake));
    }

    [Theory]
    [InlineData(Corner.TopLeft)]
    [InlineData(Corner.TopRight)]
    [InlineData(Corner.BottomLeft)]
    [InlineData(Corner.BottomRight)]
    public void AllSixteenSweepsCoverEveryCellOnceAndWalkWithoutJumpsWhenSnaking(Corner corner)
    {
        var m = new LedMarkup(6, 4);
        foreach (var direction in Enum.GetValues<FillDirection>())
            foreach (var pattern in Enum.GetValues<FillPattern>())
            {
                var cells = m.SweepCells(corner, direction, pattern);
                Assert.Equal(24, cells.Count);
                Assert.Equal(24, cells.Distinct().Count());
                var filled = m.Insert(cells, 1);
                if (pattern == FillPattern.Snake) Assert.Empty(filled.Jumps());
                else Assert.NotEmpty(filled.Jumps());
                // The first cell is the corner asked for.
                var (x, y) = m.Position(cells[0]);
                Assert.Equal(corner is Corner.TopLeft or Corner.BottomLeft ? 0 : 5, x);
                Assert.Equal(corner is Corner.TopLeft or Corner.TopRight ? 0 : 3, y);
            }
    }

    // ---- ranges

    [Fact]
    public void ReverseTurnsARangeOnTheSameCells()
    {
        var m = Grid(5, 2, 0, 1, 2, 3, 4, 5);
        Assert.Equal([0, 3, 2, 1, 4, 5], Chain(m.Reverse(2, 4)));
        Assert.Equal([0, 1, 2, 3, 4, 5], Chain(m.Reverse(2, 4).Reverse(2, 4)));
        Assert.Equal(m.Chain, m.Reverse(3, 3).Chain);
    }

    [Fact]
    public void MoveRangeKeepsTheOrderInsideThePiece()
    {
        var m = Grid(5, 2, 0, 1, 2, 3, 4, 5);
        Assert.Equal([0, 4, 5, 1, 2, 3], Chain(m.MoveRange(2, 4, 4)));
        Assert.Equal([3, 4, 0, 1, 2, 5], Chain(m.MoveRange(4, 5, 1)));
        Assert.Equal([0, 1, 5, 2, 3, 4], Chain(m.MoveRange(3, 5, 4)));
    }

    [Fact]
    public void MoveRangeToItsOwnPlaceChangesNothing()
    {
        var m = Grid(5, 2, 0, 1, 2, 3, 4, 5);
        Assert.Equal(m.Chain, m.MoveRange(2, 4, 2).Chain);
    }

    [Fact]
    public void MoveRangeBeyondTheEndIsRefused()
    {
        var m = Grid(5, 2, 0, 1, 2, 3, 4, 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.MoveRange(2, 4, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.MoveRange(2, 4, 0));
    }

    [Fact]
    public void DeleteRangeClosesTheGapAndClearTailCutsTheEnd()
    {
        var m = Grid(5, 2, 0, 1, 2, 3, 4, 5);
        Assert.Equal([0, 4, 5], Chain(m.DeleteRange(2, 4)));
        Assert.Equal([0, 1, 2], Chain(m.ClearTail(3)));
        Assert.Empty(m.ClearTail(0).Chain);
        Assert.Equal(m.Chain, m.ClearTail(6).Chain);
    }

    [Fact]
    public void RangesOutsideTheChainAreRefused()
    {
        var m = Grid(5, 2, 0, 1, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Reverse(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Reverse(2, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.DeleteRange(3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.ClearTail(4));
    }

    // ---- the whole markup

    [Fact]
    public void ClearGivesAnEmptyGridOfTheSameSize()
    {
        var m = Grid(4, 3, 1, 2).Clear();
        Assert.Equal(0, m.Count);
        Assert.Equal((4, 3), (m.Columns, m.Rows));
    }

    [Fact]
    public void ReverseAllMakesLedOneTheLast()
    {
        var m = Grid(4, 3, 7, 3, 9);
        Assert.Equal([9, 3, 7], Chain(m.ReverseAll()));
    }

    [Fact]
    public void MirrorsFlipTheCellsNotTheNumbers()
    {
        var m = Grid(3, 2, 0, 1, 5);
        Assert.Equal([2, 1, 3], Chain(m.MirrorHorizontally()));
        Assert.Equal([3, 4, 2], Chain(m.MirrorVertically()));
        Assert.Equal(m, m.MirrorHorizontally().MirrorHorizontally());
        Assert.Equal(m, m.MirrorVertically().MirrorVertically());
    }

    [Fact]
    public void RotationTurnsTheGridAndFourTurnsComeBack()
    {
        // 3 x 2:   0 1 2   turned clockwise gives 2 x 3:  3 0
        //          3 4 5                                  4 1
        //                                                 5 2
        var m = Grid(3, 2, 0, 1, 2, 3, 4, 5);
        var turned = m.RotateClockwise();
        Assert.Equal((2, 3), (turned.Columns, turned.Rows));
        Assert.Equal([1, 3, 5, 0, 2, 4], Chain(turned));
        Assert.Equal(m, turned.RotateClockwise().RotateClockwise().RotateClockwise());
    }

    [Fact]
    public void ResizeKeepsLedsAtTheirPositionAndDropsThoseOutside()
    {
        var m = Grid(4, 2, 0, 1, 2, 3, 7, 6, 5, 4);
        var (smaller, dropped) = m.Resize(3, 1);
        Assert.Equal(5, dropped);
        Assert.Equal([0, 1, 2], Chain(smaller));

        var (bigger, none) = Grid(2, 2, 0, 1, 3).Resize(4, 3);
        Assert.Equal(0, none);
        Assert.Equal([0, 1, 5], Chain(bigger));   // (1,1) is cell 5 on the wider grid
        Assert.Equal((4, 3), (bigger.Columns, bigger.Rows));
    }

    // ---- what the editor shows

    [Fact]
    public void JumpsListTheLedsThatDoNotTouchTheOneBefore()
    {
        var m = Grid(5, 3, 0, 1, 7, 8, 14);   // (0,0) (1,0) (2,1) (3,1) (4,2): diagonal neighbours touch
        Assert.Empty(m.Jumps());
        var far = Grid(5, 3, 0, 1, 4, 14);
        Assert.Equal([3, 4], far.Jumps());
    }

    [Fact]
    public void MarkupsWithTheSameChainAreEqual()
    {
        Assert.Equal(Grid(3, 2, 1, 2), Grid(3, 2, 1, 2));
        Assert.NotEqual(Grid(3, 2, 1, 2), Grid(3, 2, 2, 1));
        Assert.NotEqual(Grid(3, 2, 1, 2), Grid(2, 3, 1, 2));
        Assert.Equal(Grid(3, 2, 1, 2).GetHashCode(), Grid(3, 2, 1, 2).GetHashCode());
    }

    [Fact]
    public void OperationsLeaveTheOriginalAlone()
    {
        var m = Grid(4, 3, 0, 1, 2);
        _ = m.Insert([8], 1);
        _ = m.Remove(1);
        _ = m.ReverseAll();
        Assert.Equal([0, 1, 2], Chain(m));
    }
}
