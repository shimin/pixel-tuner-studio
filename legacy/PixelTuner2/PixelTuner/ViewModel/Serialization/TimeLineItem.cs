using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace PixelTuner.ViewModel.Serialization
{
    public class TimeLineItem : IConvertible
    {
        Regex timeRx = new Regex(@"(?<h>\d{2}):(?<m>\d{2}):(?<s>\d{2})\.(?<ms>\d{3})");
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
        public bool LaserOn { get; set; }

        [XmlAttribute]
        public string Start
        {
            get
            {
                return _startTime.HasValue ? String.Format("{0}:{1}:{2}.{3}", _startTime.Value.Hour.ToString("D2"),
                    _startTime.Value.Minute.ToString("D2"), _startTime.Value.Second.ToString("D2"),
                    _startTime.Value.Millisecond.ToString("D3"))
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
                    _endTime.Value.Millisecond.ToString("D3"))
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

        int _stretching = 1;
        [XmlAttribute]
        public int Stretching
        {
            get { return _stretching; }
            set { _stretching = value; }
        }

        byte _brightness = 100;
        [XmlAttribute]
        public byte Brightness
        {
            get { return _brightness; }
            set { _brightness = value; }
        }

        public static implicit operator PixelThumbnailViewModel(TimeLineItem item)
        {
            var v = new PixelTuner.ViewModel.PixelThumbnailViewModel(new System.IO.FileInfo(item.FullName));
            v.StartTime = item.StartTime;
            v.EndTime = item.EndTime;
            v.Stretching = item.Stretching;
            v.Brightness = item.Brightness;
            v.LaserOn = item.LaserOn;
            return v;
        }

        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public byte ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public char ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public double ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public short ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public float ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public string ToString(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public virtual object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(PixelThumbnailViewModel))
            {
                return (PixelThumbnailViewModel)this;
            }
            else if (conversionType == typeof(TimeLineItemViewModel))
            {
                return (PixelThumbnailViewModel)this;
            }
            throw new NotImplementedException();
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }
    }
}