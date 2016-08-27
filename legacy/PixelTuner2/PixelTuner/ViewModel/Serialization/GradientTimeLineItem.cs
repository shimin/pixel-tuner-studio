using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace PixelTuner.ViewModel.Serialization
{
    public class GradientTimeLineItem : TimeLineItem
    {
        public static implicit operator PixelTuner.ViewModel.GradientThumbnailViewModel(GradientTimeLineItem item)
        {
            var v = new GradientThumbnailViewModel(new System.IO.FileInfo(item.FullName));
            v.StartTime = item.StartTime;
            v.EndTime = item.EndTime;
            v.Stretching = item.Stretching;
            v.Brightness = item.Brightness;
            v.LaserOn = item.LaserOn;

            return v;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(TimeLineItemViewModel))
            {
                return (PixelTuner.ViewModel.GradientThumbnailViewModel)this;
            }
            else if (conversionType == typeof(GradientThumbnailViewModel))
            {
                return (PixelTuner.ViewModel.GradientThumbnailViewModel)this;
            }
            throw new NotImplementedException();
        }
    }
}
