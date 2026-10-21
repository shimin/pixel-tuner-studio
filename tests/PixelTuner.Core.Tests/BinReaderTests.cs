using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Tests;

/// <summary>The reader is how every later test of the editor looks into a .bin, so it is checked first, on its own.</summary>
public class BinReaderTests
{
    private static PixelImage Image(int width, int height, Func<int, int, Rgb> pixel)
    {
        var image = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                image[x, y] = pixel(x, y);
        return image;
    }

    /// <summary>The example of docs/binary-format.md, section 1.9, written out byte by byte.</summary>
    private static readonly byte[] DocumentedExample = Convert.FromHexString(string.Concat(
        "5050 4F49 0104 006E 0000 0001 1000 0000",
        "0300 1900 0046 0000 AC0D 0000 4000 0000",
        "0400 0000 0000 0000 5D00 0000 0400 0000",
        "E803 0000 4000 0000 0200 FFFF DC05 0000",
        "085C 0000 00FF 0000 FF00 7F00 7F00 00CC",
        "CC0A 0064 C80A 0032 0018 0050 2808 6D00",
        "0000 0000 0000 0000 0000 0000 0000").Replace(" ", ""));

    [Fact]
    public void Reads_the_example_of_the_format_document()
    {
        var file = BinReader.ReadPpoi(DocumentedExample);

        Assert.Equal(1, file.Version);
        Assert.Equal(4, file.Height);
        Assert.Equal(110, file.Size);
        var line = Assert.Single(file.Timelines);
        Assert.Equal(0x10, line.Address);
        Assert.Equal(new Rgb(25, 0, 0), line.Color);
        Assert.Equal(new Rgb(70, 0, 0), line.SelectionColor);
        Assert.Equal(3500u, line.DurationMs);
        Assert.Equal([0x40u, 0x5Du, 0x40u], line.Entries.Select(e => e.ImageAddress));
        Assert.Equal([(ushort)4, (ushort)4, (ushort)2], line.Entries.Select(e => e.Stretching));
        Assert.Equal([false, false, true], line.Entries.Select(e => e.LaserOn));
        Assert.Equal([0u, 1000u, 1500u], line.Entries.Select(e => e.StartMs));

        var picture = line.Entries[0].Image;
        Assert.Same(picture, line.Entries[2].Image);
        Assert.Equal(8, picture.BitsPerChannel);
        Assert.Equal(2, picture.Width);
        Assert.Equal(255, picture.Value(0, 0, 0));
        Assert.Equal(0x7F, picture.Value(0, 1, 1));   // green is halved on a plain 24-bit device
        Assert.Equal(0xCC, picture.Value(0, 2, 2));   // blue is scaled by 0.8
        Assert.Equal(0x28, picture.Value(1, 3, 2));
        Assert.Equal(1, line.Entries[1].Image.Width);
    }

