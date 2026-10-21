using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Tests;

/// <summary>
/// The exact match with PixelTuner 2's warp is checked in the oracle tests (<c>WarpOracleTests</c>); these say what it does.
/// </summary>
public class PerspectiveWarpTests
{
    private static PixelImage White(int width, int height)
    {
        var image = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                image[x, y] = new Rgb(255, 255, 255);
        return image;
    }

    private static int Picture(PixelImage image, int y)
    {
        int left = -1, right = -1;
        for (int x = 0; x < image.Width; x++)
            if (image[x, y] != Rgb.Black)
            {
                if (left < 0) left = x;
                right = x;
            }
        return left < 0 ? 0 : right - left + 1;
    }

    [Fact]
    public void The_top_is_squeezed_more_than_the_middle_and_the_middle_more_than_the_bottom()
    {
        var warped = PerspectiveWarp.Apply(White(200, 100));

        int top = Picture(warped, 10), middle = Picture(warped, 50), bottom = Picture(warped, 90);
        Assert.True(top < middle && middle < bottom, $"{top} < {middle} < {bottom}");
        // 0.65 keeps 30 % of the width at the top row, 0.8 keeps 60 % in the middle (give or take the blanked border and the spline).
        Assert.InRange(top, 45, 75);
        Assert.InRange(middle, 105, 130);
        Assert.True(bottom >= 190);
    }

    [Fact]
    public void An_offset_of_one_everywhere_leaves_the_picture_whole_apart_from_the_blanked_border()
    {
        var warped = PerspectiveWarp.Apply(White(50, 30), 1, 1, 1);

        Assert.Equal(new Rgb(255, 255, 255), warped[25, 15]);
        // PixelTuner 2 blanks the outer rows and columns before it warps: two at the top and left, one at the bottom and right.
        Assert.Equal(Rgb.Black, warped[0, 15]);
        Assert.Equal(Rgb.Black, warped[25, 0]);
        Assert.Equal(Rgb.Black, warped[25, 29]);
    }

    [Fact]
    public void The_size_stays_and_a_tall_picture_is_brought_to_800_high_first()
    {
        var small = PerspectiveWarp.Apply(White(40, 20));
        Assert.Equal((40, 20), (small.Width, small.Height));

        var tall = PerspectiveWarp.Apply(White(500, 1600));
        Assert.Equal((250, 800), (tall.Width, tall.Height));   // PixelTuner 2's page worked at that size
    }

    [Theory]
    [InlineData(0.5, 0.8, 1)]
    [InlineData(0.65, 1.01, 1)]
    [InlineData(0.65, 0.8, 0.2)]
    public void Offsets_outside_the_sliders_range_are_refused(double top, double middle, double bottom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PerspectiveWarp.Apply(White(10, 10), top, middle, bottom));
    }

    [Fact]
    public void Colours_move_with_the_squeeze_instead_of_being_lost()
    {
        // A left half red, a right half blue: after the squeeze each row still shows red then blue.
        var image = new PixelImage(100, 40);
        for (int y = 0; y < 40; y++)
            for (int x = 0; x < 100; x++)
                image[x, y] = x < 50 ? new Rgb(255, 0, 0) : new Rgb(0, 0, 255);

        var warped = PerspectiveWarp.Apply(image);

        var row = Enumerable.Range(0, 100).Select(x => warped[x, 20]).Where(c => c != Rgb.Black).ToList();
        Assert.True(row.First().R > 200);
        Assert.True(row.Last().B > 200);
    }
}
