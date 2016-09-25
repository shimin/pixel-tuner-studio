using System;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.IO;

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

        private ObservableCollection<Toolkit.ColorItem> _availableColors = new ObservableCollection<Toolkit.ColorItem>();
        public ObservableCollection<Toolkit.ColorItem> AvailableColors
        {
            get
            {
                return _availableColors;
            }
            set
            {
                _availableColors = value;
            }
        }

        private ObservableCollection<Toolkit.ColorItem> _colorsShort = new ObservableCollection<Toolkit.ColorItem>();
        public ObservableCollection<Toolkit.ColorItem> ColorsShort
        {
            get
            {
                return _colorsShort;
            }
            set
            {
                _colorsShort = value;
            }
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

        public void SetColorModel(bool monochrome)
        {
            AvailableColors.Clear();
            ColorsShort.Clear();

            if (monochrome)
            {
                AvailableColors = RootViewModel.ColorsMono;
                ColorsShort = RootViewModel.ColorsMonoShort;
                ColorLast = Color.FromRgb(ColorLast.R, ColorLast.R, ColorLast.R);
            }
            else
            {
                AvailableColors = RootViewModel.ColorsRgb;
                ColorsShort = RootViewModel.ColorsRgbShort;
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

        public override ReplaceItemWindow OpenReplaceWindow()
        {
            return new ReplaceItemWindow(new EffectsViewModel(
                (String)System.Windows.Application.Current.TryFindResource("Menu_Window_Effects"))
            );
        }

        public override object Clone(FileInfo fi = null)
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
