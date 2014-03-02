using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Workspace.Themes.Default.ListView;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace PixelTuner.Serialization
{
    public class TimeLineItem
    {
        Regex timeRx = new Regex(@"(?<h>\d{2}):(?<m>\d{2}):(?<s>\d{2})\.(?<ms>\d{4})");
        public TimeLineItem()
        {

        }

        DateTime? _startTime = DateTime.MinValue;
        [XmlIgnore]
        public DateTime? StartTime
        {
            get { return _startTime; }
            set
            {
                _startTime = value;
            }
        }

        [XmlAttribute]
        public string Start
        {
            get
            {
                return _startTime.HasValue ? String.Format("{0}:{1}:{2}.{3}", _startTime.Value.Hour.ToString("D2"),
                    _startTime.Value.Minute.ToString("D2"), _startTime.Value.Second.ToString("D2"),
                    _startTime.Value.Millisecond.ToString("D4"))
                    : "";
            }
            set
            {
                DateTime _time = DateTime.MinValue;
                Match m = timeRx.Match(value);
                if (m.Groups.Count > 0)
                {
                    _time = _time.AddHours(int.Parse(m.Groups["h"].ToString()));
                    _time = _time.AddMinutes(int.Parse(m.Groups["m"].ToString()));
                    _time = _time.AddSeconds(int.Parse(m.Groups["s"].ToString()));
                    _time = _time.AddMilliseconds(int.Parse(m.Groups["ms"].ToString()));
                }
                var timeString = value;
                _time = _time.AddYears(1);
                _startTime = _time;
            }
        }

        DateTime? _endTime = null;
        [XmlIgnore]
        public DateTime? EndTime
        {
            get
            {
                return _endTime;
            }
            set
            {
                _endTime = value;
            }
        }

        [XmlAttribute]
        public string End
        {
            get
            {
                return _endTime.HasValue ? String.Format("{0}:{1}:{2}.{3}", _endTime.Value.Hour.ToString("D2"),
                    _endTime.Value.Minute.ToString("D2"), _endTime.Value.Second.ToString("D2"),
                    _endTime.Value.Millisecond.ToString("D4"))
                    : "";
            }
            set
            {
                DateTime _time = DateTime.MinValue;
                Match m = timeRx.Match(value);
                if (m.Groups.Count > 0)
                {
                    _time = _time.AddHours(int.Parse(m.Groups["h"].ToString()));
                    _time = _time.AddMinutes(int.Parse(m.Groups["m"].ToString()));
                    _time = _time.AddSeconds(int.Parse(m.Groups["s"].ToString()));
                    _time = _time.AddMilliseconds(int.Parse(m.Groups["ms"].ToString()));
                }
                var timeString = value;
                _time = _time.AddYears(1);
                _endTime = _time;
            }
        }

        bool _viewExpanded = false;
        [XmlAttribute]
        public bool TimelineViewExpanded
        {
            get
            {
                return _viewExpanded;
            }
            set
            {
                _viewExpanded = value;
            }
        }

        string _fullName;
        [XmlAttribute]
        public virtual string FullName
        {
            get
            {
                return _fullName;
            }
            set
            {
                _fullName = value;
            }
        }

        public static implicit operator ThumbnailFileItem(TimeLineItem item)
        {
            var v = new Workspace.Themes.Default.ListView.ThumbnailFileItem(new System.IO.FileInfo(item.FullName));
            v.StartTime = item.StartTime;
            v.EndTime = item.EndTime;
            return v;
        }
    }
}