using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Communication.Binarize
{
    public class LightEffectFile : LightFileBase
    {
        #region Properties

        Color _colorFirst = Color.Black;
        public Color ColorFirst
        {
            get { return _colorFirst; }
            set { _colorFirst = value; }
        }

        Color _colorLast = Color.Black;
        public Color ColorLast
        {
            get { return _colorLast; }
            set { _colorLast = value; }
        }

        int _t1 = 0;
        public int T1
        {
            get { return _t1; }
            set { _t1 = value; }
        }

        int _t2 = 0;
        public int T2
        {
            get { return _t2; }
            set { _t2 = value; }
        }

        public static int Size
        {
            get
            {
                return
                    sizeof(UInt32) + // duration
                    3 * sizeof(byte) + // step rgb
                    3 * sizeof(byte); // target color rgb
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates object
        /// </summary>
        /// <param name="name">Name of effect</param>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        public LightEffectFile(string name, DateTime startTime, DateTime endTime)
            :base(name, startTime, endTime)
        {

        }

        #endregion

        public override List<byte> GetBinary(int rate)
        {
            List<byte> result = new List<byte>();

            int dR = ColorLast.R - ColorFirst.R;
            int dG = ColorLast.G - ColorFirst.G;
            int dB = ColorLast.B - ColorFirst.B;

            DateTime time = this.StartTime;
            while (time < this.EndTime)
            {
                switch (Name)
                {
                    case "Linear Transition": result.AddRange(GetLinearTransition(time, rate, ColorFirst, ColorLast, dR, dG, dB)); break;
                    case "Sine Wave": result.AddRange(GetColorSine(time, rate, ColorFirst, ColorLast, dR, dG, dB)); break;
                    case "Square Wave": result.AddRange(GetColorSquare(time, rate, ColorFirst, ColorLast, dR, dG, dB)); break;
                    case "Saw-tooth Wave": result.AddRange(GetColorSaw(time, rate, ColorFirst, ColorLast, dR, dG, dB)); break;
                }
                time = time.AddMilliseconds(rate);
            }
            return result;
        }

        private IEnumerable<byte> GetLinearTransition(DateTime time, int rate, Color ColorFirst, Color ColorLast, int dR, int dG, int dB)
        {
            List<byte> color = new List<byte>();

            double durationMS = (EndTime - StartTime).TotalMilliseconds;
            double timepointMS = (time - StartTime).TotalMilliseconds;

            // для линейного перехода, линейная пропорция
            int xR = (int)(dR * timepointMS / durationMS);
            int xG = (int)(dG * timepointMS / durationMS);
            int xB = (int)(dB * timepointMS / durationMS);

            color.Add((byte)(ColorFirst.R + xR));
            color.Add((byte)(ColorFirst.G + xG));
            color.Add((byte)(ColorFirst.B + xB));

            return color;
        }

        private IEnumerable<byte> GetColorSaw(DateTime time, int rate, Color ColorFirst, Color ColorLast, int dR, int dG, int dB)
        {
            List<byte> color = new List<byte>();

            double t = (time - StartTime).TotalMilliseconds;
            var tMod = t % (T1 + T2);
            double xR = tMod > T1 ? Math.Atan((double)-Math.Abs(dR) / (double)T2) * (tMod - T1) + ColorLast.R : Math.Atan((double)Math.Abs(dR) / (double)T1) * tMod + ColorFirst.R;
            double xG = tMod > T1 ? Math.Atan((double)-Math.Abs(dG) / (double)T2) * (tMod - T1) + ColorLast.G : Math.Atan((double)Math.Abs(dG) / (double)T1) * tMod + ColorFirst.G;
            double xB = tMod > T1 ? Math.Atan((double)-Math.Abs(dB) / (double)T2) * (tMod - T1) + ColorLast.B : Math.Atan((double)Math.Abs(dB) / (double)T1) * tMod + ColorFirst.B;
            
            xR = xR > 255 ? 255 - (xR - 255) : Math.Abs(xR);
            xG = xG > 255 ? 255 - (xG - 255) : Math.Abs(xG);
            xB = xB > 255 ? 255 - (xB - 255) : Math.Abs(xB);
            
            color.Add((byte)xR);
            color.Add((byte)xG);
            color.Add((byte)xB);

            return color;
        }

        private IEnumerable<byte> GetColorSquare(DateTime time, int rate, Color ColorFirst, Color ColorLast, int dR, int dG, int dB)
        {
            List<byte> color = new List<byte>();

            double t = (time - StartTime).TotalMilliseconds;
            
            byte xR = t % (T1 + T2) <= T1 ? ColorFirst.R : ColorLast.R;
            byte xG = t % (T1 + T2) <= T1 ? ColorFirst.G : ColorLast.G;
            byte xB = t % (T1 + T2) <= T1 ? ColorFirst.B : ColorLast.B;

            if (T1 == 0)
            {
                xR = ColorLast.R;
                xG = ColorLast.G;
                xB = ColorLast.B;
            }
            if (T2 == 0)
            {
                xR = ColorFirst.R;
                xG = ColorFirst.G;
                xB = ColorFirst.B;
            }

            color.Add(xR);
            color.Add(xG);
            color.Add(xB);

            return color;
        }

        /// <summary>
        /// returns color by time marker
        /// </summary>
        /// <param name="time">Must be within start and end time</param>
        /// <param name="startColor">Left bound color</param>
        /// <param name="endColor">Right bound color</param>
        /// <returns></returns>
        internal virtual IEnumerable<byte> GetColorSine(DateTime time, int rate, System.Drawing.Color startColor, System.Drawing.Color endColor, int dR, int dG, int dB)
        {
            List<byte> color = new List<byte>();
            
            double t = (time - StartTime).TotalMilliseconds;
            double xR = (dR / 2) * Math.Sin(2 * Math.PI * t / T1 - Math.PI / 2) + (dR / 2) + startColor.R;
            double xG = (dG / 2) * Math.Sin(2 * Math.PI * t / T1 - Math.PI / 2) + (dG / 2) + startColor.G;
            double xB = (dB / 2) * Math.Sin(2 * Math.PI * t / T1 - Math.PI / 2) + (dB / 2) + startColor.B;

            color.Add((byte)Math.Abs(xR));
            color.Add((byte)Math.Abs(xG));
            color.Add((byte)Math.Abs(xB));

            return color;
        }
    }
}
