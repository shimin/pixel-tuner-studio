using System.Collections.Concurrent;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Export;

/// <summary>
/// The colour correction of the export read backwards: for a value stored in a block, the level (0 to 255) of the picture that the export
/// turned into it. The export corrects every colour for the LEDs (the WS2812 table of a video module, the gamma of APA102, the tables of
/// the 3528 LEDs), so the stored values of a bright picture are small numbers; a screen shows the picture, not the numbers.
/// The correction is never worked out by hand here: it is <see cref="PixelColor"/> and <see cref="Ws2812Profile"/> themselves, run on every
/// level of grey, and a stored value takes the levels that give it (the middle of them where several do).
/// </summary>
internal static class LedCorrection
{
    private static readonly ConcurrentDictionary<(bool Video, ushort Height, int Bits), byte[][]> Tables = new();

    /// <summary>True for the kinds of device whose blocks go through the correction; E-Pic and LightMaster colours are stored as they are.</summary>
    public static bool Corrects(string deviceType) => deviceType is DeviceKind.Poi or DeviceKind.Hoop or DeviceKind.Fan or DeviceKind.Video;

    /// <summary>For each channel (red, green, blue), the level of the picture for every value that can be stored at this depth.</summary>
    public static byte[][] For(string deviceType, int height, int bitsPerChannel) =>
        Tables.GetOrAdd((deviceType == DeviceKind.Video, (ushort)height, bitsPerChannel), Build);

    private static byte[][] Build((bool Video, ushort Height, int Bits) key)
    {
        var pixel = new PixelColor(key.Height, 100);
        int size = key.Bits switch { 4 => 16, 8 => 256, _ => 4096 };
        var tables = new byte[3][];
        for (int channel = 0; channel < 3; channel++)
        {
            var stored = new int[256];
            for (int level = 0; level < 256; level++)
            {
                var grey = new Rgb((byte)level, (byte)level, (byte)level);
                if (key.Video) grey = Ws2812Profile.Modify(grey);
                stored[level] = key.Bits switch { 4 => pixel.To4Bit(grey, channel), 8 => pixel.To8Bit(grey, channel), _ => pixel.To12Bit(grey, channel) };
            }

            var table = tables[channel] = new byte[size];
            for (int value = 1; value < size; value++)
            {
                int nearest = int.MaxValue, sum = 0, count = 0;
                for (int level = 0; level < 256; level++)
                {
                    int distance = Math.Abs(stored[level] - value);
                    if (distance < nearest) { nearest = distance; sum = level; count = 1; }
                    else if (distance == nearest) { sum += level; count++; }
                }
                table[value] = (byte)(sum / count);
            }
            // Dark stays dark: the levels that give 0 are a whole stretch, and its middle is not black.
        }
        return tables;
    }
}
