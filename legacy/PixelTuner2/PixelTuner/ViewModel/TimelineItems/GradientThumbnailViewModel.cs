using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PixelTuner.ViewModel.Serialization;

namespace PixelTuner.ViewModel
{
    public class GradientThumbnailViewModel : TimeLineItemViewModel
    {
        public override string ContentID
        {
            get
            {
                return "GradientElement";
            }
        }

        public GradientThumbnailViewModel()
            : base()
        {

        }

        public GradientThumbnailViewModel(FileInfo fi)
            : base(fi)
        {
        }

        public static implicit operator GradientTimeLineItem(GradientThumbnailViewModel item)
        {
            var result = new GradientTimeLineItem();
            result.StartTime = item.StartTime;
            result.EndTime = item.EndTime;
            result.FullName = item.FullName;
            result.Stretching = item.Stretching;
            result.Brightness = item.Brightness;
            result.LaserOn = item.LaserOn;
            return result;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(GradientTimeLineItem))
            {
                return (GradientTimeLineItem)this;
            }
            return base.ToType(conversionType, provider);
        }
    }
}
