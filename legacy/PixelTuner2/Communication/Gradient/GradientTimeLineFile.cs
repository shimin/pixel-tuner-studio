using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Gradient
{
    public class GradientTimeLineFile
    {
        public System.Drawing.Color TimelineColor { get; set; }

        public List<GradientImageFile> Images { get; set; }

        public GradientTimeLineFile()
        {
            Images = new List<GradientImageFile>();
            TimelineColor = System.Drawing.Color.FromArgb(0, 0, 0x69);
        }

        internal UInt32 GetSize()
        {
            return (UInt32)(GradientImageFile.Size * Images.Count + 2 + 3 + 4);
        }

        internal List<byte> GetBinary(GradientsCollection imgCollection)
        {
            var bin = new List<byte>();
            bin.AddRange(BitConverter.GetBytes((UInt16)Images.Count));      // 2 byte images count
            bin.Add(TimelineColor.R);                                       // 1 byte R
            bin.Add(TimelineColor.G);                                       // 1 byte G
            bin.Add(TimelineColor.B);                                       // 1 byte B
            bin.AddRange(BitConverter.GetBytes(GetTimelineDuration()));     // 4 bytes timeline duration
            foreach (var v in Images)
            {
                var desc = imgCollection.GetItem(v.Key);
                if (desc != null)
                {
                    v.StartAddress = desc.StartAddress;
                    bin.AddRange(BitConverter.GetBytes(v.StartAddress));    // 4 bytes
                    bin.AddRange(BitConverter.GetBytes((UInt16)(v.Stretching * 5)));  // 2 bytes
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
