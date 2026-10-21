namespace PixelTuner.Core.Imaging;

/// <summary>
/// The "Transform" page of PixelTuner 2's import window: a photo is squeezed towards its vertical middle line, more at the top
/// than at the bottom, so that what the poi draws in the air looks right on its curved path. A port of the mesh warp
/// PixelTuner 2 used (<c>FttImageProcessor</c>, <c>WarpGrid</c> and <c>ImageHelper.RowWarpResample</c>, which are a
/// port of the warp filter from JH Labs): a 3 x 3 grid whose columns are moved sideways, with splines between the rows.
/// </summary>
/// <remarks>
/// Kept as PixelTuner 2 had it, quirks included: the outer 2 pixels at the top and left and 1 pixel at the bottom and right are blanked first, and every
/// row is resampled by area averaging that runs on into the first pixel of the next row.
/// </remarks>
public static class PerspectiveWarp
{
    /// <summary>The sliders' range: how much of the width the picture keeps at a height. Below 0.51 the two sides would cross.</summary>
    public const double MinOffset = 0.51;

    public const double MaxOffset = 1;

    // PixelTuner 2's starting values.
    public const double DefaultTop = 0.65;
    public const double DefaultMiddle = 0.8;
    public const double DefaultBottom = 1;

    /// <summary>
    /// PixelTuner 2 scaled a picture taller than this down before it warped it (the page was drawn at that size).
    /// </summary>
    public const int PreviewHeight = 800;

