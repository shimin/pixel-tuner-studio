using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Communication.Binarize
{
    public class LightColorFile : LightFileBase
    {
        #region Properties

        Color _color;
        public Color Color
        {
            get { return _color; }
            set { _color = value; }
        }
        
        #endregion

        #region Constructor

        /// <summary>
        /// Creates object
        /// </summary>
        /// <param name="name">Name of item</param>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        public LightColorFile(string name, DateTime startTime, DateTime endTime)
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
                result.AddRange(LightFileBase.ColorToBinary(this.Color));
                time = time.AddMilliseconds(rate);
            }
            return result;
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine("\t\tLightColorFile begin");

            result.AppendLine("\t\tName:       :" + this.Name.ToString());
            result.AppendLine("\t\tStartAddress:" + this.StartAddress.ToString("X"));

            result.AppendLine("\t\tColorFirst: " + this.Color.ToString());

            result.AppendLine("\t\tStartTime:  " + this.StartTime.ToString());
            result.AppendLine("\t\tEndTime:    " + this.EndTime.ToString());

            result.AppendLine("\t\tLightColorFile end");
            return result.ToString();
        }
    }
}
