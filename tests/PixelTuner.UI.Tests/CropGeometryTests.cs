using PixelTuner.Core.Imaging;
using PixelTuner.UI.LibraryPanel;

namespace PixelTuner.UI.Tests;

public class CropGeometryTests
{
    private static readonly PixelRect Rect = new(20, 10, 40, 30);   // right 60, bottom 40

    [Theory]
    [InlineData(20, 10, CropHandle.TopLeft)]
    [InlineData(61, 10, CropHandle.TopRight)]
    [InlineData(19, 41, CropHandle.BottomLeft)]
    [InlineData(60, 40, CropHandle.BottomRight)]
    [InlineData(40, 25, CropHandle.Move)]
    [InlineData(5, 5, CropHandle.New)]
    [InlineData(90, 25, CropHandle.New)]
    public void The_pointer_grabs_a_corner_the_inside_or_nothing(double x, double y, CropHandle expected)
    {
        Assert.Equal(expected, CropGeometry.HitTest(Rect, x, y, grab: 2));
    }

    [Fact]
    public void Moving_keeps_the_size_and_stays_inside_the_picture()
    {
        Assert.Equal(new PixelRect(30, 15, 40, 30), CropGeometry.Drag(CropHandle.Move, Rect, 40, 25, 50, 30, 100, 80));
        Assert.Equal(new PixelRect(60, 50, 40, 30), CropGeometry.Drag(CropHandle.Move, Rect, 40, 25, 500, 500, 100, 80));
        Assert.Equal(new PixelRect(0, 0, 40, 30), CropGeometry.Drag(CropHandle.Move, Rect, 40, 25, -500, -500, 100, 80));
    }

    [Fact]
    public void A_corner_moves_while_the_opposite_corner_stays()
    {
        Assert.Equal(new PixelRect(25, 12, 35, 28), CropGeometry.Drag(CropHandle.TopLeft, Rect, 20, 10, 25, 12, 100, 80));
        Assert.Equal(new PixelRect(20, 10, 50, 40), CropGeometry.Drag(CropHandle.BottomRight, Rect, 60, 40, 70, 50, 100, 80));
        Assert.Equal(new PixelRect(20, 5, 45, 35), CropGeometry.Drag(CropHandle.TopRight, Rect, 60, 10, 65, 5, 100, 80));
        Assert.Equal(new PixelRect(10, 10, 50, 35), CropGeometry.Drag(CropHandle.BottomLeft, Rect, 20, 40, 10, 45, 100, 80));
    }

    [Fact]
    public void Dragging_a_corner_past_the_opposite_one_flips_the_rectangle_and_never_goes_below_a_pixel()
    {
        // The bottom right corner pulled up and left of the top left one.
        Assert.Equal(new PixelRect(5, 2, 15, 8), CropGeometry.Drag(CropHandle.BottomRight, Rect, 60, 40, 5, 2, 100, 80));
        // Onto the opposite corner: one pixel.
        var tiny = CropGeometry.Drag(CropHandle.BottomRight, Rect, 60, 40, 20, 10, 100, 80);
        Assert.Equal((1, 1), (tiny.Width, tiny.Height));
    }

    [Fact]
    public void A_new_rectangle_runs_from_where_the_pointer_went_down_and_is_cut_to_the_picture()
    {
        Assert.Equal(new PixelRect(10, 10, 30, 20), CropGeometry.Drag(CropHandle.New, Rect, 10, 10, 40, 30, 100, 80));
        Assert.Equal(new PixelRect(50, 40, 50, 40), CropGeometry.Drag(CropHandle.New, Rect, 50, 40, 900, 900, 100, 80));
        Assert.Equal(new PixelRect(0, 0, 30, 30), CropGeometry.Drag(CropHandle.New, Rect, 30, 30, -50, -50, 100, 80));
    }

    [Fact]
    public void Nothing_grabbed_changes_nothing()
    {
        Assert.Equal(Rect, CropGeometry.Drag(CropHandle.None, Rect, 0, 0, 50, 50, 100, 80));
    }
}
