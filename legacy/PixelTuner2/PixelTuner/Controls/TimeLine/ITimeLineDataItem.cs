using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.Controls
{
	public interface ITimeLineDataItem
        : ICloneable
	{
		DateTime? StartTime { get; set; }
		DateTime? EndTime { get; set; }
        Boolean TimelineViewExpanded { get; set; }
        System.ComponentModel.INotifyPropertyChanged Parent { get; set; }
	}
}