    /// <param name="top">Where the right edge of the top row goes, as a part of the width (0.51 to 1); the left edge goes to 1 - top.</param>
    /// <param name="middle">The same for the middle row.</param>
    /// <param name="bottom">The same for the bottom row.</param>
    public static PixelImage Apply(PixelImage source, double top = DefaultTop, double middle = DefaultMiddle, double bottom = DefaultBottom)
    {
        foreach (double offset in new[] { top, middle, bottom })
            if (offset is < MinOffset or > MaxOffset) throw new ArgumentOutOfRangeException(nameof(top), "An offset is between 0.51 and 1.");
        if (source.Height > PreviewHeight)
            source = ImageImport.Scale(source, (int)(source.Width / ((double)source.Height / PreviewHeight)), PreviewHeight);

        int width = source.Width, height = source.Height;
        var pixelsIn = new int[width * height];
        var pixelsOut = new int[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var c = source[x, y];
                pixelsIn[y * width + x] = x > 1 && y > 1 && x < width - 1 && y < height - 1
                    ? unchecked((int)0xFF000000) | (c.R << 16) | (c.G << 8) | c.B
                    : unchecked((int)0xFF000000);
            }

        // The destination grid: the points of the page, left to right and top to bottom; only their x moves.
        var sourceGrid = new Grid(3, 3, width, height);
        var destGrid = new Grid(3, 3, width, height);
        double[] points = [1 - top, 0.5, top, 1 - middle, 0.5, middle, 1 - bottom, 0.5, bottom];
        for (int i = 0; i < points.Length; i++) destGrid.X[i] = (float)points[i] * width;

        Warp(pixelsIn, width, height, sourceGrid, destGrid, pixelsOut);

        var result = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int argb = pixelsOut[y * width + x];
                result[x, y] = new Rgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            }
        return result;
    }

    private sealed class Grid
    {
        public readonly float[] X;
        public readonly float[] Y;
        public readonly int Rows;
        public readonly int Cols;

        public Grid(int rows, int cols, int w, int h)
        {
            Rows = rows;
            Cols = cols;
            X = new float[rows * cols];
            Y = new float[rows * cols];
            int index = 0;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    X[index] = (float)col * (w - 1) / (cols - 1);
                    Y[index] = (float)row * (h - 1) / (rows - 1);
                    index++;
                }
        }
    }

    // Catmull-Rom spline constants.
    private const float M00 = -0.5f, M01 = 1.5f, M02 = -1.5f, M03 = 0.5f;
    private const float M10 = 1.0f, M11 = -2.5f, M12 = 2.0f, M13 = -0.5f;
    private const float M20 = -0.5f, M22 = 0.5f, M31 = 1.0f;

    private static void InterpolateSpline(float[] xKnots, float[] yKnots, int offset, int length, float[] splineY, int splineOffset, int splineLength)
    {
        int index = offset;
        int end = offset + length - 1;
        float x0 = xKnots[index];
        float k0, k1, k2;
        k0 = k1 = k2 = yKnots[index];
        float x1 = xKnots[index + 1];
        float k3 = yKnots[index + 1];

        for (int i = 0; i < splineLength; i++)
        {
            if (index <= end && i > xKnots[index])
            {
                k0 = k1;
                k1 = k2;
                k2 = k3;
                x0 = xKnots[index];
                index++;
                if (index <= end) x1 = xKnots[index];
                if (index < end) k3 = yKnots[index + 1];
                else k3 = k2;
            }
            float t = (i - x0) / (x1 - x0);
            float c3 = M00 * k0 + M01 * k1 + M02 * k2 + M03 * k3;
            float c2 = M10 * k0 + M11 * k1 + M12 * k2 + M13 * k3;
            float c1 = M20 * k0 + M22 * k2;
            float c0 = M31 * k1;
            splineY[splineOffset + i] = ((c3 * t + c2) * t + c1) * t + c0;
        }
    }

    private static void Warp(int[] inPixels, int cols, int rows, Grid sourceGrid, Grid destGrid, int[] outPixels)
    {
        int size = Math.Max(cols, rows);
        var xrow = new float[size];
        var yrow = new float[size];
        var scale = new float[size + 1];
        var interpolated = new float[size + 1];
        int gridCols = sourceGrid.Cols, gridRows = sourceGrid.Rows;
        var splines = new Grid(rows, gridCols, 1, 1);

        for (int u = 0; u < gridCols; u++)
        {
            int i = u;
            for (int v = 0; v < gridRows; v++)
            {
                xrow[v] = sourceGrid.X[i];
                yrow[v] = sourceGrid.Y[i];
                i += gridCols;
            }
            InterpolateSpline(yrow, xrow, 0, gridRows, interpolated, 0, rows);
            i = u;
            for (int y = 0; y < rows; y++)
            {
                splines.X[i] = interpolated[y];
                i += gridCols;
            }
        }

        for (int u = 0; u < gridCols; u++)
        {
            int i = u;
            for (int v = 0; v < gridRows; v++)
            {
                xrow[v] = destGrid.X[i];
                yrow[v] = destGrid.Y[i];
                i += gridCols;
            }
            InterpolateSpline(yrow, xrow, 0, gridRows, interpolated, 0, rows);
            i = u;
            for (int y = 0; y < rows; y++)
            {
                splines.Y[i] = interpolated[y];
                i += gridCols;
            }
        }

        int offset = 0;
        for (int y = 0; y < rows; y++)
        {
            InterpolateSpline(splines.X, splines.Y, offset, gridCols, scale, 0, cols);
            scale[cols] = cols;
            RowWarpResample(inPixels, outPixels, cols, y * cols, 1, scale);
            offset += gridCols;
        }
    }

    /// <summary>Resamples one row by area averaging, to the output positions of every source pixel (ImageHelper.RowWarpResample).</summary>
    private static void RowWarpResample(int[] sourcePixels, int[] destPixels, int scanLineLength, int offset, int stride, float[] outPositions)
    {
        int srcIndex = offset, destIndex = offset, lastIndex = sourcePixels.Length;
        var inArr = new float[scanLineLength + 2];
        int i = 0;
        for (int j = 0; j < scanLineLength; j++)
        {
            while (outPositions[i + 1] < j) i++;
            inArr[j] = i + (float)(j - outPositions[i]) / (outPositions[i + 1] - outPositions[i]);
        }
        inArr[scanLineLength] = scanLineLength;
        inArr[scanLineLength + 1] = scanLineLength;

        float inSegment = 1.0f;
        float outSegment = inArr[1];
        float sizfac = outSegment;
        float rSum = 0, gSum = 0, bSum = 0;
        int rgb = sourcePixels[srcIndex];
        int r = (rgb >> 16) & 0xff, g = (rgb >> 8) & 0xff, b = rgb & 0xff;
        srcIndex += stride;
        rgb = sourcePixels[srcIndex];
        int nextR = (rgb >> 16) & 0xff, nextG = (rgb >> 8) & 0xff, nextB = rgb & 0xff;
        srcIndex += stride;
        i = 1;

        while (i <= scanLineLength)
        {
            float rIntensity = inSegment * r + (1.0f - inSegment) * nextR;
            float gIntensity = inSegment * g + (1.0f - inSegment) * nextG;
            float bIntensity = inSegment * b + (1.0f - inSegment) * nextB;
            if (inSegment < outSegment)
            {
                rSum += rIntensity * inSegment;
                gSum += gIntensity * inSegment;
                bSum += bIntensity * inSegment;
                outSegment -= inSegment;
                inSegment = 1.0f;
                r = nextR;
                g = nextG;
                b = nextB;
                if (srcIndex < lastIndex) rgb = sourcePixels[srcIndex];
                nextR = (rgb >> 16) & 0xff;
                nextG = (rgb >> 8) & 0xff;
                nextB = rgb & 0xff;
                srcIndex += stride;
            }
            else
            {
                rSum += rIntensity * outSegment;
                gSum += gIntensity * outSegment;
                bSum += bIntensity * outSegment;
                destPixels[destIndex] = unchecked((int)0xFF000000)
                    | ((int)Math.Min(rSum / sizfac, 255) << 16)
                    | ((int)Math.Min(gSum / sizfac, 255) << 8)
                    | (int)Math.Min(bSum / sizfac, 255);
                destIndex += stride;
                rSum = gSum = bSum = 0.0f;
                inSegment -= outSegment;
                outSegment = inArr[i + 1] - inArr[i];
                sizfac = outSegment;
                i++;
            }
        }
    }
}
