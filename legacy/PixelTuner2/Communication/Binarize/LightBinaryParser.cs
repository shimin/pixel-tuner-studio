using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Binarize
{
    public struct ColorStruct
    {
        public byte R;
        public byte G;
        public byte B;
    }

    public class LightBinaryProject
    {
        private LightBinaryProject() { }

        public static LightBinaryProject Parse(byte[] data)
        {
            var result = new LightBinaryProject();

            result._header = ASCIIEncoding.ASCII.GetString(data, 0, 4);
            result._projectSize = BitConverter.ToUInt32(data, 4);
            result._samplesSectionAddress = BitConverter.ToUInt32(data, 8);
            result._segmentAmount = data[12];
            //result._discreteness = data[13];
            result._dataBlockSize = data[13];
            result._lastTimeStamp = BitConverter.ToUInt32(data, 14);
            result._samples = new Dictionary<UInt32, List<ColorStruct>>();
            result._sampleSize = 4 + result._dataBlockSize * result._segmentAmount;
            long actualSamplesAmount = (result._projectSize - result._samplesSectionAddress) / result._sampleSize;

            for (int i = 0; i < actualSamplesAmount; i++)
            {
                int baseAddress = (int)result._samplesSectionAddress + i * result._sampleSize;

                var key = BitConverter.ToUInt32(data, baseAddress);
                var val = new List<ColorStruct>();

                for (int idx = 0; idx < result._segmentAmount; idx++)
                {
                    var cs = new ColorStruct();
                    cs.R = data[baseAddress + 4 + result._dataBlockSize * idx];
                    cs.G = data[baseAddress + 5 + result._dataBlockSize * idx];
                    cs.B = data[baseAddress + 6 + result._dataBlockSize * idx];
                    val.Add(cs);
                }

                if (val.Count > 0) result._samples.Add(key, val);
            }

            return result;
        }

        public static LightBinaryProject Parse(List<byte> data)
        {
            return Parse(data.ToArray());
        }

        public List<ColorStruct> GetColors(TimeSpan timeStamp)
        {
            //var sampleNumber = (int)(timeStamp.TotalMilliseconds / _discreteness);
            UInt32 totalMs = (UInt32)timeStamp.TotalMilliseconds;

            if (_samples.Count == 0 || totalMs > _lastTimeStamp) return null;

            //UInt16 sampleNumber16 = (UInt16)Math.Min(UInt16.MaxValue, sampleNumber);

            if (_samples.ContainsKey(totalMs))
                return _samples[totalMs];
            else
            {
                while (!_samples.ContainsKey(totalMs) && totalMs > 0)
                {
                    totalMs--;
                }
                return _samples[totalMs];
            }
        }

        string _header;
        public string Header
        {
            get { return _header; }
        }

        UInt32 _projectSize;
        public UInt32 ProjectSize
        {
            get { return _projectSize; }
        }

        UInt32 _samplesSectionAddress;
        public UInt32 SamplesSectionAddress
        {
            get { return _samplesSectionAddress; }
        }

        byte _segmentAmount;
        public byte SegmentAmount
        {
            get { return _segmentAmount; }
        }

        byte _discreteness;
        public byte Discreteness
        {
            get { return _discreteness; }
        }

        byte _dataBlockSize;
        public byte DataBlockSize
        {
            get { return _dataBlockSize; }
        }

        UInt32 _lastTimeStamp;
        public UInt32 LastTimeStamp
        {
            get { return _lastTimeStamp; }
        }

        int _sampleSize = 0;

        Dictionary<UInt32, List<ColorStruct>> _samples;
        internal Dictionary<UInt32, List<ColorStruct>> Samples
        {
            get { return _samples; }
        }
    }
}
