using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Gradient
{
    public class GradientProjectFile
    {
        #region Fields
        
        List<byte> projectPrefix = new List<byte>(Encoding.ASCII.GetBytes("EPIC"));

        public List<GradientTimeLineFile> TimeLines { get; set; }

        #endregion

        #region Constructor

        public GradientProjectFile()
        {
            TimeLines = new List<GradientTimeLineFile>();
        }

        #endregion

        #region Methods
        
        public int GetBinarySize(GradientsCollection images)
        {
            byte targetHeight = 1;
            int size = 0;
            var notEmptyLines = new List<GradientTimeLineFile>();
            TimeLines.ForEach((tl) => { if (tl.Images.Count > 0) notEmptyLines.Add(tl); });

            List<byte> result = CreateProjectHeader(notEmptyLines, targetHeight);
            size = (int)GenerateTimelineAddresses(notEmptyLines, result);
            size += images.GetSize(targetHeight);

            return size;
        }

        public List<byte> GetBinary(GradientsCollection images)
        {
            byte targetHeight = 1;
            var nonEmptyLines = new List<GradientTimeLineFile>();
            TimeLines.ForEach(tl => { if (tl.Images.Count > 0) nonEmptyLines.Add(tl); });

            List<byte> result = CreateProjectHeader(nonEmptyLines, targetHeight);

            var firstFreeAddress = GenerateTimelineAddresses(nonEmptyLines, result);              // timeline addresses
            images.SetBlockAddress(firstFreeAddress);
            
            nonEmptyLines.ForEach((tl) => { result.AddRange(tl.GetBinary(images)); });  // timelines data
            result.AddRange(images.GetBinary(targetHeight));                            // images data

            SetBinarySize(result);

            return result;
        }

        private List<byte> CreateProjectHeader(List<GradientTimeLineFile> timeLines, byte targetHeight)
        {
            List<byte> result = new List<byte>(projectPrefix);                          // header
            result.AddRange(new byte[4]);                                               // binary total size field
            result.Add((byte)timeLines.Count);                                          // timelines count
            return result;
        }

        /// <summary>
        /// Completes field (4 bytes) with binary size in header
        /// </summary>
        /// <param name="result">Binary to complete the field</param>
        private void SetBinarySize(List<byte> result)
        {
            UInt32 size = (UInt32)result.Count;
            byte[] sizeBytes = BitConverter.GetBytes(size);
            int projectHeaderOffset = projectPrefix.Count + sizeof(byte);
            for (int i = projectHeaderOffset; i < projectHeaderOffset + sizeBytes.Length; i++)
                result[i] = sizeBytes[i - projectHeaderOffset];
        }

        /// <summary>
        /// Generates timeline addresses, writes it to the binary and returns first available address after
        /// timeline address list section
        /// </summary>
        /// <param name="lines">Non empty timelines</param>
        /// <param name="result">Binary with complete timeline descriptors address list</param>
        /// <returns>First address after section</returns>
        private UInt32 GenerateTimelineAddresses(List<GradientTimeLineFile> lines, List<byte> result)
        {
            // increase current address by timeline addresses block
            UInt32 currentAddress = (UInt32)(result.Count + lines.Count * sizeof(UInt32));
            
            // get address of each timeline descriptor
            for (int i = 0; i < lines.Count; i++)
            {
                result.AddRange(BitConverter.GetBytes(currentAddress));
                currentAddress += lines[i].GetSize();
            }

            // return first available address after timeline addresses block
            return currentAddress;
        }

        #endregion

    }
}
