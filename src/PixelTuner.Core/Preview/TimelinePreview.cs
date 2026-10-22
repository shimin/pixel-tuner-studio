using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Layout;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Preview;

/// <summary>A picture block of the .bin as the device lights it: columns of LEDs, in 8-bit colours.</summary>
public sealed class PreviewPicture(int width, int height, Rgb[] pixels)
{
    public int Width { get; } = width;
    public int Height { get; } = height;

    public Rgb At(int column, int row) => pixels[column * Height + row];

    /// <summary>One column: the colours of the LEDs from the first to the last.</summary>
    public Rgb[] Column(int column) => pixels.AsSpan(column * Height, Height).ToArray();
}

/// <summary>What the LEDs of a device show at one moment of a timeline.</summary>
/// <param name="Entry">Index of the entry that plays (pauses are entries), or -1 when nothing plays: before the first one and after the end.</param>
/// <param name="Column">The column of the picture that is lit now; for light, always 0.</param>
/// <param name="ColumnTime">How long the device holds a column; zero for light.</param>
/// <param name="Colors">The LEDs of the column from the first to the last; for light, the colours of the slots.</param>
/// <param name="Picture">The picture the entry plays, whole (for drawing what the device paints as it moves); null for light or when nothing plays.</param>
public sealed record PreviewFrame(TimeSpan Time, int Entry, int Column, TimeSpan ColumnTime, IReadOnlyList<Rgb> Colors, PreviewPicture? Picture, bool Laser)
{
    public bool IsPlaying => Entry >= 0;

    /// <summary>How long the picture takes to play once.</summary>
    public TimeSpan PassTime => Picture is null ? TimeSpan.Zero : ColumnTime * Picture.Width;
}

/// <summary>
/// What a device plays from one timeline, read back from the .bin the build makes for it (the preview shows what the device will get,
/// pixel for byte, after brightness and colour correction), at any time of the show and at the device's own speed: a pixel poi holds a column
/// for k × 50 µs, a video or fan module for k ms, an E-Pic poi for k / 5 ms, and a picture plays again from its first column until the next entry.
/// </summary>
public sealed class TimelinePreview
{
    // What one step of the stretching in the file is, in ticks (100 ns): 50 µs on a pixel device, 1 ms on a video or fan module. An E-Pic
    // poi is written as 5 times what the editor shows, and the editor's k is k / 5 ms (see ItemPreview.EpicPassDuration), so a step is 0.04 ms.
    private const long PixelTicks = 500, MillisecondTicks = 10_000, EpicTicks = 400;

    private readonly PpoiTimeline? _line;
    private readonly LghtFile? _light;
    private readonly long _stepTicks;
    private readonly Dictionary<uint, PreviewPicture> _pictures = [];

    private TimelinePreview(string deviceType, int height, TimeSpan duration, PpoiTimeline? line, LghtFile? light)
    {
        DeviceType = deviceType;
        Height = height;
        Duration = duration;
        _line = line;
        _light = light;
        _stepTicks = deviceType switch
        {
            DeviceKind.Video or DeviceKind.Fan => MillisecondTicks,
            DeviceKind.EpicPoi => EpicTicks,
            _ => PixelTicks,
        };
    }

    /// <summary>One of <see cref="DeviceKind"/>.</summary>
    public string DeviceType { get; }

    /// <summary>LEDs of a column; for light, the slots of the sample.</summary>
    public int Height { get; }

    public TimeSpan Duration { get; }

    public bool IsLight => _light is not null;

    /// <summary>The entries of the timeline in the file, for tests and for the views that mark where they start.</summary>
    public IReadOnlyList<PpoiEntry> Entries => _line?.Entries ?? [];

    /// <summary>
    /// Builds the file for the device that plays the timeline and reads it back; null when the build refuses (the issues say why).
    /// The device is the first one of the project that lists the timeline, or one made up for its kind.
    /// </summary>
    public static TimelinePreview? Build(ShowProject project, ShowMedia? media, Timeline timeline, out IReadOnlyList<BuildIssue> issues)
    {
        var device = BinInfo.DeviceFor(project, timeline);
        var result = new ShowBuilder(project, media).Build(device);
        issues = result.Issues;
        return result.Bin is { } bin ? FromBin(bin, device.DeviceType) : null;
    }

    /// <summary>Plays a .bin of a device of this kind. A video pair is one timeline in the file and plays as one (both chains in a column).</summary>
    public static TimelinePreview? FromBin(byte[] bin, string deviceType)
    {
        if (BinReader.IsLght(bin))
        {
            var light = BinReader.ReadLght(bin);
            return light.Samples.Count == 0 ? null : new TimelinePreview(deviceType, light.Slots, TimeSpan.FromMilliseconds(light.DurationMs), null, light);
        }
        if (!BinReader.IsPpoi(bin)) return null;
        var file = BinReader.ReadPpoi(bin);
        if (file.Timelines.Count == 0) return null;
        var line = file.Timelines[0];
        return new TimelinePreview(deviceType, file.Height, TimeSpan.FromMilliseconds(line.DurationMs), line, null);
    }

    /// <summary>What the LEDs show at a time of the show.</summary>
    public PreviewFrame At(TimeSpan time)
    {
        if (_light is { } light)
        {
            var sample = light.At((uint)Math.Max(0, time.TotalMilliseconds));
            var colors = Enumerable.Range(0, light.Slots).Select(sample.Slot).ToArray();
            return new PreviewFrame(time, time < Duration ? 0 : -1, 0, TimeSpan.Zero, colors, null, false);
        }

        var entries = _line!.Entries;
        double ms = time.TotalMilliseconds;
        int index = -1;
        for (int i = 0; i < entries.Count && entries[i].StartMs <= ms; i++) index = i;
        if (index < 0 || time >= Duration) return new PreviewFrame(time, -1, 0, TimeSpan.Zero, new Rgb[Height], null, false);

        var entry = entries[index];
        var picture = Picture(entry.Image);
        long columnTicks = _stepTicks * Math.Max(1, (int)entry.Stretching);
        long column = (time - TimeSpan.FromMilliseconds(entry.StartMs)).Ticks / columnTicks;
        int shown = picture.Width == 0 ? 0 : (int)(column % picture.Width);
        var colours = picture.Width == 0 ? new Rgb[Height] : picture.Column(shown);
        return new PreviewFrame(time, index, shown, TimeSpan.FromTicks(columnTicks), colours, picture, entry.LaserOn);
    }

    private PreviewPicture Picture(PpoiImage image)
    {
        if (_pictures.TryGetValue(image.Address, out var known)) return known;
        int width = image.Width, height = image.Height;
        var pixels = new Rgb[width * height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                pixels[x * height + y] = new Rgb(Channel(image, x, y, 0), Channel(image, x, y, 1), Channel(image, x, y, 2));
        return _pictures[image.Address] = new PreviewPicture(width, height, pixels);
    }

    // The device's value of a channel as the 8 bits a screen shows: 4 and 12 bit values are spread over 0..255.
    private static byte Channel(PpoiImage image, int column, int row, int channel)
    {
        int value = image.Value(column, row, channel);
        return image.BitsPerChannel switch
        {
            8 => (byte)value,
            12 => (byte)((value * 255 + 2047) / 4095),
            _ => (byte)(value * 17),
        };
    }

    /// <summary>The positions of the LEDs on the grid of a video layout, in wiring order, as the video export samples them.</summary>
    public static IReadOnlyList<(int X, int Y)> Positions(LedLayout layout) => VideoExport.Positions(layout);
}
