using System.Buffers.Binary;
using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Legacy;

namespace PixelTuner.Core.Tests;

public class PixelBinaryWriterTests
{
    private static PixelImage Image(int width, int height, Rgb color)
    {
        var image = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                image[x, y] = color;
        return image;
    }

    private static PixelExportTimeline Line(params PixelExportItem[] items)
    {
        var line = new PixelExportTimeline { Color = new Rgb(0, 0, 25) };
        line.Items.AddRange(items);
        return line;
    }

    [Fact]
    public void Header_describes_the_file()
    {
        var item = new PixelExportItem { ImageKey = "a", Image = Image(2, 4, new Rgb(10, 20, 30)), DurationMs = 1500, Stretching = 3 };
        byte[] bin = PixelBinaryWriter.Write([Line(item)], height: 4);

        Assert.Equal("PPOI"u8.ToArray(), bin[..4]);
        Assert.Equal(1, bin[4]);
        Assert.Equal(4, BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(5)));
        Assert.Equal((uint)bin.Length, BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(7)));
        Assert.Equal(1, bin[11]);

        int line = (int)BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(12));
        Assert.Equal(16, line);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(line)));
        Assert.Equal(new byte[] { 0, 0, 25, 70, 0, 0 }, bin[(line + 2)..(line + 8)]);
        Assert.Equal(1500u, BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(line + 8)));
        Assert.Equal(3, BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(line + 16)));

        // Image block: 8 bits per pixel, address of its last byte, then 2 columns × 4 LEDs × RGB.
        int image = (int)BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(line + 12));
        Assert.Equal(8, bin[image]);
        Assert.Equal((uint)(bin.Length - 1), BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(image + 1)));
        Assert.Equal(image + 5 + 2 * 4 * 3, bin.Length);
    }

    [Fact]
    public void Default_leds_scale_green_and_blue_down()
    {
        var item = new PixelExportItem { Image = Image(1, 1, new Rgb(200, 200, 200)) };
        byte[] bin = PixelBinaryWriter.Write([Line(item)], height: 1);
        Assert.Equal(new byte[] { 200, 100, 160 }, bin[^3..]);
    }

    [Fact]
    public void Same_picture_is_stored_once()
    {
        var picture = Image(3, 2, new Rgb(1, 2, 3));
        var first = new PixelExportItem { ImageKey = "./a.bmp", Image = picture, DurationMs = 100 };
        var again = new PixelExportItem { ImageKey = "./a.bmp", Image = picture, StartMs = 100, DurationMs = 100 };
        var dimmer = new PixelExportItem { ImageKey = "./a.bmp", Image = picture, StartMs = 200, DurationMs = 100, Brightness = 50 };

        byte[] bin = PixelBinaryWriter.Write([Line(first, again, dimmer)], height: 2);
        var addresses = LegacyBinaryAssert.ImageEntries(bin).Select(e => e.Address).ToList();

        Assert.Equal(addresses[0], addresses[1]);
        Assert.NotEqual(addresses[0], addresses[2]);
    }

    [Fact]
    public void Short_image_sits_on_the_bottom_leds()
    {
        var item = new PixelExportItem { Image = Image(1, 1, new Rgb(100, 0, 0)) };
        byte[] bin = PixelBinaryWriter.Write([Line(item)], height: 3);
        // One column: R of LEDs 0..2, then G, then B. Only the bottom LED is lit.
        Assert.Equal(new byte[] { 0, 0, 100, 0, 0, 0, 0, 0, 0 }, bin[^9..]);
    }

    [Fact]
    public void Flip_turns_the_picture_upside_down()
    {
        var image = new PixelImage(1, 2) { [0, 0] = new Rgb(100, 0, 0) };
        var item = new PixelExportItem { Image = image, FlipVertical = true };
        byte[] bin = PixelBinaryWriter.Write([Line(item)], height: 2);
        Assert.Equal(new byte[] { 0, 100 }, bin[^6..^4]);
    }

    [Fact]
    public void Empty_timelines_are_left_out()
    {
        var item = new PixelExportItem { Image = Image(1, 1, new Rgb(1, 1, 1)) };
        byte[] bin = PixelBinaryWriter.Write([Line(), Line(item)], height: 1);
        Assert.Equal(1, bin[11]);
    }

    [Theory]
    [InlineData(ColorDepth.Bit12, 4, 3 * 3)]     // 5 LEDs: 3 bytes per channel
    [InlineData(ColorDepth.Bit24, 8, 5 * 3)]
    [InlineData(ColorDepth.Bit36, 12, 5 * 3 * 2)]
    public void Colour_depth_sets_the_column_size(ColorDepth depth, byte bits, int columnBytes)
    {
        byte[] block = PixelBinaryWriter.EncodeImage(Image(2, 5, new Rgb(255, 255, 255)), 5, depth, 100);
        Assert.Equal(bits, block[0]);
        Assert.Equal(5 + 2 * columnBytes, block.Length);
    }

    [Fact]
    public void Ptp_times_are_read_as_offsets_from_the_show_start()
    {
        Assert.Equal(new TimeSpan(0, 1, 2, 3, 450), PtpDocument.ParseTime("01:02:03.450"));
        Assert.Equal(TimeSpan.Zero, PtpDocument.ParseTime(null));
    }

    [Fact]
    public void Missing_absolute_path_falls_back_to_the_project_folder()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ptp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "Pattern.BMP"), [0]);
            var resolver = new PtpMediaResolver(dir);
            Assert.Equal(Path.Combine(dir, "Pattern.BMP"), resolver.Resolve(@"C:\Users\someone\shows\pattern.bmp"));
            Assert.Equal(Path.Combine(dir, "Pattern.BMP"), resolver.Resolve("./pattern.bmp"));
            Assert.Throws<FileNotFoundException>(() => resolver.Resolve("./other.bmp"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
