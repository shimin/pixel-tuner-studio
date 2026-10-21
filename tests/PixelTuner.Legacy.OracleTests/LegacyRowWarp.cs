// Verbatim copy of ImageHelper.RowWarpResample from legacy/PixelTuner2/Common/Imaging/ImageHelper.cs (that file needs WPF
// and GDI+ and cannot be compiled here as a whole); WarpGrid.cs, which calls it, is compiled from the legacy folder.
using System;

namespace Common.Imaging
{
    public static partial class ImageHelper
    {
        public static void RowWarpResample(ref int[] sourcePixels, ref int[] destPixels, int scanLineLength, int offset,
            int stride, float[] outPositions)
        {
            int i, j;
            float sizfac;
            float inSegment;
            float outSegment;
            int a, r, g, b, nextA, nextR, nextG, nextB;
            float aSum, rSum, gSum, bSum;
            float[] inArr;
            int srcIndex = offset;
            int destIndex = offset;
            int lastIndex = sourcePixels.Length;
            int rgb;

            inArr = new float[scanLineLength + 2];
            i = 0;
            for (j = 0; j < scanLineLength; j++)
            {
                while (outPositions[i + 1] < j)
                    i++;
                inArr[j] = i + (float)(j - outPositions[i]) / (outPositions[i + 1] - outPositions[i]);
                //			inArr[j] = ImageMath.clamp( inArr[j], 0, length-1 );
            }
            inArr[scanLineLength] = scanLineLength;
            inArr[scanLineLength + 1] = scanLineLength;

            inSegment = 1.0f;
            outSegment = inArr[1];
            sizfac = outSegment;
            aSum = rSum = gSum = bSum = 0.0f;
            rgb = sourcePixels[srcIndex];
            a = 255; //(rgb >> 24) & 0xff;
            r = (rgb >> 16) & 0xff;
            g = (rgb >> 8) & 0xff;
            b = rgb & 0xff;
            srcIndex += stride;
            rgb = sourcePixels[srcIndex];
            nextA = 255; //(rgb >> 24) & 0xff;
            nextR = (rgb >> 16) & 0xff;
            nextG = (rgb >> 8) & 0xff;
            nextB = rgb & 0xff;
            srcIndex += stride;
            i = 1;

            while (i <= scanLineLength)
            {
                float aIntensity = inSegment * a + (1.0f - inSegment) * nextA;
                float rIntensity = inSegment * r + (1.0f - inSegment) * nextR;
                float gIntensity = inSegment * g + (1.0f - inSegment) * nextG;
                float bIntensity = inSegment * b + (1.0f - inSegment) * nextB;
                if (inSegment < outSegment)
                {
                    aSum += (aIntensity * inSegment);
                    rSum += (rIntensity * inSegment);
                    gSum += (gIntensity * inSegment);
                    bSum += (bIntensity * inSegment);
                    outSegment -= inSegment;
                    inSegment = 1.0f;
                    a = nextA;
                    r = nextR;
                    g = nextG;
                    b = nextB;
                    if (srcIndex < lastIndex)
                        rgb = sourcePixels[srcIndex];
                    nextA = (rgb >> 24) & 0xff;
                    nextR = (rgb >> 16) & 0xff;
                    nextG = (rgb >> 8) & 0xff;
                    nextB = rgb & 0xff;
                    srcIndex += stride;
                }
                else {
                    aSum += (aIntensity * outSegment);
                    rSum += (rIntensity * outSegment);
                    gSum += (gIntensity * outSegment);
                    bSum += (bIntensity * outSegment);

                    destPixels[destIndex] =
                        (255 << 24 |
                        ((int)Math.Min(rSum / sizfac, 255) << 16) |
                        ((int)Math.Min(gSum / sizfac, 255) << 8) |
                        (int)Math.Min(bSum / sizfac, 255));

                    destIndex += stride;
                    aSum = rSum = gSum = bSum = 0.0f;
                    inSegment -= outSegment;
                    outSegment = inArr[i + 1] - inArr[i];
                    sizfac = outSegment;
                    i++;
                }
            }
        }
    }
}
