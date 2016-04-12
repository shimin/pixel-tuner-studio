using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Input;

using Common.Controls;

namespace PixelTuner.ViewModel
{
    public class SolidColorThumbnailViewModel : TimeLineItemViewModel
    {
        #region Properties

        public override string ContentID
        {
            get
            {
                return "SolidColorElement";
            }
        }

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

        //Color _bgColor = Color.FromRgb(0, 0, 0);
        public Color BackgroundColor
        {
            get { return BackgroundBrush.Color; }
            set
            {
                //_bgColor = value; 
                ColorFirst = ColorLast = value;
                BackgroundBrush = new SolidColorBrush(value);
                RaisePropertyChanged("BackgroundColor"); RaisePropertyChanged("BackgroundBrush");
            }
        }        

        public System.Windows.Media.SolidColorBrush BackgroundBrush
        {
            get;
            set;
        }

        string _name = "";
        public override string Name
        {
            get
            {
                return _name;
            }
        }

        protected string _uri = "pack://application:,,,/Effects/04_solid.png";
        public override string FullName
        {
            get
            {
                return _uri;
            }
        }

        #endregion

        public SolidColorThumbnailViewModel()
            : this(null, "")
        {

        }

        public SolidColorThumbnailViewModel(BitmapImage img, string fullName)
            : base()
        {
            _name = fullName;
            _image = img;
            BackgroundColor = Color.FromRgb(0, 0, 0);
            BackgroundBrush = new System.Windows.Media.SolidColorBrush(BackgroundColor);
        }

        public override double ColorLayerOpacity
        {
            get
            {
                return 1.0;
            }
        }

        public override bool UseColorLayer
        {
            get
            {
                return true;
            }
        }

        public override bool CanRepeat(object parameter)
        {
            return false;
        }

        public override bool CanThumbnail(object parameter)
        {
            return true;
        }

        protected override void OnThumbnail(object parameter)
        {
            ProprietaryEdit(parameter);
        }

        public override bool CanSwitchLaserOn(object parameter)
        {
            return base.CanSwitchLaserOn(parameter);
        }

        protected override void ProprietaryEdit(object parameter)
        {
            var cd = new System.Windows.Forms.ColorDialog();
            var dr = cd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                BackgroundColor = Color.FromRgb(cd.Color.R, cd.Color.G, cd.Color.B);
                if (BackgroundColor != BackgroundBrush.Color)
                {
                    BackgroundBrush = new SolidColorBrush(BackgroundColor);
                    RaisePropertyChanged("BackgroundBrush");
                }
            }
        }

        protected override void OnDisposeMe(object parameter)
        {
            base.OnDisposeMe(parameter);
        }

        public static implicit operator PixelTuner.ViewModel.Serialization.TimeLineItemSolid(SolidColorThumbnailViewModel item)
        {
            var result = new PixelTuner.ViewModel.Serialization.TimeLineItemSolid();
            result.StartTime = item.StartTime;
            result.EndTime = item.EndTime;
            result.FullName = item.FullName;
            result.Stretching = item.Stretching;
            result.Color = item.BackgroundColor.ToString();
            return result;
        }

        public override object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(PixelTuner.ViewModel.Serialization.TimeLineItem))
            {
                return (PixelTuner.ViewModel.Serialization.TimeLineItemSolid)this;
            }
            throw new NotImplementedException();
        }

        public override object Clone()
        {
            SolidColorThumbnailViewModel result = (SolidColorThumbnailViewModel)base.Clone();
            result._image = this._image;
            result._name = this._name;
            result.BackgroundBrush = this.BackgroundBrush;
            return result;
        }

        public override string ToString()
        {
            return ContentID + " " + FullName;
        }
    }
}
