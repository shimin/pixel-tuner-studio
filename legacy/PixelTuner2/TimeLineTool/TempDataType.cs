using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeLineTool
{
	public class TempDataType : ITimeLineDataItem
	{
		public DateTime? StartTime { get; set; }
		public DateTime? EndTime { get; set; }
        public Boolean TimelineViewExpanded { get; set; }
		public String Name { get; set; }
	}
}
