using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Communication.Pixelate
{
    public class PixelTimelineFile
    {
        public System.Drawing.Color TimelineColor { get; set; }

        public System.Drawing.Color TimelineColorSelection { get; set; }

        public ushort TimeLineHeight { get; set; }

        public List<PixelImageFile> Images { get; set; }

        public PixelTimelineFile()
        {
            Images = new List<PixelImageFile>();
            TimelineColor = System.Drawing.Color.FromArgb(0, 0, 20);
            TimelineColorSelection = System.Drawing.Color.FromArgb(70, 0, 0);
        }

        internal UInt32 GetSize()
        {
            return (UInt32)(PixelImageFile.Size * Images.Count + 2 + 3 + 3 + 4);
        }

        internal List<byte> GetBinary(ImagesCollection imgCollection)
        {
            var bin = new List<byte>();
            bin.AddRange(BitConverter.GetBytes((UInt16)Images.Count));      // 2 byte images count
            bin.Add(TimelineColor.R);                                       // 1 byte R
            bin.Add(TimelineColor.G);                                       // 1 byte G
            bin.Add(TimelineColor.B);                                       // 1 byte B
            bin.Add(TimelineColorSelection.R);                              // 1 byte selection R
            bin.Add(TimelineColorSelection.G);                              // 1 byte selection G
            bin.Add(TimelineColorSelection.B);                              // 1 byte selection B
            bin.AddRange(BitConverter.GetBytes(GetTimelineDuration()));     // 4 bytes timeline duration
            foreach (var v in Images)
            {
                var desc = imgCollection.GetItem(v.Key);
                if (desc != null)
                {
                    v.StartAddress = desc.StartAddress;
                    bin.AddRange(BitConverter.GetBytes(v.StartAddress));    // 4 bytes
                    bin.AddRange(BitConverter.GetBytes(v.Stretching));      // 2 bytes
                    bin.AddRange(BitConverter.GetBytes(v.Mask));            // 2 bytes
                    bin.AddRange(BitConverter.GetBytes(v.StartTime));       // 4 bytes
                }
            }
            return bin;
        }

        private UInt32 GetTimelineDuration()
        {
            UInt32 totalMilliseconds = 0;
            Images.ForEach( i => totalMilliseconds += i.Duration );
            return totalMilliseconds;
        }
    }
}
