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
        public List<LightFileBase> Images { get; set; }

        public byte SubmoduleAddress { get; set; }

        public LightTimelineFile()
        {
            Images = new List<LightFileBase>();
        }

        internal UInt32 GetSize()
        {
            return (UInt32)(LightFileBase.Size * Images.Count) + 2 * sizeof(byte);
        }

        //private void GetColorBounds(LightColorFile v, out Color startColor, out Color endColor)
        //{
        //    startColor = Color.Black;
        //    endColor = Color.Black;

        //    if (Images == null || Images.Count == 0) return;
        //    if (v == Images.First<LightColorFile>())        // начало линии
        //    {
        //        // если цвет
        //        if (v.Color.HasValue)
        //        {
        //            startColor = endColor = v.Color.Value;
        //        }
        //        else    // переход
        //        {       
        //            // по логике, за переходом не может идти снова переход
        //            if (Images.Count > 1)
        //            {
        //                endColor = Images[1].Color.Value;
        //            }
        //        }
        //    }
        //    else if (v == Images.Last<LightColorFile>())    // конец линии
        //    {
        //        // если цвет
        //        if (v.Color.HasValue)
        //        {
        //            startColor = endColor = v.Color.Value;
        //        }
        //        else    // если переход
        //        {
        //            if (Images.Count > 1)
        //            {
        //                startColor = Images[Images.Count - 2].Color.Value;
        //            }
        //        }
        //    }
        //    else // середина (все что не начало и не конец)
        //    {
        //        if (v.Color.HasValue)   // если цвет
        //        {
        //            endColor = startColor = v.Color.Value;
        //        }
        //        else    // если переход
        //        {
        //            int idx = Images.IndexOf(v);
        //            startColor = Images[idx - 1].Color.Value;
        //            endColor = Images[idx + 1].Color.Value;
        //        }
        //    }
        //}

        internal List<byte> GetBinary(int samplesCount, int rate)
        {
            List<byte> result = new List<byte>();
            DateTime time = Images.First().StartTime;

            int currentSampleIdx = 0;
            Color lastColor = Color.Black;

            Images.ForEach((image) =>
            {
                result.AddRange(image.GetBinary(rate));
                //Color startColor = Color.Black;
                //Color endColor = Color.Black;
                //GetColorBounds(image, out startColor, out endColor);
                //while (time < image.EndTime)
                //{
                //    result.AddRange(image.GetColor(time, startColor, endColor));
                //    time = time.AddMilliseconds(rate);
                //    currentSampleIdx++;
                //}
                //lastColor = endColor;
            });

            while (currentSampleIdx < samplesCount)
            {
                // возможно, если таймлайн кончился, цвет должен стать черным
                //result.AddRange(LightImageFile.ColorToBinary(lastColor));
                result.AddRange(LightColorFile.ColorToBinary(Color.Black));
                currentSampleIdx++;
            }

            return result;
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine("\tline begin");
            result.AppendLine("\timages: " + this.Images.Count.ToString());
            result.AppendLine("\taddress " + this.SubmoduleAddress.ToString());
            Images.ForEach((img) => result.AppendLine(img.ToString()));
            result.AppendLine("\tline end");
            return result.ToString();
        }
    }
}
