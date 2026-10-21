namespace PixelTuner.Core.Imaging;

/// <summary>
/// How an image looks on a device, for the editor's preview: brightness as PixelTuner 2 applied it
/// (a float factor, truncated), each column repeated by the stretching, flipped when the timeline is.
/// An illustration of the picture, not the exact bytes the device receives.
/// </summary>
public static class ItemPreview
{
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
