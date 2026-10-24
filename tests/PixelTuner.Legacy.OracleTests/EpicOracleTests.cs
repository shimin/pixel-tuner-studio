using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Communication.Gradient;
using PixelTuner.Core.Legacy;
using Xunit;
using LegacyDepth = Communication.ColorDepth;
using StudioDepth = PixelTuner.Core.Export.ColorDepth;

namespace PixelTuner.Legacy.OracleTests;

/// <summary>
/// E-Pic poi .bin: Studio's port against PixelTuner 2's Communication/Gradient on random shows.
/// The glue of FileViewModel.GetEpicBinaryPresentation (CreateEpicProjectFile, FillGabs,
/// CreateBlankGradientItem) is repeated here as it was.
/// </summary>
public sealed class EpicOracleTests : IDisposable
{
    private const int DefaultStretching = 4;
    // Lower case: PixelTuner 2 lower-cased item paths before it opened them.
    private readonly string _folder = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "epic-oracle-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData(1, StudioDepth.Bit24)]
    [InlineData(2, StudioDepth.Bit24)]
    [InlineData(3, StudioDepth.Bit24)]
    [InlineData(4, StudioDepth.Bit36)]
    [InlineData(5, StudioDepth.Bit36)]
    public void Random_show_matches_PixelTuner_2(int seed, StudioDepth depth)
    {
        var random = new Random(seed);
        // Gradient pictures of random sizes; one name is never written, as a missing file.
        // PixelTuner 2's 36-bit blue table stopped at 254 and threw on full blue (Studio completes it).
        byte max = depth == StudioDepth.Bit36 ? (byte)254 : (byte)255;
        var files = Enumerable.Range(0, 6).Select(i => WriteBmp(random, $"g{i}.bmp", max)).ToList();
        files.Add(Path.Combine(_folder, "missing.bmp"));

        var lines = Enumerable.Range(0, random.Next(1, 5)).Select(_ => RandomLine(random, files)).ToList();
        var document = PtpDocument.Load(new MemoryStream(Encoding.UTF8.GetBytes(ToPtp(lines))));
        var export = new PtpEpicExport(path => File.Exists(path) ? path : null) { Depth = depth };
        byte[] actual = export.Export(PtpEpicExport.EpicTimelines(document));

        byte[] expected = Legacy(lines, (LegacyDepth)Enum.Parse(typeof(LegacyDepth), depth.ToString()));
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// PixelTuner 2 wrote broken 12-bit files, so 12 bits are checked against the 24-bit export
    /// (checked above): the same timelines, and each image holds 15 * value / 255 of its colours,
    /// with its end address and the next image right after it.
    /// </summary>
    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Twelve_bit_export_is_the_24_bit_one_in_4_bits(int seed)
    {
        var random = new Random(seed);
        var files = Enumerable.Range(0, 6).Select(i => WriteBmp(random, $"g{i}.bmp", 255)).ToList();
        files.Add(Path.Combine(_folder, "missing.bmp"));
        var lines = Enumerable.Range(0, random.Next(1, 5)).Select(_ => RandomLine(random, files)).ToList();
        var document = PtpDocument.Load(new MemoryStream(Encoding.UTF8.GetBytes(ToPtp(lines))));
        var timelines = PtpEpicExport.EpicTimelines(document);
        byte[] bin24 = new PtpEpicExport(path => File.Exists(path) ? path : null) { Depth = StudioDepth.Bit24 }.Export(timelines);
        byte[] bin12 = new PtpEpicExport(path => File.Exists(path) ? path : null) { Depth = StudioDepth.Bit12 }.Export(timelines);

        Assert.Equal(bin24.AsSpan(0, 7).ToArray(), bin12.AsSpan(0, 7).ToArray());
        Assert.Equal((uint)bin12.Length, System.BitConverter.ToUInt32(bin12, 7));
        int lineCount = bin24[11];
        uint imagesStart = 0;
        for (int t = 0; t < lineCount; t++)
        {
            int line = (int)System.BitConverter.ToUInt32(bin24, 12 + 4 * t);
            Assert.Equal(line, (int)System.BitConverter.ToUInt32(bin12, 12 + 4 * t));
            int count = System.BitConverter.ToUInt16(bin24, line);
            Assert.Equal(bin24.AsSpan(line, 12).ToArray(), bin12.AsSpan(line, 12).ToArray());
            for (int i = 0; i < count; i++)
            {
                int entry = line + 12 + 12 * i;
                // Everything but the image address is the same.
                Assert.Equal(bin24.AsSpan(entry + 4, 8).ToArray(), bin12.AsSpan(entry + 4, 8).ToArray());
                int a24 = (int)System.BitConverter.ToUInt32(bin24, entry);
                int a12 = (int)System.BitConverter.ToUInt32(bin12, entry);
                imagesStart = (uint)(line + 12 + 12 * count);

                Assert.Equal(8, bin24[a24]);
                Assert.Equal(4, bin12[a12]);
                int end24 = (int)System.BitConverter.ToUInt32(bin24, a24 + 1);
                int end12 = (int)System.BitConverter.ToUInt32(bin12, a12 + 1);
                int values = end24 - a24 - 4;
                Assert.Equal(a12 + 4 + (values + 1) / 2, end12);
                for (int v = 0; v < values; v++)
                {
                    int expected = 15 * bin24[a24 + 5 + v] / 255;
                    byte packed = bin12[a12 + 5 + v / 2];
                    Assert.Equal(expected, v % 2 == 0 ? packed >> 4 : packed & 0x0f);
                }
                if (values % 2 == 1) Assert.Equal(0, bin12[end12] & 0x0f);
            }
        }

        // Images follow each other with no gap up to the end of the file.
        int address = (int)imagesStart;
        while (address < bin12.Length)
        {
            Assert.Equal(4, bin12[address]);
            address = (int)System.BitConverter.ToUInt32(bin12, address + 1) + 1;
        }
        Assert.Equal(bin12.Length, address);
    }

    /// <summary>PixelTuner 2's blue table stopped at 254 and failed on full blue.</summary>
    [Fact]
    public void Full_blue_at_36_bits_takes_the_completed_table_entry()
    {
        string bmp = Path.Combine(_folder, "blue.bmp");
        var bytes = new byte[58];
        bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
        System.BitConverter.GetBytes(58).CopyTo(bytes, 2);
        System.BitConverter.GetBytes(54).CopyTo(bytes, 10);
        System.BitConverter.GetBytes(40).CopyTo(bytes, 14);
        System.BitConverter.GetBytes(1).CopyTo(bytes, 18);
        System.BitConverter.GetBytes(1).CopyTo(bytes, 22);
        System.BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        System.BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        bytes[54] = 255;   // B, G, R
        File.WriteAllBytes(bmp, bytes);
        var lines = new List<LineSpec> { new((0, 0, 255), [new ItemSpec(bmp, 0, 1000, 1, 100, false)]) };
        var document = PtpDocument.Load(new MemoryStream(Encoding.UTF8.GetBytes(ToPtp(lines))));
        byte[] bin = new PtpEpicExport(path => File.Exists(path) ? path : null) { Depth = StudioDepth.Bit36 }
            .Export(PtpEpicExport.EpicTimelines(document));
        int image = (int)System.BitConverter.ToUInt32(bin, 12 + 4 + 12);
        Assert.Equal(12, bin[image]);
        Assert.Equal(0, System.BitConverter.ToUInt16(bin, image + 5));
        Assert.Equal(0, System.BitConverter.ToUInt16(bin, image + 7));
        Assert.Equal(3850, System.BitConverter.ToUInt16(bin, image + 9));
    }

    private sealed record LineSpec((byte R, byte G, byte B) Color, List<ItemSpec> Items);

    private sealed record ItemSpec(string Path, int Start, int End, int Stretching, byte Brightness, bool Laser);

    private LineSpec RandomLine(Random random, List<string> files)
    {
        var items = new List<ItemSpec>();
        int time = random.Next(3) == 0 ? 0 : random.Next(1, 3000);
        int count = random.Next(1, 8);
        for (int i = 0; i < count; i++)
        {
            int length = random.Next(1, 5000);
            byte brightness = random.Next(3) == 0 ? (byte)100 : (byte)random.Next(0, 101);
            items.Add(new ItemSpec(files[random.Next(files.Count)], time, time + length,
                random.Next(1, 20), brightness, random.Next(4) == 0));
            time += length;
            if (random.Next(3) == 0) time += random.Next(1, 2000);
        }
        return new LineSpec(((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)), items);
    }

    /// <summary>A 24-bit BMP of random colours, 1 to 3 rows, 1 to 40 columns.</summary>
    private string WriteBmp(Random random, string name, byte max)
    {
        int width = random.Next(1, 41), height = random.Next(1, 4);
        int stride = (width * 3 + 3) & ~3;
        var bmp = new byte[54 + stride * height];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        System.BitConverter.GetBytes(bmp.Length).CopyTo(bmp, 2);
        System.BitConverter.GetBytes(54).CopyTo(bmp, 10);
        System.BitConverter.GetBytes(40).CopyTo(bmp, 14);
        System.BitConverter.GetBytes(width).CopyTo(bmp, 18);
        System.BitConverter.GetBytes(height).CopyTo(bmp, 22);
        System.BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        System.BitConverter.GetBytes((short)24).CopyTo(bmp, 28);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width * 3; x++)
                bmp[54 + y * stride + x] = (byte)(random.Next(5) == 0 ? max : random.Next(max + 1));
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bmp);
        return path;
    }

    private static string Time(int ms) => TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    private static string ToPtp(List<LineSpec> lines)
    {
        var xml = new StringBuilder();
        xml.AppendLine("""<?xml version="1.0"?>""");
        xml.AppendLine("""<FileViewModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">""");
        foreach (var line in lines)
        {
            xml.AppendLine($"""  <Line xsi:type="EpicPoiTimeLineViewModel" LineColorTagXml="#FF{line.Color.R:X2}{line.Color.G:X2}{line.Color.B:X2}">""");
            foreach (var item in line.Items)
                xml.AppendLine($"""    <Item xsi:type="TimeLineItemGradient" LaserOn="{(item.Laser ? "true" : "false")}" Start="{Time(item.Start)}" End="{Time(item.End)}" FullName="{item.Path}" Stretching="{item.Stretching}" Brightness="{item.Brightness}" />""");
            xml.AppendLine("  </Line>");
        }
        xml.AppendLine("</FileViewModel>");
        return xml.ToString();
    }

    /// <summary>FileViewModel.CreateEpicProjectFile and GetEpicBinaryPresentation.</summary>
    private static byte[] Legacy(List<LineSpec> lines, LegacyDepth depth)
    {
        var gradients = new List<GradientsCollectionItem>();
        var project = new GradientProjectFile();
        foreach (var line in lines)
        {
            var file = new GradientTimeLineFile
            {
                TimelineColor = System.Drawing.Color.FromArgb(255, line.Color.R, line.Color.G, line.Color.B),
            };
            var items = new List<ItemSpec>();
            if (line.Items[0].Start != 0) items.Add(Blank(0, line.Items[0].Start));
            for (int i = 0; i < line.Items.Count; i++)
            {
                items.Add(line.Items[i]);
                if (i + 1 < line.Items.Count && line.Items[i].End != line.Items[i + 1].Start)
                    items.Add(Blank(line.Items[i].End, line.Items[i + 1].Start));
            }
            foreach (var item in items)
            {
                // After FillGabs the items touch, so CreateEpicProjectFile's own gap branch never ran.
                string path = item.Path.ToLower();
                uint mask = item.Laser ? 1u : 0u;
                var imageFile = new GradientImageFile(path, (ushort)item.Stretching, (uint)(item.End - item.Start),
                    (uint)item.Start, (ushort)mask);
                var image = new GradientsCollectionItem(path, 1, depth);
                imageFile.Brightness = image.Brightness = item.Brightness;
                gradients.Add(image);
                file.Images.Add(imageFile);
            }
            project.TimeLines.Add(file);
        }
        return [.. project.GetBinary(new GradientsCollection(gradients))];
    }

    /// <summary>CreateBlankGradientItem: no file, default stretching and brightness.</summary>
    private static ItemSpec Blank(int start, int end) => new("", start, end, DefaultStretching, 100, false);
}
