using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Windows.Media.Imaging;

namespace PixelTuner.ViewModel.Serialization
{
    public class TimeLineItemEffect : TimeLineItem
    {
        [XmlAttribute]
        public String EffectID
        {
            get;
            set;
        }

        [XmlElement]
        public TimeLineItemEffectParameters Data { get; set; }

        public static implicit operator PixelTuner.ViewModel.EffectThumbnailViewModel(TimeLineItemEffect item)
        {
            PixelTuner.ViewModel.EffectThumbnailViewModel v = null;
            switch (item.EffectID)
            {
                case "Linear Transition": v = new EffectTransitionThumbnailViewModel(GetImage(item), item.EffectID); break;
                case "Sine Wave": v = new ViewModel.EffectSineThumbnailViewModel(GetImage(item), item.EffectID); break;
                case "Square Wave" : v = new ViewModel.EffectSquareThumbnailViewModel(GetImage(item), item.EffectID); break;
                case "Saw-tooth Wave" : v = new ViewModel.EffectSawThumbnailViewModel(GetImage(item), item.EffectID); break;
                default: v = null; break;
            }
            CopyEffectData(v, item);
            v.StartTime = item.StartTime;
            v.EndTime = item.EndTime;
            //v.BackgroundColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(item.Color);
            return v;
        }

        private static void CopyEffectData(ViewModel.EffectThumbnailViewModel v, TimeLineItemEffect item)
        {
            v.ColorFirst = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(item.Data.ColorFirst);
            v.ColorLast = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(item.Data.ColorLast);
            v.T1 = item.Data.T1;
            v.T2 = item.Data.T2;
        }

        private static BitmapImage GetImage(TimeLineItemEffect item)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            int idx = effectNames.IndexOf(item.EffectID);
            image.UriSource = effectUris[idx >= 0 ? idx : 0];
            image.EndInit();
            return image;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(TimeLineItemViewModel))
            {
                return (PixelTuner.ViewModel.EffectThumbnailViewModel)this;
            }
            throw new NotImplementedException();
        }

        static List<Uri> effectUris = new List<Uri>()
        {
            new Uri("pack://application:,,,/Effects/01_effect.png"),
            new Uri("pack://application:,,,/Effects/02_effect.png"),
            new Uri("pack://application:,,,/Effects/03_effect.png"),
        };

        static List<string> effectNames = new List<string>()
        {
            "Sine Wave",
            "Square Wave",
            "Saw-tooth Wave"
        };
    }
}
