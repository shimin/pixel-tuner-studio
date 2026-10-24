namespace PixelTuner.Core.Imaging;

/// <summary>
/// How an image looks on a device, for the editor's preview: brightness as PixelTuner 2 applied it
/// (a float factor, truncated), each column repeated by <c>stretching</c> pixels wide, flipped when the timeline is.
/// On the device the stretching is not a width but a speed (see <see cref="PassDuration"/> and <see cref="VideoPassDuration"/>); the preview in the
/// inspector draws the picture itself, one pixel per column.
/// An illustration of the picture, not the exact bytes the device receives.
/// </summary>
public static class ItemPreview
{
    // TODO(release): the units of the three stretching formulas below are not checked on real devices, see docs/before-release.md.

    /// <summary>What one step of the stretching is on a pixel device: it holds a column for this many 50 µs intervals.</summary>
    public static readonly TimeSpan StretchingUnit = TimeSpan.FromTicks(500);

    /// <summary>How long a picture takes to play once: every column is held for <paramref name="stretching"/> intervals.</summary>
    public static TimeSpan PassDuration(int width, int stretching) =>
        TimeSpan.FromTicks(StretchingUnit.Ticks * Math.Max(0, width) * Math.Max(1, stretching));

    /// <summary>
    /// How long a gradient picture takes to play once on an E-pic poi. Copied from PixelTuner 2's "Repeat" window
    /// (<c>RepeatItemWindow</c>): <c>K = milliseconds per column * 5</c>, so a column is held for k / 5 ms.
    /// (PixelTuner 2 writes k * 5 into the .bin for E-Pic, and that is what the device receives.)
    /// </summary>
    public static TimeSpan EpicPassDuration(int width, int stretching) =>
        TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond * Math.Max(0, width) * Math.Max(1, stretching) / 5);

    /// <summary>
    /// How long an animation takes to play once on a video device: its stretching is the time of one frame
    /// (one column of the device image) in milliseconds, so 25 frames a second is 40.
    /// </summary>
    public static TimeSpan VideoPassDuration(int frames, int frameMilliseconds) =>
        TimeSpan.FromMilliseconds((double)Math.Max(0, frames) * Math.Max(1, frameMilliseconds));

    /// <summary>How many times the picture plays in an item of this length (a fraction when the last pass is cut off).</summary>
    public static double Repeats(TimeSpan itemDuration, int width, int stretching) => Repeats(itemDuration, PassDuration(width, stretching));

    public static double Repeats(TimeSpan itemDuration, TimeSpan pass) => pass <= TimeSpan.Zero ? 0 : itemDuration / pass;

    public static PixelImage Render(PixelImage source, byte brightness, int stretching, bool flipVertical)
    {
        stretching = Math.Max(1, stretching);
        float factor = (float)Math.Min(brightness, (byte)100) / 100;
        var result = new PixelImage(checked(source.Width * stretching), source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            int sourceRow = flipVertical ? source.Height - 1 - y : y;
            for (int x = 0; x < source.Width; x++)
            {
                var c = source[x, sourceRow];
                var scaled = new Rgb((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));
                for (int k = 0; k < stretching; k++) result[x * stretching + k, y] = scaled;
            }
        }
        return result;
    }
}
