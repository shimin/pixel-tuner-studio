using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Communication.Binarize
{
    public class LightTransitionFile : LightFileBase
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

        #endregion

        #region Constructor

        /// <summary>
        /// Creates object
        /// </summary>
        /// <param name="name">Name of transition</param>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        public LightTransitionFile(string name, DateTime startTime, DateTime endTime)
            :base(name, startTime, endTime)
        {

        }

        #endregion

        public override List<byte> GetBinary(int rate)
        {
            List<byte> result = new List<byte>();

            DateTime time = this.StartTime;
            while (time < this.EndTime)
            {
                result.AddRange(GetColor(time, this.ColorFirst, this.ColorLast));
                time = time.AddMilliseconds(rate);
            }
            return result;
        }

        /// <summary>
        /// returns color by time marker
        /// </summary>
        /// <param name="time">Must be within start and end time</param>
        /// <param name="startColor">Left bound color</param>
        /// <param name="endColor">Right bound color</param>
        /// <returns></returns>
        internal virtual IEnumerable<byte> GetColor(DateTime time, System.Drawing.Color startColor, System.Drawing.Color endColor)
        {
            List<byte> color = new List<byte>();
            int dR = endColor.R - startColor.R;
            int dG = endColor.G - startColor.G;
            int dB = endColor.B - startColor.B;

            double durationMS = (EndTime - StartTime).TotalMilliseconds;
            double timepointMS = (time - StartTime).TotalMilliseconds;

            // для линейного перехода, линейная пропорция
            int xR = (int)(dR * timepointMS / durationMS);
            int xG = (int)(dG * timepointMS / durationMS);
            int xB = (int)(dB * timepointMS / durationMS);

            color.Add((byte)(startColor.R + xR));
            color.Add((byte)(startColor.G + xG));
            color.Add((byte)(startColor.B + xB));

            return color;
        }
    }
}
