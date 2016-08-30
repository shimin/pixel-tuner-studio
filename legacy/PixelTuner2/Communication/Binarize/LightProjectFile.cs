using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Binarize
{
    public class LightProjectFile
    {
        #region Fields
        
        List<byte> projectPrefix = new List<byte>(Encoding.ASCII.GetBytes("LGHT"));

        public List<LightTimelineFile> TimeLines { get; set; }

        private List<List<byte>> channels = null;

        #endregion

        #region Constructor

        public LightProjectFile()
        {
            TimeLines = new List<LightTimelineFile>();
        }

        #endregion

        #region Methods
        
        public List<byte> GetBinary(int discreteness)
        {
            // ignore empty timelines
            var notEmptyLines = new List<LightTimelineFile>();
            TimeLines.ForEach((tl) => { if (tl.Images.Count > 0) notEmptyLines.Add(tl); });

            int rate = discreteness;                                                    // time between samples (ms)
            int pointSize = 3;                                                          // size of single value (rgb)
            UInt32 samplesSectionAddress = 40;                                          // start address samples section

            List<byte> result = new List<byte>(projectPrefix);                          // header
            result.AddRange(new byte[4]);                                               // binary total size field
            result.AddRange(BitConverter.GetBytes(samplesSectionAddress));              // start address samples section
            result.Add(EstimateTimelinesCount(notEmptyLines));                          // timelines count            
            //result.Add((byte)rate);                                                   // rate
            result.Add((byte)pointSize);                                                // pointSize

            DateTime start = DateTime.MinValue.AddYears(1);
            DateTime end = GetLongestDuration(notEmptyLines);
            
            var duration = (end - start).TotalMilliseconds;
            if (duration % rate != 0) duration = (duration / rate) * (rate + 1);
            int samplesCount = (int)(duration / rate) + 1;

            channels = new List<List<byte>>();
            notEmptyLines.ForEach((tl) => { channels.Add(tl.GetBinary(samplesCount, rate)); });

            //result.AddRange(BitConverter.GetBytes((UInt16)samplesCount));             // last! sample number
            result.AddRange(BitConverter.GetBytes((UInt32)duration));                   // last sample timestamp
            UInt32 nullSize = samplesSectionAddress - (UInt32)result.Count;
            result.AddRange(new byte[nullSize]);                                        // arrange samples section

            //int newSamplesCount = ThinSamples(channels);
            List<byte> prevSample = null;
            List<byte> nextSample = null;
            for (UInt16 i = 0; i < samplesCount; i++)
            {
                nextSample = new List<byte>();
                for (int j = 0; j < channels.Count; j++)
                {
                    if (!notEmptyLines[j].IsMonochrome)
                    {
                        while (nextSample.Count % pointSize != 0) nextSample.Add(0);    // complete sample to match pointSize.
                        nextSample.AddRange(channels[j].GetRange(i * pointSize, pointSize));
                    }
                    else
                    {
                        nextSample.Add(channels[j][i * pointSize]);                     // add R value only of current monochrome line
                    }
                }
                while (nextSample.Count % pointSize != 0) nextSample.Add(0);            // complete sample to match pointSize.

                // filter samples to store only non-repeating sets
                if (prevSample == null || !Equals(prevSample, nextSample) || i == samplesCount - 1)
                {
                    //result.AddRange(BitConverter.GetBytes(i));                        // sample number 2 bytes
                    result.AddRange(BitConverter.GetBytes((UInt32)(i * rate)));         // sample timestamp 4 bytes
                    result.AddRange(nextSample);                                        // sample data
                }
                else
                {
                    //Console.WriteLine("Skipped sample: " + i.ToString());
                }
                prevSample = nextSample;
            }

            SetBinarySize(result);
            return result;
        }

        private byte EstimateTimelinesCount(List<LightTimelineFile> notEmptyLines)
        {
            byte result = 0;
            int monochromeLines = 0;
            for (int i = 0; i < notEmptyLines.Count; i++)
            {
                if (notEmptyLines[i].IsMonochrome)
                {
                    monochromeLines++;
                    if (monochromeLines % 3 == 0) result++;
                }
                else
                {
                    if (monochromeLines % 3 != 0)
                    {
                        result++;
                        monochromeLines = 0;
                    }
                    result++;
                }
            }
            if (monochromeLines != 0) result++;
            return result;
        }

        private bool Equals(List<byte> list1, List<byte> list2)
        {
            if (list1.Count != list2.Count) return false;
            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i] != list2[i]) return false;
            }
            return true;
        }

        private DateTime GetLongestDuration(List<LightTimelineFile> notEmptyLines)
        {
            DateTime result = DateTime.MinValue.AddYears(1);
            if (notEmptyLines.Count > 0)
            {
                result = notEmptyLines.FirstOrDefault<LightTimelineFile>().Images.LastOrDefault<LightFileBase>().EndTime;
                notEmptyLines.ForEach((tl) => { if (tl.Images.Last<LightFileBase>().EndTime > result) result = tl.Images.Last<LightFileBase>().EndTime; });
            }
            return result;
        }

        private void SetBinarySize(List<byte> result)
        {
            byte[] sizeBytes = BitConverter.GetBytes(result.Count);
            for (int i = projectPrefix.Count; i < projectPrefix.Count + sizeBytes.Length; i++)
                result[i] = sizeBytes[i - projectPrefix.Count];
        }

        private UInt32 SetTimelineRefs(List<LightTimelineFile> notEmptyLines, List<byte> result)
        {
            UInt32 currentAddress = (UInt32)(result.Count + notEmptyLines.Count * sizeof(UInt32));
            List<UInt32> addresses = GetTimelineAddresses(currentAddress, notEmptyLines);
            addresses.ForEach((adr) => { result.AddRange(BitConverter.GetBytes(adr)); });

            uint lastTimelineSize = 0;
            uint lastTimelineAddress = 0;

            if (notEmptyLines.Count > 0) lastTimelineSize = notEmptyLines.Last<LightTimelineFile>().GetSize();
            if (addresses.Count > 0) lastTimelineAddress = addresses.Last<UInt32>();
            
            return lastTimelineSize + lastTimelineAddress + 1;
        }

        private List<UInt32> GetTimelineAddresses(UInt32 currentAddress, List<LightTimelineFile> lines)
        {
            List<UInt32> result = new List<uint>();
            for (int i = 0; i < lines.Count; i++)
            {
                result.Add(currentAddress);
                currentAddress += lines[i].GetSize();
            }
            return result;
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine("LightProject");
            TimeLines.ForEach((l) => result.AppendLine(l.ToString()));
            return result.ToString();
        }

        #endregion
    }
}
