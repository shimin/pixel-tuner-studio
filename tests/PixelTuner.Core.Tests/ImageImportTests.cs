using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Tests;

public class ImageImportTests
{
    private static PixelImage Solid(int width, int height, Rgb color)
    {
        var image = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                image[x, y] = color;
        return image;
    }

    [Theory]
    [InlineData(100, 50, 36, 72)]    // twice as wide as high: 72 columns at 36 LEDs
    [InlineData(100, 100, 36, 36)]
    [InlineData(101, 100, 36, 36)]   // 36.36 rounds down
    [InlineData(150, 100, 25, 38)]   // 37.5: a half goes to the even number, as Math.Round did in PixelTuner 2
    [InlineData(154, 100, 25, 38)]   // 38.5 goes to 38, not 39
    [InlineData(1, 1000, 36, 1)]     // a hair of a picture still has a column
    public void The_scaled_width_keeps_the_proportions(int width, int height, int target, int expected)
    {
        Assert.Equal(expected, ImageImport.ScaledWidth(width, height, target));
    }

    [Fact]
    public void Crop_takes_a_rectangle_and_refuses_one_outside_the_picture()
    {
        var image = new PixelImage(4, 3);
        image[2, 1] = new Rgb(9, 8, 7);

        var cut = ImageImport.Crop(image, new PixelRect(1, 1, 3, 2));

        Assert.Equal((3, 2), (cut.Width, cut.Height));
        Assert.Equal(new Rgb(9, 8, 7), cut[1, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageImport.Crop(image, new PixelRect(2, 0, 3, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageImport.Crop(image, new PixelRect(0, 0, 0, 1)));
    }

    [Fact]
    public void Scaling_gives_the_size_asked_and_keeps_a_flat_colour()
    {
        var scaled = ImageImport.Scale(Solid(40, 20, new Rgb(200, 100, 50)), 100, 50);
        Assert.Equal((100, 50), (scaled.Width, scaled.Height));
        Assert.Equal(new Rgb(200, 100, 50), scaled[50, 25]);

        var smaller = ImageImport.Scale(Solid(40, 20, new Rgb(200, 100, 50)), 8, 4);
        Assert.Equal(new Rgb(200, 100, 50), smaller[4, 2]);
    }

    [Fact]
    public void A_downscale_averages_instead_of_skipping_pixels()
    {
        // A one-pixel checkerboard of black and white halves to grey, not to black or white.
        var board = new PixelImage(64, 64);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                board[x, y] = (x + y) % 2 == 0 ? new Rgb(255, 255, 255) : Rgb.Black;

        var small = ImageImport.Scale(board, 8, 8);

        Assert.InRange(small[4, 4].R, 110, 145);
    }

    [Fact]
    public void Fit_crops_scales_to_the_height_of_the_device_and_corrects_colour_last()
    {
        var source = Solid(200, 100, new Rgb(100, 100, 100));
        source[150, 50] = new Rgb(255, 0, 0);

        // The right half (100 x 100) at 36 LEDs: 36 x 36.
        var fitted = ImageImport.Fit(source, new PixelRect(100, 0, 100, 100), 36);
        Assert.Equal((36, 36), (fitted.Width, fitted.Height));

        var brighter = ImageImport.Fit(source, null, 50, new ImportAdjustments(Brightness: 100));
        Assert.Equal((100, 50), (brighter.Width, brighter.Height));
        Assert.Equal(new Rgb(200, 200, 200), brighter[5, 5]);
    }

    [Fact]
    public void Without_corrections_the_picture_is_returned_as_it_is()
    {
        var image = Solid(2, 2, new Rgb(1, 2, 3));
        Assert.Same(image, ImageImport.Adjust(image, ImportAdjustments.None));
    }

    [Theory]
    [InlineData(100, false, 100, 200)]
    [InlineData(-200, false, 100, 0)]      // clamped at black
    [InlineData(200, false, 100, 255)]     // and at white
    [InlineData(100, true, 20, 30)]        // midtones only: the darkest values move a tenth as far
    [InlineData(100, true, 120, 220)]      // values above the threshold move fully
    public void Brightness_adds_to_every_channel(int brightness, bool midtones, int input, int expected)
    {
        var result = ImageImport.Adjust(Solid(1, 1, new Rgb((byte)input, (byte)input, (byte)input)), new ImportAdjustments(Brightness: brightness, MidtonesOnly: midtones));
        Assert.Equal(expected, result[0, 0].R);
    }

    [Fact]
    public void Contrast_pushes_values_away_from_the_middle()
    {
        var more = ImageImport.Adjust(Solid(1, 1, new Rgb(180, 128, 80)), new ImportAdjustments(Contrast: 50));
        Assert.True(more[0, 0].R > 180);
        Assert.True(more[0, 0].B < 80);
        Assert.InRange(more[0, 0].G, 126, 130);   // the middle stays

        var less = ImageImport.Adjust(Solid(1, 1, new Rgb(200, 50, 128)), new ImportAdjustments(Contrast: -50));
        Assert.True(less[0, 0].R < 200 && less[0, 0].G > 50);
    }

    [Fact]
    public void Hue_saturation_and_value_move_the_colour_as_in_PixelTuner_2()
    {
        var red = Solid(1, 1, new Rgb(255, 0, 0));

        var cyan = ImageImport.Adjust(red, new ImportAdjustments(Hue: 180))[0, 0];
        Assert.Equal(new Rgb(0, 255, 255), cyan);

        var grey = ImageImport.Adjust(red, new ImportAdjustments(Saturation: -100))[0, 0];
        Assert.Equal(grey.R, grey.G);
        Assert.Equal(grey.G, grey.B);

        var dark = ImageImport.Adjust(Solid(1, 1, new Rgb(200, 200, 200)), new ImportAdjustments(Value: -100))[0, 0];
        Assert.Equal(Rgb.Black, dark);
        var light = ImageImport.Adjust(Solid(1, 1, new Rgb(20, 20, 20)), new ImportAdjustments(Value: 100))[0, 0];
        Assert.Equal(new Rgb(255, 255, 255), light);
    }

    [Fact]
    public void Sharpening_leaves_flat_areas_and_the_border_alone_and_strengthens_an_edge()
    {
        var edge = new PixelImage(8, 5);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 8; x++)
                edge[x, y] = x < 4 ? new Rgb(100, 100, 100) : new Rgb(150, 150, 150);

        var sharp = ImageImport.Adjust(edge, new ImportAdjustments(Sharpen: 2));

        Assert.Equal(new Rgb(100, 100, 100), sharp[1, 2]);   // flat
        Assert.Equal(edge[0, 0], sharp[0, 0]);                // border
        Assert.True(sharp[3, 2].R < 100);                     // darker on the dark side of the edge
        Assert.True(sharp[4, 2].R > 150);                     // brighter on the bright side
    }

    [Fact]
    public void A_picture_survives_png_encoding_exactly()
    {
        var image = new PixelImage(3, 2);
        image[0, 0] = new Rgb(1, 2, 3);
        image[2, 1] = new Rgb(250, 128, 7);

        var again = ImageDecoder.Decode(ImageImport.EncodePng(image));

        Assert.Equal((3, 2), (again.Width, again.Height));
        Assert.Equal(image[0, 0], again[0, 0]);
        Assert.Equal(image[2, 1], again[2, 1]);
    }
}
