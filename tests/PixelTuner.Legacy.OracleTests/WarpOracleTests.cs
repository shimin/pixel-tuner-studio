using System;
using Common.Imaging;
using PixelTuner.Core.Imaging;
using Xunit;

namespace PixelTuner.Legacy.OracleTests;

/// <summary>
/// The Transform page of PixelTuner 2's import window against its own warp code (<c>WarpGrid</c>, compiled from legacy).
/// The glue from <c>FttImageProcessor</c> (blank border, the grids) is repeated here; the image libraries it sat on are not needed.
/// </summary>
public class WarpOracleTests
{
    private static PixelImage Random(Random random, int width, int height)
    {
        var image = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                image[x, y] = new Rgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        return image;
    }

    private static PixelImage Legacy(PixelImage source, double top, double middle, double bottom)
    {
        int width = source.Width, height = source.Height;
        var pixelsIn = new int[width * height];
        var pixelsOut = new int[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var c = source[x, y];
                pixelsIn[y * width + x] = (x > 1 && y > 1) && (x < (width - 1) && y < (height - 1))
                    ? unchecked((int)0xFF000000) | (c.R << 16) | (c.G << 8) | c.B
                    : unchecked((int)0xFF000000);
            }

        var wpSource = new WarpGrid(3, 3, width, height);
        var wpDest = new WarpGrid(3, 3, width, height);
        double[] x3 = [1 - top, 0.5, top, 1 - middle, 0.5, middle, 1 - bottom, 0.5, bottom];
        for (int i = 0; i < x3.Length; i++) wpDest.XlGrid[i] = (float)x3[i] * width;
        WarpGrid.Warp(ref pixelsIn, width, height, wpSource, wpDest, ref pixelsOut);

        var result = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int argb = pixelsOut[y * width + x];
                result[x, y] = new Rgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            }
        return result;
    }

    [Theory]
    [InlineData(1, 40, 30, 0.65, 0.8, 1.0)]          // PixelTuner 2's starting values
    [InlineData(2, 100, 50, 0.51, 0.51, 0.51)]       // the most it squeezes
    [InlineData(3, 64, 64, 1.0, 1.0, 1.0)]           // nothing moves
    [InlineData(4, 33, 71, 0.9, 0.6, 0.75)]
    [InlineData(5, 200, 36, 0.55, 0.95, 0.7)]
    [InlineData(6, 7, 9, 0.7, 0.7, 0.7)]
    [InlineData(7, 125, 80, 0.62, 0.84, 0.97)]
    [InlineData(8, 4, 4, 0.8, 0.8, 0.8)]
    public void Perspective_warp_matches_PixelTuner_2(int seed, int width, int height, double top, double middle, double bottom)
    {
        var source = Random(new Random(seed), width, height);

        var expected = Legacy(source, top, middle, bottom);
        var actual = PerspectiveWarp.Apply(source, top, middle, bottom);

        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Assert.True(expected[x, y] == actual[x, y], $"Pixel ({x}, {y}): PixelTuner 2 {expected[x, y]}, got {actual[x, y]}.");
    }

    [Fact]
    public void Random_settings_match_PixelTuner_2()
    {
        var random = new Random(99);
        for (int n = 0; n < 25; n++)
        {
            int width = random.Next(5, 150), height = random.Next(5, 90);
            double top = 0.51 + random.NextDouble() * 0.49, middle = 0.51 + random.NextDouble() * 0.49, bottom = 0.51 + random.NextDouble() * 0.49;
            var source = Random(random, width, height);

            var expected = Legacy(source, top, middle, bottom);
            var actual = PerspectiveWarp.Apply(source, top, middle, bottom);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    Assert.True(expected[x, y] == actual[x, y], $"Case {n}, pixel ({x}, {y}).");
        }
    }
}
