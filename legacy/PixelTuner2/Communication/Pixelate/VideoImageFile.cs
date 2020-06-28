using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Pixelate
{
    public class VideoImageFile : PixelImageFile
    {
        public VideoImageFile(string path, ushort stretching, uint duration, uint startTime, ushort mask, bool flipVertical)
            : base(path, stretching, duration, startTime, mask, flipVertical)
        {
        }
    }
}
