using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Export;

/// <summary>
/// Colour correction per device height and colour depth, exactly as PixelTuner 2 did it.
/// The float and double arithmetic is kept as it was on purpose: changing it changes
/// rounding, and then the .bin no longer matches what devices were tuned for.
/// </summary>
internal readonly struct PixelColor
{
    private const float Gamma = 2.8f;

    private readonly ushort _height;
    private readonly byte _brightness;
    private readonly float _multiplier;

    public PixelColor(ushort height, byte brightness)
    {
        _height = height;
        _brightness = brightness;
        _multiplier = brightness / 100.0f;
    }

    /// <summary>24-bit value of one channel (0 = R, 1 = G, 2 = B).</summary>
    public byte To8Bit(Rgb c, int channel)
    {
        if (_height == 80)
            return Map8To8(ApplyBrightness(c), channel);   // brightness applied twice, as in PixelTuner 2
        if (_height > 150)
            return MapApa102(c, channel);
        return Map8To8(c, channel);
    }

    /// <summary>36-bit value of one channel through the LED calibration table.</summary>
    public ushort To12Bit(Rgb c, int channel)
    {
        var scaled = ApplyBrightness(c);
        bool led3528 = _height >= 100;
        return channel switch
        {
            0 => (led3528 ? ColorProfiles.Led3528R : ColorProfiles.DefaultR)[scaled.R],
            1 => (led3528 ? ColorProfiles.Led3528G : ColorProfiles.DefaultG)[scaled.G],
            _ => (led3528 ? ColorProfiles.Led3528B : ColorProfiles.DefaultB)[scaled.B],
        };
    }

    /// <summary>12-bit value (0 to 15) of one channel.</summary>
    public int To4Bit(Rgb c, int channel)
    {
        var scaled = ApplyBrightness(c);
        byte value = channel switch { 0 => scaled.R, 1 => scaled.G, _ => scaled.B };
        return (byte)(15 * value / byte.MaxValue) & 0x0f;
    }

    private Rgb ApplyBrightness(Rgb c)
    {
        float m = (float)_brightness / 100;
        return new Rgb((byte)(c.R * m), (byte)(c.G * m), (byte)(c.B * m));
    }

    /// <summary>Default LEDs: green and blue are stronger than red, so they are scaled down.</summary>
    private byte Map8To8(Rgb c, int channel)
    {
        byte result = channel switch
        {
            0 => (byte)(c.R * 1.0),
            1 => (byte)(c.G * 0.5),
            _ => (byte)(c.B * 0.8),
        };
        return (byte)(result * _multiplier);
    }

    /// <summary>APA102 LEDs (devices taller than 150): gamma 2.8 with a per-channel maximum.</summary>
    private byte MapApa102(Rgb c, int channel)
    {
        byte result = channel switch
        {
            0 => GammaCorrected(c.R, 160),
            1 => GammaCorrected(c.G, 200),
            _ => GammaCorrected(c.B, 255),
        };
        return (byte)(result * _multiplier);
    }

    private static byte GammaCorrected(byte c, int maxOut) =>
        (byte)(Math.Pow((float)c / (float)255, Gamma) * maxOut + 0.5f);
}
