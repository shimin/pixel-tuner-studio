using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeLineTool;

namespace PixelTuner
{
    public class TimeLineViewModel
        : ITimeLineManager
    {
        #region ITimeLineManager Members

        public bool CanAddToTimeLine(ITimeLineDataItem item)
        {
            return true;
        }

        #endregion
    }
}
