using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace PixelTuner.ViewModel.Serialization
{
    public class TimeLineItemVideo : TimeLineItem
    {
        private int _xOffset;
        [XmlAttribute]
        public int XOffset
        {
            get { return _xOffset; }
            set { _xOffset = value; }
        }

        private int _yOffset;
        [XmlAttribute]
        public int YOffset
        {
            get { return _yOffset; }
            set { _yOffset = value; }
        }

        public static implicit operator PixelTuner.ViewModel.VideoThumbnailViewModel(TimeLineItemVideo item)
        {
            var v = new VideoThumbnailViewModel(new System.IO.FileInfo(item.FullName));
            v.StartTime = item.StartTime;
            v.EndTime = item.EndTime;
            v.Stretching = item.Stretching;
            v.Brightness = item.Brightness;
            v.LaserOn = item.LaserOn;
            v.XOffset = item.XOffset;
            v.YOffset = item.YOffset;
            return v;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(TimeLineItemViewModel))
            {
                return (PixelTuner.ViewModel.VideoThumbnailViewModel)this;
            }
            else if (conversionType == typeof(VideoThumbnailViewModel))
            {
                return (PixelTuner.ViewModel.VideoThumbnailViewModel)this;
            }
            throw new NotImplementedException();
        }
    }
}
