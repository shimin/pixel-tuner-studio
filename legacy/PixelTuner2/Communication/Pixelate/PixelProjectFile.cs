using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pixelate
{
    public class PixelProjectFile
    {
        #region Fields
        
        List<byte> projectPrefix = new List<byte>(Encoding.ASCII.GetBytes("FLSH"));

        public List<PixelTimelineFile> TimeLines { get; set; }

        #endregion

        #region Constructor

        public PixelProjectFile()
        {
            TimeLines = new List<PixelTimelineFile>();
        }

        #endregion

        #region Methods
        
        public List<byte> GetBinary(ImagesCollection images, byte targetHeight)
        {
            // ignore empty timelines
            var notEmptyLines = new List<PixelTimelineFile>();
            TimeLines.ForEach((tl) => { if (tl.Images.Count > 0) notEmptyLines.Add(tl); });


            List<byte> result = new List<byte>(projectPrefix);                          // header
            result.AddRange(new byte[4]);                                               // binary total size field
            result.Add((byte)notEmptyLines.Count);                                      // timelines count

            var firstFreeAddress = SetTimelineRefs(notEmptyLines, result);              // timeline addresses
            images.SetBlockAddress(firstFreeAddress);
            
            notEmptyLines.ForEach((tl) => { result.AddRange(tl.GetBinary(images)); });  // timelines data
            result.AddRange(images.GetBinary(targetHeight));                            // images data

            SetBinarySize(result);

            return result;
        }

        private void SetBinarySize(List<byte> result)
        {
            byte[] sizeBytes = BitConverter.GetBytes(result.Count);
            for (int i = projectPrefix.Count; i < projectPrefix.Count + sizeBytes.Length; i++)
                result[i] = sizeBytes[i - projectPrefix.Count];
        }

        private UInt32 SetTimelineRefs(List<PixelTimelineFile> notEmptyLines, List<byte> result)
        {
            UInt32 currentAddress = (UInt32)(result.Count + notEmptyLines.Count * sizeof(UInt32));
            List<UInt32> addresses = GetTimelineAddresses(currentAddress, notEmptyLines);
            addresses.ForEach((adr) => { result.AddRange(BitConverter.GetBytes(adr)); });

            uint lastTimelineSize = 0;
            uint lastTimelineAddress = 0;

            if (notEmptyLines.Count > 0) lastTimelineSize = notEmptyLines.Last<PixelTimelineFile>().GetSize();
            if (addresses.Count > 0) lastTimelineAddress = addresses.Last<UInt32>();
            
            return lastTimelineSize + lastTimelineAddress + 1;
        }

        private List<UInt32> GetTimelineAddresses(UInt32 currentAddress, List<PixelTimelineFile> lines)
        {
            List<UInt32> result = new List<uint>();
            for (int i = 0; i < lines.Count; i++)
            {
                result.Add(currentAddress);
                currentAddress += lines[i].GetSize() + 1;
            }
            return result;
        }

        #endregion
    }
}
