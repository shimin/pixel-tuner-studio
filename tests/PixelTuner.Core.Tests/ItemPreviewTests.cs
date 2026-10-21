using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Tests;

public class ItemPreviewTests
{
    private static PixelImage TwoByTwo()
    {
        var image = new PixelImage(2, 2);
        image[0, 0] = new Rgb(200, 100, 50);
        image[1, 0] = new Rgb(10, 20, 30);
        image[0, 1] = new Rgb(1, 2, 3);
        image[1, 1] = new Rgb(255, 255, 255);
        return image;
    }

    [Fact]
    public void Brightness_scales_and_truncates_like_PixelTuner_2()
    {
        var result = ItemPreview.Render(TwoByTwo(), 50, 1, false);
        Assert.Equal(new Rgb(100, 50, 25), result[0, 0]);
        Assert.Equal(new Rgb(127, 127, 127), result[1, 1]);
        Assert.Equal(new Rgb(0, 1, 1), result[0, 1]);
    }

    [Fact]
    public void Columns_repeat_by_the_stretching()
    {
        var result = ItemPreview.Render(TwoByTwo(), 100, 3, false);
        Assert.Equal((6, 2), (result.Width, result.Height));
        Assert.Equal(new Rgb(200, 100, 50), result[2, 0]);
        Assert.Equal(new Rgb(10, 20, 30), result[3, 0]);
    }

    [Fact]
    public void Flip_turns_the_picture_over_and_zero_brightness_is_black()
    {
        var flipped = ItemPreview.Render(TwoByTwo(), 100, 1, true);
        Assert.Equal(new Rgb(1, 2, 3), flipped[0, 0]);
        Assert.Equal(Rgb.Black, ItemPreview.Render(TwoByTwo(), 0, 1, false)[0, 0]);
    }

    [Theory]
    [InlineData(100, 100, 2.0)]    // a 100 px picture, 100 x 50 us per column: 500 ms a pass, twice in a second
    [InlineData(100, 200, 1.0)]
    [InlineData(100, 10, 20.0)]
    [InlineData(100, 300, 1000.0 / 1500)]   // the second pass is cut off
    public void Stretching_sets_the_speed_and_the_item_length_sets_how_often_the_picture_repeats(int width, int stretching, double repeats)
    {
        var second = TimeSpan.FromMilliseconds(1000);
        Assert.Equal(repeats, ItemPreview.Repeats(second, width, stretching), 9);
        Assert.Equal(TimeSpan.FromMilliseconds(width * stretching * 0.05), ItemPreview.PassDuration(width, stretching));
    }

    [Fact]
    public void Video_stretching_is_milliseconds_per_frame()
    {
        var pass = ItemPreview.VideoPassDuration(25, 40);   // 25 frames at 40 ms: one second
        Assert.Equal(TimeSpan.FromSeconds(1), pass);
        Assert.Equal(12.0, ItemPreview.Repeats(TimeSpan.FromSeconds(12), pass), 9);
        Assert.Equal(0.45, ItemPreview.Repeats(TimeSpan.FromSeconds(10), ItemPreview.VideoPassDuration(556, 40)), 2);
    }

    [Fact]
    public void Epic_column_is_a_fifth_of_a_millisecond_per_step_as_in_the_repeat_window()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(100), ItemPreview.EpicPassDuration(50, 10));   // 50 columns, 2 ms each
        Assert.Equal(4.0, ItemPreview.Repeats(TimeSpan.FromMilliseconds(400), ItemPreview.EpicPassDuration(50, 10)), 9);
    }
}
