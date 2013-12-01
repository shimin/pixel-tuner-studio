using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;

namespace Communication.Binarize
{
    public class LightTimelineFile
    {
        public List<LightImageFile> Images { get; set; }

        public byte SubmoduleAddress { get; set; }

        public LightTimelineFile()
        {
            Images = new List<LightImageFile>();
        }

        internal UInt32 GetSize()
        {
            return (UInt32)(LightImageFile.Size * Images.Count) + 2 * sizeof(byte);
        }

        internal List<byte> GetBinary()
        {
            var bin = new List<byte>();
            bin.Add(SubmoduleAddress);
            bin.Add((byte)Images.Count);
            foreach (var v in Images)
            {
                Color startColor = Color.Black;
                Color endColor = Color.Black;
                GetColorBounds(v, out startColor, out endColor);
                UInt32 duration = (UInt32)(((TimeSpan)(v.EndTime - v.StartTime)).TotalMilliseconds / 100);
                bin.AddRange(BitConverter.GetBytes(duration));                  // 4 bytes
                bin.Add(startColor.R);
                bin.Add(startColor.G);
                bin.Add(startColor.B);
                bin.Add(endColor.R);
                bin.Add(endColor.G);
                bin.Add(endColor.B);
            }
            return bin;
        }

        private void GetColorBounds(LightImageFile v, out Color startColor, out Color endColor)
        {
            startColor = Color.Black;
            endColor = Color.Black;

            if (Images == null || Images.Count == 0) return;
            if (v == Images.First<LightImageFile>())        // начало линии
            {
                // если цвет
                if (v.Color.HasValue)
                {
                    startColor = endColor = v.Color.Value;
                }
                else    // переход
                {       
                    // по логике, за переходом не может идти снова переход
                    if (Images.Count > 1)
                    {
                        endColor = Images[1].Color.Value;
                    }
                }
            }
            else if (v == Images.Last<LightImageFile>())    // конец линии
            {
                // если цвет
                if (v.Color.HasValue)
                {
                    startColor = endColor = v.Color.Value;
                }
                else    // если переход
                {
                    if (Images.Count > 1)
                    {
                        startColor = Images[Images.Count - 2].Color.Value;
                    }
                }
            }
            else // середина (все что не начало и не конец)
            {
                if (v.Color.HasValue)   // если цвет
                {
                    endColor = startColor = v.Color.Value;
                }
                else    // если переход
                {
                    int idx = Images.IndexOf(v);
                    startColor = Images[idx - 1].Color.Value;
                    endColor = Images[idx + 1].Color.Value;
                }
            }
        }

        internal List<byte> GetBinary(int samplesCount, int rate)
        {
            List<byte> result = new List<byte>();
            DateTime time = Images.First().StartTime;

            int currentSampleIdx = 0;
            Color lastColor = Color.Black;

            Images.ForEach((image) =>
            {
                Color startColor = Color.Black;
                Color endColor = Color.Black;
                GetColorBounds(image, out startColor, out endColor);
                while (time <= image.EndTime)
                {
                    result.AddRange(image.GetColor(time, startColor, endColor));
                    time = time.AddMilliseconds(rate);
                    currentSampleIdx++;
                }
                lastColor = endColor;
            });

            while (currentSampleIdx < samplesCount)
            {
                // возможно, если таймлайн кончился, цвет должен стать черным
                //result.AddRange(LightImageFile.ColorToBinary(lastColor));
                result.AddRange(LightImageFile.ColorToBinary(Color.Black));
                currentSampleIdx++;
            }

            return result;
        }
    }
}
