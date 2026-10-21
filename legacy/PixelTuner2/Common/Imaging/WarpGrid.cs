using System;

namespace Common.Imaging
{
    public class WarpGrid
    {
        public float[] XlGrid { get; set; }
        public float[] YlGrid { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }

        #region Polynom interpolation constants

        private const float M00 = -0.5f;
        private const float M01 = 1.5f;
        private const float M02 = -1.5f;
        private const float M03 = 0.5f;
        private const float M10 = 1.0f;
        private const float M11 = -2.5f;
        private const float M12 = 2.0f;
        private const float M13 = -0.5f;
        private const float M20 = -0.5f;
        private const float M22 = 0.5f;
        private const float M31 = 1.0f;

        #endregion Polynom interpolation constants

        public WarpGrid(int rows, int cols, int w, int h)
        {
            Rows = rows;
            Cols = cols;
            XlGrid = new float[rows * cols];
            YlGrid = new float[rows * cols];
            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    XlGrid[index] = (float)col * (w - 1) / (cols - 1);
                    YlGrid[index] = (float)row * (h - 1) / (rows - 1);
                    index++;
                }
            }
        }

        public static void InterpolateSpline(ref float[] xKnots, ref float[] yKnots, int offset, int length,
            ref float[] splineY, int splineOffset, int splineLength)
        {
            int index = offset;
            int end = offset + length - 1;
            float x0, x1;
            float k0, k1, k2, k3;
            float c0, c1, c2, c3;

            x0 = xKnots[index];
            k0 = k1 = k2 = yKnots[index];
            x1 = xKnots[index + 1];
            k3 = yKnots[index + 1];

            for (int i = 0; i < splineLength; i++)
            {
                if (index <= end && i > xKnots[index])
                {
                    k0 = k1;
                    k1 = k2;
                    k2 = k3;
                    x0 = xKnots[index];
                    index++;
                    if (index <= end)
                        x1 = xKnots[index];
                    if (index < end)
                        k3 = yKnots[index + 1];
                    else
                        k3 = k2;
                }
                float t = (i - x0) / (x1 - x0);
                c3 = M00 * k0 + M01 * k1 + M02 * k2 + M03 * k3;
                c2 = M10 * k0 + M11 * k1 + M12 * k2 + M13 * k3;
                c1 = M20 * k0 + M22 * k2;
                c0 = M31 * k1;

                splineY[splineOffset + i] = ((c3 * t + c2) * t + c1) * t + c0;
            }
        }

        public static void Warp(ref int[] inPixels, int cols, int rows, WarpGrid sourceGrid, WarpGrid destGrid,
            ref int[] outPixels)
        {
            int x, y;
            int u, v;
            int[] intermediate;
            WarpGrid splines;

            if (sourceGrid.Rows != destGrid.Rows || sourceGrid.Cols != destGrid.Cols)
                throw new ArgumentException("source and destination grids are different sizes");

            int size = Math.Max(cols, rows);
            float[] xrow = new float[size];
            float[] yrow = new float[size];
            float[] scale = new float[size + 1];
            float[] interpolated = new float[size + 1];

            int gridCols = sourceGrid.Cols;
            int gridRows = sourceGrid.Rows;

            splines = new WarpGrid(rows, gridCols, 1, 1);

            for (u = 0; u < gridCols; u++)
            {
                int i = u;

                for (v = 0; v < gridRows; v++)
                {
                    xrow[v] = sourceGrid.XlGrid[i];
                    yrow[v] = sourceGrid.YlGrid[i];
                    i += gridCols;
                }

                InterpolateSpline(ref yrow, ref xrow, 0, gridRows, ref interpolated, 0, rows);

                i = u;
                for (y = 0; y < rows; y++)
                {
                    splines.XlGrid[i] = interpolated[y];
                    i += gridCols;
                }
            }

            for (u = 0; u < gridCols; u++)
            {
                int i = u;

                for (v = 0; v < gridRows; v++)
                {
                    xrow[v] = destGrid.XlGrid[i];
                    yrow[v] = destGrid.YlGrid[i];
                    i += gridCols;
                }

                InterpolateSpline(ref yrow, ref xrow, 0, gridRows, ref interpolated, 0, rows);

                i = u;
                for (y = 0; y < rows; y++)
                {
                    splines.YlGrid[i] = interpolated[y];
                    i += gridCols;
                }
            }

            // intermediate = new int[rows * cols];

            int offset = 0;
            for (y = 0; y < rows; y++)
            {
                var xlGrid = splines.XlGrid;
                var ylGrid = splines.YlGrid;

                InterpolateSpline(ref xlGrid, ref ylGrid, offset, gridCols, ref scale, 0, cols);
                scale[cols] = cols;
                ImageHelper.RowWarpResample(ref inPixels, ref outPixels, cols, y*cols, 1, scale);
                offset += gridCols;
            }


            /*splines = new WarpGrid(gridRows, cols, 1, 1);

            offset = 0;
            int offset2 = 0;
            for (v = 0; v < gridRows; v++)
            {
                var xlGrid = sourceGrid.XlGrid;
                var ylGrid = sourceGrid.YlGrid;
                var splineY = splines.XlGrid;

                InterpolateSpline(ref xlGrid, ref ylGrid, offset, gridCols, ref splineY, offset2, cols);
                offset += gridCols;
                offset2 += cols;
            }

            offset = 0;
            offset2 = 0;
            for (v = 0; v < gridRows; v++)
            {
                var xlGrid = destGrid.XlGrid;
                var ylGrid = destGrid.YlGrid;
                var splineY = splines.YlGrid;

                InterpolateSpline(ref xlGrid, ref ylGrid, offset, gridCols, ref splineY, offset2, cols);
                offset += gridCols;
                offset2 += cols;
            }

            for (x = 0; x < cols; x++)
            {
                int i = x;

                for (v = 0; v < gridRows; v++)
                {
                    xrow[v] = splines.XlGrid[i]; ;
                    yrow[v] = splines.YlGrid[i]; ;
                    i += cols;
                }

                InterpolateSpline(ref xrow, ref yrow, 0, gridRows, ref scale, 0, rows);
                scale[rows] = rows;
                ImageHelpers.RowWarpResample(ref intermediate, ref outPixels, rows, x, cols, scale);
            } */
        }
    }
}