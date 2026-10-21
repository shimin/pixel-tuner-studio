using System.Collections.Generic;
using ImageProcessor;
using Point = System.Windows.Point;


namespace Common.Imaging
{
    public static class ImageProcessorExtenstions
    {
        public static ImageFactory FttImage(this ImageFactory img,
            Point pointLt, Point pointMt, Point pointRt, 
            Point pointLm, Point pointMm, Point pointRm, 
            Point pointLb, Point pointMb, Point pointRb
            )
        {
            if (img.ShouldProcess)
            {
                FttImageProcessor ftt = new FttImageProcessor { DynamicParameter = 
                    new List<Point>
                    {
                        pointLt, pointMt, pointRt,
                        pointLm, pointMm, pointRm,
                        pointLb, pointMb, pointRb
                    }
                };
                img.CurrentImageFormat.ApplyProcessor(ftt.ProcessImage, img);
            }

            return img;
        }
    }
}