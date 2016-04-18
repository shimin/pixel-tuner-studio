using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using ImageProcessor;
using ImageProcessor.Common.Exceptions;
using ImageProcessor.Imaging;
using ImageProcessor.Processors;

using Point = System.Windows.Point;

namespace Common.Imaging 
{
    public class FttImageProcessor : IGraphicsProcessor
    {
        public dynamic DynamicParameter { get; set; }

        public Dictionary<string, string> Settings { get; set; }

        public FttImageProcessor()
        {
            Settings = new Dictionary<string, string>();
        }

        public Image ProcessImage(ImageFactory factory)
        {
            try
            {
                var width = factory.Image.Width;
                var height = factory.Image.Height;

                var fftDestination = new Bitmap(width, height, factory.Image.PixelFormat);
                fftDestination.SetResolution(factory.Image.HorizontalResolution, factory.Image.VerticalResolution);
                
                var pixelsIn = new int[width*height];
                var pixelsOut = new int[width*height];

                using (var sourceBitmap = new FastBitmap(factory.Image))
                {                                      
                    Parallel.For(0, height, y =>
                    {
                        for (var x = 0; x < sourceBitmap.Width; x++)
                        {
                            pixelsIn[(y*width) + x] = (x > 1 && y > 1) && (x < (width - 1) && y < (height - 1))
                                ? sourceBitmap.GetPixel(x, y).ToArgb()
                                : Color.Black.ToArgb();
                        }                          
                    });                    
                }

                var wpSource = new WarpGrid(3, 3, width, height);
                var wpDest = new WarpGrid(3, 3, width, height);
                var listPoint = (List<Point>)DynamicParameter;
                for (int i = 0; i < listPoint.Count; i++)
                {
                    wpDest.XlGrid[i] = (float)listPoint[i].X * width;
                }
                
                WarpGrid.Warp(ref pixelsIn, width, height, wpSource, wpDest, ref pixelsOut);

                using (var destBitmap = new FastBitmap(fftDestination))
                {
                    Parallel.For(0, height, y =>
                    {
                        for (var x = 0; x < destBitmap.Width; x++)
                        {
                            destBitmap.SetPixel(x, y, Color.FromArgb(pixelsOut[(y * width) + x]));                            
                        }
                    });
                }
                return fftDestination;
            }
            catch (Exception)
            {
                throw new ImageProcessingException("Error in ftt!");
            }
        }
    }
}
