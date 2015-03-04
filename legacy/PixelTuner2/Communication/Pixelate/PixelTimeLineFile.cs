using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Communication.Pixelate
{
    public class PixelTimelineFile
    {
        public List<PixelImageFile> Images { get; set; }

        public PixelTimelineFile()
        {
            Images = new List<PixelImageFile>();
        }

        internal UInt32 GetSize()
        {
            return (UInt32)(PixelImageFile.Size * Images.Count);
        }

        internal List<byte> GetBinary(ImagesCollection imgCollection)
        {
            var bin = new List<byte>();
            bin.Add((byte)Images.Count);
            foreach (var v in Images)
            {
                var desc = imgCollection.GetItem(v.Key);
                if (desc != null)
                {
                    v.StartAddress = desc.StartAddress;
                    bin.AddRange(BitConverter.GetBytes(v.StartAddress));    // 4 bytes
                    bin.AddRange(BitConverter.GetBytes(v.Stretching));      // 2 bytes
                    bin.AddRange(BitConverter.GetBytes(v.Duration));        // 2 bytes
                }
            }
            return bin;
        }
    }
}
