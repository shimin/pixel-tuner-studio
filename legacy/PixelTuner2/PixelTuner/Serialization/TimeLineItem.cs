using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeLineTool;
using System.Xml.Serialization;

namespace PixelTuner.Serialization
{
    public class TimeLineItem
    {
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
                var timeString = value;
                DateTime _time = DateTime.MinValue;
                DateTime.TryParse(value, out _time);
                _time.AddYears(1);
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
                var timeString = value;
                DateTime _time = DateTime.MinValue;
                DateTime.TryParse(value, out _time);
                _time.AddYears(1);
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
    }
}