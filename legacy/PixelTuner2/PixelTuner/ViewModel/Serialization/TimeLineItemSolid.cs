using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace PixelTuner.ViewModel.Serialization
{
    public class TimeLineItemSolid : TimeLineItem
    {
        [XmlAttribute]
        public String Color
        {
            get;
            set;
        }

        public static implicit operator PixelTuner.ViewModel.SolidColorThumbnailViewModel(TimeLineItemSolid item)
        {
            var v = new PixelTuner.ViewModel.SolidColorThumbnailViewModel(null, item.FullName);
            v.StartTime = item.StartTime;
            v.EndTime = item.EndTime;
            v.Stretching = item.Stretching;
            v.BackgroundColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(item.Color);
            return v;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(TimeLineItemViewModel))
            {
                return (PixelTuner.ViewModel.SolidColorThumbnailViewModel)this;
            }
            throw new NotImplementedException();
        }
    }
}
