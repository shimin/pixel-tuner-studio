using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.ViewModel
{
    public static class TimelineHelper
    {
        public static TimeLineViewModel FindParentTimeline(FileViewModel file, TimeLineItemViewModel item)
        {
            if (file != null && item != null)
            {
                foreach (var t in file.TimeLines)
                {
                    if (t.Items.Contains(item)) return t;
                }
            }
            return null;
        }
    }
}
