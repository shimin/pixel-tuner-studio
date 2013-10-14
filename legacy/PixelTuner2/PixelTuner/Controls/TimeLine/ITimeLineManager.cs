using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.Controls
{
	public interface ITimeLineManager
	{
		Boolean CanAddToTimeLine(ITimeLineDataItem item);
	}
}
