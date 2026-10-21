using SkiaSharp;

namespace PixelTuner.Core.Imaging;

/// <summary>A rectangle of a picture, in pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public static PixelRect Whole(PixelImage image) => new(0, 0, image.Width, image.Height);
}

/// <summary>
/// The colour corrections of PixelTuner 2's import window, in its units: the sliders and their ranges are the same.
/// Everything at its default leaves the picture as it is.
/// </summary>
/// <param name="Brightness">-255 to 255, added to every channel.</param>
/// <param name="MidtonesOnly">Brightness works less on the darkest and brightest values (PixelTuner 2's "midtones only").</param>
/// <param name="Contrast">-100 to 100.</param>
/// <param name="Hue">-180 to 180 degrees.</param>
/// <param name="Saturation">-100 to 100 percent.</param>
/// <param name="Value">-100 to 100 percent of lightness.</param>
/// <param name="Sharpen">0 is off; 0.1 to 10 is the amount of an unsharp mask.</param>
public sealed record ImportAdjustments(
    int Brightness = 0, bool MidtonesOnly = false, double Contrast = 0, int Hue = 0, double Saturation = 0, double Value = 0, double Sharpen = 0)
{
    public static readonly ImportAdjustments None = new();

    public bool IsNone => Brightness == 0 && Contrast == 0 && Hue == 0 && Saturation == 0 && Value == 0 && Sharpen == 0;
}

