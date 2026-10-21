namespace PixelTuner.Core.Imaging;

/// <summary>
/// A decoded image as plain 8-bit RGB pixels, row by row from the top.
/// Alpha is dropped: devices have no transparency, and PixelTuner 2 read
/// only R, G and B too.
/// </summary>
public sealed class PixelImage
{
    private readonly Rgb[] _pixels;

    public PixelImage(int width, int height)
        : this(width, height, new Rgb[checked(width * height)])
    {
    }

    public PixelImage(int width, int height, Rgb[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (pixels.Length != width * height)
            throw new ArgumentException("Pixel count does not match the size.", nameof(pixels));

        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }

    public Rgb this[int x, int y]
    {
        get => _pixels[y * Width + x];
        set => _pixels[y * Width + x] = value;
    }

    /// <summary>An all-black image, as PixelTuner 2 used for gaps on a timeline.</summary>
    public static PixelImage Black(int width, int height) => new(width, height);

    /// <summary>A copy flipped top to bottom.</summary>
    public PixelImage FlippedVertically()
    {
        var result = new PixelImage(Width, Height);
        for (int y = 0; y < Height; y++)
            Array.Copy(_pixels, y * Width, result._pixels, (Height - 1 - y) * Width, Width);
        return result;
    }
}

/// <summary>An 8-bit RGB colour.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Black => default;
}
