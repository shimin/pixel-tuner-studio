using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PixelTuner.ViewModel.Serialization;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace PixelTuner.ViewModel
{
    public class EffectThumbnailViewModel : TimeLineItemViewModel
    {
        #region Properties

        private Color _colorFirst;
        public Color ColorFirst
        {
            get { return _colorFirst; }
            set { _colorFirst = value; RaisePropertyChanged("ColorFirst"); RaisePropertyChanged("ColorFirstBrush"); }
        }

        private Color _colorLast;
        public Color ColorLast
        {
            get { return _colorLast; }
            set { _colorLast = value; RaisePropertyChanged("ColorLast"); RaisePropertyChanged("ColorLastBrush"); }
        }

        public SolidColorBrush ColorFirstBrush { get { return new SolidColorBrush(ColorFirst); } }

        public SolidColorBrush ColorLastBrush { get { return new SolidColorBrush(ColorLast); } }

        public int? T1 { get; set; }

        public int? T2 { get; set; }

        public override string ContentID
        {
            get
            {
                return "EffectElement";
            }
        }

        string _name = "";
        public override string Name
        {
            get
            {
                return _name;
            }
        }

        protected string _uri;
        public override string FullName
        {
            get
            {
                return _uri;
            }
        }

        protected override bool CanThumbnail(object parameter)
        {
            return false;
        }

        #endregion

        public EffectThumbnailViewModel()
            : this(null, "")
        {

        }

        public EffectThumbnailViewModel(BitmapImage img, string fullName)
            : base()
        {
            _name = fullName;
            _image = img;
        }

        public static implicit operator PixelTuner.ViewModel.Serialization.TimeLineItemEffect(EffectThumbnailViewModel item)
        {
            var result = new PixelTuner.ViewModel.Serialization.TimeLineItemEffect();
            result.StartTime = item.StartTime;
            result.EndTime = item.EndTime;
            result.FullName = item.FullName;
            result.EffectID = item._name;
            result.Data = new PixelTuner.ViewModel.Serialization.TimeLineItemEffectParameters();
            result.Data.ColorFirst = item.ColorFirst.ToString();
            result.Data.ColorLast = item.ColorLast.ToString();
            result.Data.T1 = item.T1.HasValue ? item.T1.Value : 0;
            result.Data.T1Specified = result.Data.T1 != 0;
            result.Data.T2 = item.T2.HasValue ? item.T2.Value : 0;
            result.Data.T2Specified = result.Data.T2 != 0;
            return result;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(PixelTuner.ViewModel.Serialization.TimeLineItem))
            {
                return (PixelTuner.ViewModel.Serialization.TimeLineItemEffect)this;
            }
            throw new NotImplementedException();
        }

        public override object Clone()
        {
            EffectThumbnailViewModel result = (EffectThumbnailViewModel)base.Clone();
            result._image = this._image;
            result._name = this._name;
            result.NormalBackground = this.NormalBackground;
            return result;
        }

        public override string ToString()
        {
            return ContentID + " " + FullName;
        }
    }
}
