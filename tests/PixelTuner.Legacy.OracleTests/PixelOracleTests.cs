using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Communication.Pixelate;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using Xunit;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using LegacyDepth = Communication.ColorDepth;
using StudioDepth = PixelTuner.Core.Export.ColorDepth;

namespace PixelTuner.Legacy.OracleTests;

/// <summary>
/// Pixel, fan and video .bin: Studio's port against PixelTuner 2's Communication/Pixelate on
/// random shows. The glue of FileViewModel (CreatePixelProjectFile, CreateFanProjectFile, both
/// CreateVideoProjectFile, FillGabs, SplitItems, GetSyncedIntervals and the blank items) is
/// repeated here as it was.
/// </summary>
public sealed class PixelOracleTests : IDisposable
{
    private const int DefaultStretching = 4;
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
        "PixelTuner.Core.Tests", "Fixtures", "LegacyProjects", "glasses");

    // Lower case: PixelTuner 2 lower-cased item paths before it opened them.
    private readonly string _folder = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "pixel-oracle-" + Guid.NewGuid().ToString("N"))).FullName;

    private int _blankCount;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // Heights pick the colour paths of PixelTuner 2: 80 applies brightness twice, above 150
    // the APA102 gamma, from 100 the 3528 table at 36 bits; odd heights split a 12-bit byte.
    [Theory]
    [InlineData(1, 25, StudioDepth.Bit24)]
    [InlineData(2, 36, StudioDepth.Bit24)]
    [InlineData(3, 80, StudioDepth.Bit24)]
    [InlineData(4, 125, StudioDepth.Bit24)]
    [InlineData(5, 160, StudioDepth.Bit24)]
    [InlineData(6, 300, StudioDepth.Bit24)]
    [InlineData(7, 24, StudioDepth.Bit36)]
    [InlineData(8, 80, StudioDepth.Bit36)]
    [InlineData(9, 120, StudioDepth.Bit36)]
    [InlineData(10, 24, StudioDepth.Bit12)]
    [InlineData(11, 80, StudioDepth.Bit12)]
    [InlineData(12, 25, StudioDepth.Bit12)]
    [InlineData(13, 1, StudioDepth.Bit12)]
    [InlineData(14, 151, StudioDepth.Bit12)]
    public void Random_pixel_show_matches_PixelTuner_2(int seed, int height, StudioDepth depth)
    {
        var random = new Random(seed);
        var files = Enumerable.Range(0, 6).Select(i => WriteBmp(random, $"p{i}.bmp", RandomWidth(random), height)).ToList();
        // A picture lower and one taller than the device: aligned to the bottom LED.
        files.Add(WriteBmp(random, "short.bmp", RandomWidth(random), Math.Max(1, height - 3)));
        files.Add(WriteBmp(random, "tall.bmp", RandomWidth(random), height + 2));

        var lines = Enumerable.Range(0, random.Next(1, 5)).Select(_ => RandomLine(random, files)).ToList();
        foreach (var line in lines) line.Items[0] = line.Items[0] with { Path = files[0] };   // the timeline's height

        var timelines = lines.Select(ToTimeline).ToList();
        byte[] actual = new PixelExport(ShowMedia.FromFiles()) { Depth = depth, GapStretching = DefaultStretching }
            .Export(timelines, (ushort)height);

        byte[] expected = LegacyPixel(lines, (ushort)height, Legacy(depth));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 36, StudioDepth.Bit24)]
    [InlineData(2, 36, StudioDepth.Bit24)]
    [InlineData(3, 40, StudioDepth.Bit24)]
    [InlineData(4, 31, StudioDepth.Bit12)]
    [InlineData(5, 36, StudioDepth.Bit36)]
    public void Random_fan_show_matches_PixelTuner_2(int seed, int height, StudioDepth depth)
    {
        var random = new Random(seed);
        var files = Enumerable.Range(0, 4).Select(i => WriteBmp(random, $"f{i}.bmp", RandomWidth(random), height)).ToList();
        var line = RandomLine(random, files);

        byte[] actual = new PixelExport(ShowMedia.FromFiles()) { Depth = depth, GapStretching = DefaultStretching }
            .ExportFan(ToTimeline(line));

        byte[] expected = LegacyFan(line, (ushort)height, Legacy(depth));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, StudioDepth.Bit24)]
    [InlineData(2, StudioDepth.Bit24)]
    [InlineData(3, StudioDepth.Bit24)]
    [InlineData(4, StudioDepth.Bit24)]
    [InlineData(5, StudioDepth.Bit36)]
    [InlineData(6, StudioDepth.Bit12)]
    public void Random_video_show_matches_PixelTuner_2(int seed, StudioDepth depth)
    {
        var random = new Random(seed);
        var gifs = CopyGifs();
        var layout = RandomLayout(random);
        var line = RandomVideoLine(random, gifs, end: null);

        byte[] actual = Studio([(line, layout)], depth);
        byte[] expected = LegacyVideo(line, Points(layout), Legacy(depth));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, StudioDepth.Bit24)]
    [InlineData(2, StudioDepth.Bit24)]
    [InlineData(3, StudioDepth.Bit24)]
    [InlineData(4, StudioDepth.Bit24)]
    [InlineData(5, StudioDepth.Bit36)]
    [InlineData(6, StudioDepth.Bit12)]
    [InlineData(7, StudioDepth.Bit24)]
    [InlineData(8, StudioDepth.Bit24)]
    [InlineData(9, StudioDepth.Bit24)]
    [InlineData(10, StudioDepth.Bit24)]
    [InlineData(11, StudioDepth.Bit24)]
    [InlineData(12, StudioDepth.Bit24)]
    public void Random_video_pair_matches_PixelTuner_2(int seed, StudioDepth depth)
    {
        var random = new Random(seed);
        var gifs = CopyGifs();
        var layout1 = RandomLayout(random);
        var layout2 = RandomLayout(random);
        var line1 = RandomVideoLine(random, gifs, end: null);
        // PixelTuner 2 needed both timelines to end at the same time.
        var line2 = RandomVideoLine(random, gifs, end: line1[^1].End);

        byte[] actual = Studio([(line1, layout1), (line2, layout2)], depth);
        byte[] expected = LegacyVideoPair(line1, line2, Points(layout1), Points(layout2), Legacy(depth));
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// A pause on the first timeline that the second one cuts in two: PixelTuner 2 cloned the
    /// pause item for each piece, so the pieces shared one picture (the first piece's).
    /// </summary>
    [Fact]
    public void Video_pair_with_a_pause_cut_by_the_other_timeline_matches_PixelTuner_2()
    {
        var gifs = CopyGifs();
        var random = new Random(1);
        var layout1 = RandomLayout(random);
        var layout2 = RandomLayout(random);
        List<ItemSpec> line1 = [new(gifs[0], 1000, 3000, 3, 80, false)];
        List<ItemSpec> line2 = [new(gifs[1], 0, 500, 2, 60, false), new(gifs[2], 500, 3000, 5, 100, true)];

        byte[] actual = Studio([(line1, layout1), (line2, layout2)], StudioDepth.Bit24);
        byte[] expected = LegacyVideoPair(line1, line2, Points(layout1), Points(layout2), LegacyDepth.Bit24);
        Assert.Equal(expected, actual);
    }

    private sealed record LineSpec((byte R, byte G, byte B) Color, bool Flip, List<ItemSpec> Items);

    private sealed record ItemSpec(string Path, int Start, int End, int Stretching, byte Brightness, bool Laser,
        int X = 0, int Y = 0);

    private static LegacyDepth Legacy(StudioDepth depth) =>
        (LegacyDepth)Enum.Parse(typeof(LegacyDepth), depth.ToString());

    private static int RandomWidth(Random random) => random.Next(3) == 0 ? 1 : random.Next(1, 30);

    private static LineSpec RandomLine(Random random, List<string> files)
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
        var color = ((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        return new LineSpec(color, random.Next(3) == 0, items);
    }

    private static Timeline ToTimeline(LineSpec line) => new()
    {
        Kind = TimelineKind.Pixel,
        Color = $"#FF{line.Color.R:X2}{line.Color.G:X2}{line.Color.B:X2}",
        FlipVertical = line.Flip,
        Items = line.Items.Select(i => new TimelineItem
        {
            Kind = ItemKind.Image,
            Start = TimeSpan.FromMilliseconds(i.Start),
            Duration = TimeSpan.FromMilliseconds(i.End - i.Start),
            ResourceId = i.Path,
            SourcePath = i.Path,
            Stretching = i.Stretching,
            Brightness = i.Brightness,
            LaserOn = i.Laser,
        }).ToList(),
    };

    /// <summary>A 24-bit BMP of random colours (or black).</summary>
    private string WriteBmp(Random random, string name, int width, int height, bool black = false)
    {
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
        if (!black)
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width * 3; x++)
                    bmp[54 + y * stride + x] = (byte)(random.Next(5) == 0 ? 255 : random.Next(256));
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bmp);
        return path;
    }

    /// <summary>
    /// TimeLineItemViewModel.ReplaceWithBlank: a new black bitmap, 10 columns wide, saved to
    /// a temporary file of its own, with the default stretching and brightness.
    /// </summary>
    private ItemSpec BlankPixel(int start, int end, int height) =>
        new(WriteBmp(null, $"blank{_blankCount++}.bmp", 10, height, black: true), start, end, DefaultStretching, 100, false);

    /// <summary>FileViewModel.FillGabs: blanks before the first item and between items.</summary>
    private static List<ItemSpec> FillGaps(List<ItemSpec> items, Func<int, int, ItemSpec> blank)
    {
        var result = new List<ItemSpec>();
        if (items[0].Start != 0) result.Add(blank(0, items[0].Start));
        for (int i = 0; i < items.Count; i++)
        {
            result.Add(items[i]);
            if (i + 1 < items.Count && items[i].End != items[i + 1].Start)
                result.Add(blank(items[i].End, items[i + 1].Start));
        }
        return result;
    }

    /// <summary>FileViewModel.CreatePixelProjectFile and GetPixelBinaryPresentation.</summary>
    private byte[] LegacyPixel(List<LineSpec> lines, ushort height, LegacyDepth depth)
    {
        var images = new List<ImagesCollectionItem>();
        var project = new PixelProjectFile();
        foreach (var line in lines)
        {
            var file = new PixelTimelineFile
            {
                TimelineColor = DrawingColor.FromArgb(255, line.Color.R / 10, line.Color.G / 10, line.Color.B / 10),
            };
            foreach (var item in FillGaps(line.Items, (s, e) => BlankPixel(s, e, height)))
            {
                string path = item.Path.ToLower();
                ushort mask = item.Laser ? (ushort)0xffff : (ushort)0;
                var imageFile = new PixelImageFile(path, (ushort)item.Stretching, (uint)(item.End - item.Start),
                    (uint)item.Start, mask, line.Flip);
                var image = new ImagesCollectionItem(path, height, depth, item.Brightness, line.Flip);
                imageFile.Brightness = item.Brightness;
                images.Add(image);
                file.Images.Add(imageFile);
            }
            project.TimeLines.Add(file);
        }
        return [.. project.GetBinary(new ImagesCollection(images), height)];
    }

    /// <summary>
    /// FileViewModel.CreateFanProjectFile and GetFanBinaryPresentation: the default menu
    /// colour, blanks six times the line height.
    /// </summary>
    private byte[] LegacyFan(LineSpec line, ushort height, LegacyDepth depth)
    {
        var images = new List<ImagesCollectionItem>();
        var project = new PixelProjectFile();
        var file = new PixelTimelineFile();
        foreach (var item in FillGaps(line.Items, (s, e) => BlankPixel(s, e, height * 6)))
        {
            string path = item.Path.ToLower();
            ushort mask = item.Laser ? (ushort)0xffff : (ushort)0;
            var imageFile = new PixelImageFile(path, (ushort)item.Stretching, (uint)(item.End - item.Start),
                (uint)item.Start, mask, line.Flip);
            var image = new ImagesCollectionItem(path, height, depth, item.Brightness, line.Flip);
            imageFile.Brightness = image.Brightness = item.Brightness;
            images.Add(image);
            file.Images.Add(imageFile);
        }
        project.TimeLines.Add(file);
        return [.. project.GetBinary(new ImagesCollection(images), height)];
    }

    // ---- Video ----

    private List<string> CopyGifs()
    {
        var result = new List<string>();
        foreach (var source in Directory.GetFiles(Fixtures, "*.gif").Order(StringComparer.Ordinal))
        {
            string path = Path.Combine(_folder, Path.GetFileName(source).ToLowerInvariant().Replace(' ', '-'));
            File.Copy(source, path, overwrite: true);
            result.Add(path);
        }
        result.Add(Path.Combine(_folder, "missing.gif"));
        return result;
    }

    private static LedLayout RandomLayout(Random random)
    {
        int columns = random.Next(1, 45), rows = random.Next(1, 25);
        var tiles = Enumerable.Range(0, columns * rows).OrderBy(_ => random.Next()).ToList();
        int count = random.Next(1, Math.Min(tiles.Count, 200) + 1);
        return new LedLayout { Columns = columns, Rows = rows, Leds = tiles.Take(count).ToList() };
    }

    private static List<DrawingPoint> Points(LedLayout layout) =>
        layout.Leds.Select(i => new DrawingPoint(i % layout.Columns, i / layout.Columns)).ToList();

    private static List<ItemSpec> RandomVideoLine(Random random, List<string> gifs, int? end)
    {
        var items = new List<ItemSpec>();
        int time = random.Next(3) == 0 ? 0 : random.Next(1, 3000);
        int count = random.Next(1, 6);
        for (int i = 0; i < count; i++)
        {
            int length = random.Next(1, 5000);
            byte brightness = random.Next(3) == 0 ? (byte)100 : (byte)random.Next(0, 101);
            items.Add(new ItemSpec(gifs[random.Next(gifs.Count)], time, time + length, random.Next(1, 20), brightness,
                random.Next(4) == 0, random.Next(-5, 10), random.Next(-5, 10)));
            time += length;
            if (random.Next(3) == 0) time += random.Next(1, 2000);
        }
        if (end is { } last)
        {
            // Stretch or cut the line so that its last item ends at the given time.
            while (items.Count > 1 && items[^1].Start >= last) items.RemoveAt(items.Count - 1);
            if (items[^1].Start >= last) items[^1] = items[^1] with { Start = 0 };
            items[^1] = items[^1] with { End = last };
        }
        return items;
    }

    private byte[] Studio(List<(List<ItemSpec> Items, LedLayout Layout)> lines, StudioDepth depth)
    {
        var timelines = lines.Select(l => new Timeline
        {
            Kind = TimelineKind.Video,
            LedLayoutId = l.Layout.Id,
            Items = l.Items.Select(i => new TimelineItem
            {
                Kind = ItemKind.Video,
                Start = TimeSpan.FromMilliseconds(i.Start),
                Duration = TimeSpan.FromMilliseconds(i.End - i.Start),
                ResourceId = File.Exists(i.Path) ? i.Path : null,
                SourcePath = i.Path,
                Stretching = i.Stretching,
                Brightness = i.Brightness,
                LaserOn = i.Laser,
                OffsetX = i.X,
                OffsetY = i.Y,
            }).ToList(),
        }).ToList();
        var layouts = lines.Select(l => l.Layout).ToList();
        return new VideoExport(id => layouts.FirstOrDefault(l => l.Id == id), ShowMedia.FromFiles())
            { Depth = depth, GapStretching = DefaultStretching }.Export(timelines);
    }

    /// <summary>
    /// VideoThumbnailViewModel.ReplaceWithBlank: an empty one-frame picture in a temporary file
    /// of its own, with the default stretching and brightness and no offset.
    /// </summary>
    private ItemSpec BlankVideo(int start, int end) =>
        new(WriteBmp(null, $"vblank{_blankCount++}.bmp", 50, 4, black: true), start, end, DefaultStretching, 100, false);

    /// <summary>FileViewModel.CreateVideoProjectFile for one timeline.</summary>
    private byte[] LegacyVideo(List<ItemSpec> items, List<DrawingPoint> leds, LegacyDepth depth)
    {
        var images = new List<ImagesCollectionItem>();
        var project = new PixelProjectFile();
        var file = new PixelTimelineFile { TimeLineHeight = (ushort)leds.Count };
        foreach (var item in FillGaps(items, BlankVideo))
        {
            string path = item.Path.ToLower();
            ushort mask = item.Laser ? (ushort)1 : (ushort)0;
            var imageFile = new VideoImageFile(path, (ushort)item.Stretching, (uint)(item.End - item.Start),
                (uint)item.Start, mask, false);
            var image = new ImagesCollectionVideoItem(path, leds, depth, item.X, item.Y, item.Brightness);
            imageFile.Brightness = image.Brightness = item.Brightness;
            images.Add(image);
            file.Images.Add(imageFile);
        }
        project.TimeLines.Add(file);
        return [.. project.GetBinary(new ImagesCollection(images))];
    }

    /// <summary>FileViewModel.CreateVideoProjectFile for two timelines, with GetSyncedIntervals and SplitItems.</summary>
    private byte[] LegacyVideoPair(List<ItemSpec> items1, List<ItemSpec> items2,
        List<DrawingPoint> leds1, List<DrawingPoint> leds2, LegacyDepth depth)
    {
        var images = new List<ImagesCollectionItem>();
        var project = new PixelProjectFile();
        var file = new PixelTimelineFile { TimeLineHeight = (ushort)(leds1.Count + leds2.Count) };

        var filled1 = FillGaps(items1, BlankVideo);
        var filled2 = FillGaps(items2, BlankVideo);
        var intervals = filled1.Select(i => i.End).Union(filled2.Select(i => i.End)).Order().ToList();
        var split1 = Split(filled1, intervals);
        var split2 = Split(filled2, intervals);

        for (int i = 0; i < intervals.Count; i++)
        {
            var first = split1[i];
            var second = split2[i];
            string path1 = first.Path.ToLower();
            string path2 = second.Path.ToLower();
            ushort mask = first.Laser ? (ushort)1 : (ushort)0;
            var imageFile = new VideoImageFile(path1, (ushort)first.Stretching, (uint)(first.End - first.Start),
                (uint)first.Start, mask, false);
            var image = new ImagesCollectionVideoDoubleItem(path1, path2, leds1, leds2,
                first.X, first.Y, second.X, second.Y, depth, first.Brightness, second.Brightness);
            imageFile.Brightness = image.Brightness = first.Brightness;
            images.Add(image);
            file.Images.Add(imageFile);
        }
        project.TimeLines.Add(file);
        return [.. project.GetBinary(new ImagesCollection(images))];
    }

    /// <summary>FileViewModel.SplitItems.</summary>
    private static List<ItemSpec> Split(List<ItemSpec> items, List<int> intervals)
    {
        var result = new List<ItemSpec>();
        foreach (var item in items)
        {
            foreach (int end in intervals)
            {
                if (end > item.Start && end <= item.End) result.Add(item);
                if (end > item.End) break;
            }
        }
        for (int i = 0; i < intervals.Count - 1; i++)
        {
            result[i] = result[i] with { End = intervals[i] };
            result[i + 1] = result[i + 1] with { Start = intervals[i] };
        }
        return result;
    }
}
