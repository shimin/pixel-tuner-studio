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
        {

        }

        public GradientThumbnailViewModel(FileInfo fi)
            : base(fi)
        {
        }

        public static implicit operator TimeLineItemGradient(GradientThumbnailViewModel item)
        {
            var result = new TimeLineItemGradient();
            result.StartTime = item.StartTime;
            result.EndTime = item.EndTime;
            result.FullName = item.UseFullName ? item.FullName : Path.Combine("./", item.Name);
            result.Stretching = item.Stretching;
            result.Brightness = item.Brightness;
            result.LaserOn = item.LaserOn;
            return result;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(Serialization.TimeLineItem))
            {
                return (TimeLineItemGradient)this;
            }
            return base.ToType(conversionType, provider);
        }

        public override ReplaceItemWindow OpenReplaceWindow()
        {
            return new ReplaceItemWindow(new GradientsViewModel(
                (String)System.Windows.Application.Current.TryFindResource("Menu_Window_Gradients"))
            );
        }

        public override object Clone(FileInfo fi = null)
        {
            return (GradientThumbnailViewModel)base.Clone(fi);
        }
    }
}