/// <summary>
/// What PixelTuner 2's import window did to a picture before it went on a timeline: crop, scale to the height of
/// the device, then colour. A picture that is not as high as the LEDs of the device cannot be played, so the editor brings it there.
/// </summary>
public static class ImageImport
{
    /// <summary>The width a picture gets when it is scaled to a height and keeps its proportions (PixelTuner 2: <c>Math.Round(height * aspect)</c>, so a half goes to the even number).</summary>
    public static int ScaledWidth(int width, int height, int targetHeight)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        return Math.Max(1, (int)Math.Round(targetHeight * ((double)width / height)));
    }

    public static PixelImage Crop(PixelImage source, PixelRect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > source.Width || rect.Y + rect.Height > source.Height)
            throw new ArgumentOutOfRangeException(nameof(rect), "The rectangle is not inside the picture.");
        var result = new PixelImage(rect.Width, rect.Height);
        for (int y = 0; y < rect.Height; y++)
            for (int x = 0; x < rect.Width; x++)
                result[x, y] = source[rect.X + x, rect.Y + y];
        return result;
    }

    /// <summary>Resamples to a size with a smooth filter (a downscale is averaged, as PixelTuner 2's high quality bicubic did).</summary>
    public static PixelImage Scale(PixelImage source, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (width == source.Width && height == source.Height) return source;
        using var from = ToBitmap(source);
        bool shrinks = width < source.Width || height < source.Height;
        var sampling = shrinks
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKCubicResampler.Mitchell);
        using var to = from.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul), sampling)
            ?? throw new InvalidOperationException("The picture could not be scaled.");
        return FromBitmap(to);
    }

    /// <summary>Crop, scale to <paramref name="targetHeight"/> keeping the proportions, then the colour corrections.</summary>
    /// <param name="crop">Null takes the whole picture.</param>
    public static PixelImage Fit(PixelImage source, PixelRect? crop, int targetHeight, ImportAdjustments? adjustments = null)
    {
        var cut = crop is { } rect ? Crop(source, rect) : source;
        var scaled = Scale(cut, ScaledWidth(cut.Width, cut.Height, targetHeight), targetHeight);
        return Adjust(scaled, adjustments ?? ImportAdjustments.None);
    }

    /// <summary>Colour corrections in PixelTuner 2's order: brightness, contrast, hue/saturation/value, sharpening.</summary>
    public static PixelImage Adjust(PixelImage source, ImportAdjustments a)
    {
        if (a.IsNone) return source;
        var result = new PixelImage(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                var c = source[x, y];
                if (a.Brightness != 0) c = Brightness(c, a.Brightness, a.MidtonesOnly);
                if (a.Contrast != 0) c = Contrast(c, a.Contrast);
                if (a.Hue != 0 || a.Saturation != 0 || a.Value != 0) c = Hsl(c, a.Hue, a.Saturation / 100, a.Value / 100);
                result[x, y] = c;
            }
        return a.Sharpen > 0 ? Sharpen(result, a.Sharpen) : result;
    }

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);

    private static Rgb Brightness(Rgb c, int brightness, bool midtones) =>
        new(Clamp(BrightnessPart(c.R, brightness, midtones)), Clamp(BrightnessPart(c.G, brightness, midtones)), Clamp(BrightnessPart(c.B, brightness, midtones)));

    // PixelTuner 2 weighed brightness by how far a value is from the extremes when "midtones only" was on (threshold 100).
    private static int BrightnessPart(int value, int brightness, bool midtones)
    {
        if (!midtones) return value + brightness;
        const int threshold = 100;
        if (value < threshold * 0.25) return value + (int)(brightness * 0.1);
        if (value < threshold * 0.5) return value + (int)(brightness * 0.3);
        if (value < threshold * 0.75) return value + (int)(brightness * 0.6);
        if (value < threshold) return value + (int)(brightness * 0.8);
        return value + brightness;
    }

    private static Rgb Contrast(Rgb c, double contrast)
    {
        double k = (100.0 + contrast) / 100.0;
        k *= k;
        byte One(byte v) => (byte)Math.Clamp((((v / 255.0 - 0.5) * k) + 0.5) * 255, 0, 255);
        return new Rgb(One(c.R), One(c.G), One(c.B));
    }

    // Hue, saturation and lightness as System.Drawing's Color.GetHue, GetSaturation and GetBrightness gave them.
    private static Rgb Hsl(Rgb c, int deltaHue, double deltaSaturation, double deltaLightness)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double lightness = (max + min) / 2, saturation = 0, hue = 0;
        if (max != min)
        {
            double d = max - min;
            saturation = lightness <= 0.5 ? d / (max + min) : d / (2 - max - min);
            hue = max == r ? (g - b) / d : max == g ? 2 + (b - r) / d : 4 + (r - g) / d;
            hue *= 60;
            if (hue < 0) hue += 360;
        }
        hue = deltaHue + hue < 0 ? deltaHue + hue + 360 : (deltaHue + hue) % 360;
        saturation = Math.Clamp(saturation + deltaSaturation, 0, 1);
        lightness = Math.Clamp(lightness + deltaLightness, 0, 1);
        return FromHsl(hue / 360.0, saturation, lightness);
    }

    /// <summary>PixelTuner 2's <c>ImageProcessor.ColorFromHSL</c>.</summary>
    private static Rgb FromHsl(double h, double sl, double l)
    {
        double r = l, g = l, b = l;
        double v = l <= 0.5 ? l * (1.0 + sl) : l + sl - l * sl;
        if (v > 0)
        {
            double m = l + l - v;
            double sv = (v - m) / v;
            h *= 6.0;
            int sextant = (int)h;
            double fract = h - sextant;
            double vsf = v * sv * fract, mid1 = m + vsf, mid2 = v - vsf;
            switch (sextant)
            {
                case 0: (r, g, b) = (v, mid1, m); break;
                case 1: (r, g, b) = (mid2, v, m); break;
                case 2: (r, g, b) = (m, v, mid1); break;
                case 3: (r, g, b) = (m, mid2, v); break;
                case 4: (r, g, b) = (mid1, m, v); break;
                case 5: (r, g, b) = (v, m, mid2); break;
            }
        }
        return new Rgb((byte)Math.Clamp(r * 255.0, 0, 255), (byte)Math.Clamp(g * 255.0, 0, 255), (byte)Math.Clamp(b * 255.0, 0, 255));
    }

    /// <summary>Unsharp mask: the picture plus <paramref name="amount"/> times what a 3 x 3 blur takes away. The border stays as it was.</summary>
    private static PixelImage Sharpen(PixelImage source, double amount)
    {
        var result = new PixelImage(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                var c = source[x, y];
                if (x == 0 || y == 0 || x == source.Width - 1 || y == source.Height - 1) { result[x, y] = c; continue; }
                double sr = 0, sg = 0, sb = 0, weight = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        double w = (dx == 0 ? 2 : 1) * (dy == 0 ? 2 : 1);   // a Gaussian-like 1-2-1 kernel
                        var n = source[x + dx, y + dy];
                        sr += n.R * w; sg += n.G * w; sb += n.B * w; weight += w;
                    }
                result[x, y] = new Rgb(
                    Clamp((int)Math.Round(c.R + amount * (c.R - sr / weight))),
                    Clamp((int)Math.Round(c.G + amount * (c.G - sg / weight))),
                    Clamp((int)Math.Round(c.B + amount * (c.B - sb / weight))));
            }
        return result;
    }

    /// <summary>The picture as a PNG, for the package: lossless, so what was cut and scaled stays exactly as the user saw it.</summary>
    public static byte[] EncodePng(PixelImage image)
    {
        using var bitmap = ToBitmap(image);
        using var skImage = SKImage.FromBitmap(bitmap);
        return skImage.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static SKBitmap ToBitmap(PixelImage image)
    {
        var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var pixels = new byte[image.Width * image.Height * 4];
        int at = 0;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                var c = image[x, y];
                pixels[at++] = c.R; pixels[at++] = c.G; pixels[at++] = c.B; pixels[at++] = 255;
            }
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        return bitmap;
    }

    private static PixelImage FromBitmap(SKBitmap bitmap)
    {
        var result = new PixelImage(bitmap.Width, bitmap.Height);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        int at = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++, at += 4)
                result[x, y] = new Rgb(pixels[at], pixels[at + 1], pixels[at + 2]);
        return result;
    }
}
