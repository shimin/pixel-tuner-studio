using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.Service
{
    public class ProjectFile
    {
        List<byte> projectPrefix = new List<byte>( Encoding.ASCII.GetBytes("FLSH") );
        
        byte _timeLineCount = 0;
        /// <summary>
        /// Число временных линий в проекте
        /// </summary>
        public byte TimeLineCount
        {
            get { return _timeLineCount; }
            set { _timeLineCount = value; }
        }

        


    }
}
