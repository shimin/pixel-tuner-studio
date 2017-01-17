using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Communication.Pixelate
{
    public class ImagesCollectionVideoItem : ImagesCollectionItem
    {
        private List<Point> _points;
        private int _xOffset = 0;
        private int _yOffset = 0;

        public ImagesCollectionVideoItem(string imagePath, List<Point> points,  ColorDepth depth, int xOffset, int yOffset)
            : base(imagePath, (UInt16)points.Count, depth)
        {
            _xOffset = xOffset;
            _yOffset = yOffset;
            _points = points;
            Load((ushort) points.Count);
        }

        private void Load(ushort targetHeight)
        {
            if (File.Exists(_path))
            {
                Image gifImg = Image.FromFile(_path);
                var dimension = new FrameDimension(gifImg.FrameDimensionsList[0]);

                var frameCount = gifImg.GetFrameCount(dimension);
                _bitmap = new Bitmap(frameCount, targetHeight);

                for (int idx = 0; idx < frameCount; idx++)
                {
                    gifImg.SelectActiveFrame(dimension, idx);
                    var frame = new Bitmap(gifImg);
                    CopyPixelData(_bitmap, frame, _points, idx);
                }
            }
            else
            {
                _bitmap = new Bitmap(1, targetHeight);
            }
            var size = GetSize(targetHeight);
            _binary = new List<byte>(new byte[size]);
        }

        protected override void LoadFile(ushort targetHeight)
        {
            
        }

        private void CopyPixelData(Bitmap bitmap, Bitmap frame, List<Point> points, int frameIdx)
        {
            int row = 0;
            try
            {
                foreach (var point in points)
                {
                    var px = Color.Black;
                    if (point.X < frame.Width && point.Y < frame.Height)
                        px = frame.GetPixel(point.X + _xOffset, point.Y + _yOffset);
                    //Console.WriteLine($"X={point.X}, Y={point.Y}");
                    bitmap.SetPixel(frameIdx, row, px);
                    row++;
                }
            }
            catch(Exception ex)
            {
                
            }
        }
    }
}