    [Fact]
    public void Reads_back_what_the_pixel_writer_wrote_at_every_depth()
    {
        const int height = 7;   // odd on purpose: the 12-bit layout pads it
        var picture = Image(3, height, (x, y) => new Rgb((byte)(x * 80 + y), (byte)(255 - y * 30), (byte)(y * 36 + x)));
        var line = new PixelExportTimeline();
        line.Items.Add(new PixelExportItem { ImageKey = "a", Image = picture, DurationMs = 900, Stretching = 6, LaserOn = true });

        foreach (var depth in Enum.GetValues<ColorDepth>())
        {
            var file = BinReader.ReadPpoi(PixelBinaryWriter.Write([line], height, depth));
            var image = file.Timelines[0].Entries[0].Image;
            Assert.Equal(3, image.Width);
            Assert.Equal(height, image.Height);
            Assert.Equal(depth switch { ColorDepth.Bit12 => 4, ColorDepth.Bit24 => 8, _ => 12 }, image.BitsPerChannel);
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < height; y++)
                {
                    var c = picture[x, y];
                    var color = new PixelColor(height, 100);
                    for (int channel = 0; channel < 3; channel++)
                    {
                        // The writer's own colour code is the oracle here; the reader only has to find the value.
                        int expected = depth switch
                        {
                            ColorDepth.Bit12 => color.To4Bit(c, channel),
                            ColorDepth.Bit24 => color.To8Bit(c, channel),
                            _ => color.To12Bit(c, channel),
                        };
                        Assert.Equal(expected, image.Value(x, y, channel));
                    }
                }
        }
    }

    [Fact]
    public void Entries_that_share_a_picture_share_one_block()
    {
        var picture = Image(2, 3, (x, y) => new Rgb(1, 2, 3));
        var line = new PixelExportTimeline();
        line.Items.Add(new PixelExportItem { ImageKey = "a", Image = picture, StartMs = 0, DurationMs = 100 });
        line.Items.Add(new PixelExportItem { ImageKey = "a", Image = picture, StartMs = 100, DurationMs = 100 });
        var file = BinReader.ReadPpoi(PixelBinaryWriter.Write([line], 3));

        Assert.Same(file.Timelines[0].Entries[0].Image, file.Timelines[0].Entries[1].Image);
        Assert.Single(BinReader.Images(file));
    }

    [Theory]
    [InlineData("80_sample", "0.bin")]
    [InlineData("bugi", "all.bin")]
    [InlineData("36_fan", "0.bin")]
    [InlineData("125_newYear", "all.bin")]
    [InlineData("150_stavropol", "all.bin")]
    [InlineData("150_stavropol", "2.bin")]
    [InlineData("cheer_power", "1.bin")]
    [InlineData("glasses", "0.bin")]
    [InlineData("glasses", "1.bin")]
    public void Reads_the_shows_PixelTuner_2_wrote(string folder, string bin)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyProjects", folder, "bin", bin));
        var file = BinReader.ReadPpoi(data);

        Assert.NotEmpty(file.Timelines);
        // Blocks are packed one after another after the last timeline, and the file ends with the last block.
        var images = BinReader.Images(file).ToList();
        Assert.Equal(file.Timelines.Max(t => t.Address + 12 + 12 * t.Entries.Count), (int)images[0].Address);
        for (int i = 0; i + 1 < images.Count; i++)
            Assert.Equal(images[i].Address + images[i].BlockSize, images[i + 1].Address);
        Assert.Equal(data.Length, (int)images[^1].Address + images[^1].BlockSize);

        foreach (var line in file.Timelines)
        {
            // Entries follow one another without gaps from 0, and the timeline lasts at least until its last start.
            Assert.Equal(0u, line.Entries[0].StartMs);
            Assert.Equal(line.Entries.OrderBy(e => e.StartMs).Select(e => e.StartMs), line.Entries.Select(e => e.StartMs));
            Assert.True(line.DurationMs >= line.Entries[^1].StartMs);
        }
    }

    [Fact]
    public void Reads_a_light_show_with_colour_and_monochrome_channels()
    {
        var colour = new LightExportTimeline();
        colour.Items.Add(new LightExportItem { Effect = LightEffect.Solid, StartMs = 0, EndMs = 20, ColorFirst = new Rgb(1, 2, 3), ColorLast = new Rgb(1, 2, 3) });
        colour.Items.Add(new LightExportItem { Effect = LightEffect.Solid, StartMs = 20, EndMs = 40, ColorFirst = new Rgb(9, 8, 7), ColorLast = new Rgb(9, 8, 7) });
        var mono = new LightExportTimeline { IsMonochrome = true };
        mono.Items.Add(new LightExportItem { Effect = LightEffect.Solid, StartMs = 0, EndMs = 40, ColorFirst = new Rgb(77, 0, 0), ColorLast = new Rgb(77, 0, 0) });

        var file = BinReader.ReadLght(LightBinaryWriter.Write([colour, mono]));

        Assert.Equal(2, file.Slots);
        Assert.Equal(3, file.PointSize);
        Assert.Equal(40u, file.DurationMs);
        Assert.Equal([0u, 20u, 40u], file.Samples.Select(s => s.TimeMs));
        Assert.Equal(new Rgb(1, 2, 3), file.At(0).Slot(0));
        Assert.Equal(new Rgb(77, 0, 0), file.At(0).Slot(1));
        Assert.Equal(new Rgb(1, 2, 3), file.At(19).Slot(0));
        Assert.Equal(new Rgb(9, 8, 7), file.At(25).Slot(0));
    }

    [Fact]
    public void Refuses_what_is_not_a_well_formed_file()
    {
        Assert.Throws<InvalidDataException>(() => BinReader.ReadPpoi("LGHT0123456789ab"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => BinReader.ReadLght(DocumentedExample));
        Assert.Throws<InvalidDataException>(() => BinReader.ReadPpoi(DocumentedExample[..60]));

        var wrongSize = (byte[])DocumentedExample.Clone();
        wrongSize[7] = 111;
        Assert.Throws<InvalidDataException>(() => BinReader.ReadPpoi(wrongSize));

        var wrongDepth = (byte[])DocumentedExample.Clone();
        wrongDepth[0x40] = 7;
        Assert.Throws<InvalidDataException>(() => BinReader.ReadPpoi(wrongDepth));
    }
}
