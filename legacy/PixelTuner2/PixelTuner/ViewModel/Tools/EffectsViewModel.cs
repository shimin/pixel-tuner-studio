using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.IO;

namespace PixelTuner.ViewModel
{
    public class EffectsViewModel : ToolViewModel
    {
        public const string ToolContentId = "EffectsTool";

        public EffectsViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
            _images.Add(new SolidColorThumbnailViewModel(new BitmapImage(new Uri("pack://application:,,,/Effects/04_solid.png")), "Solid Color"));
            _images.Add(new EffectTransitionThumbnailViewModel(FetchBitmap(effectUris[0]), effectNames[0]));
            _images.Add(new EffectSineThumbnailViewModel(FetchBitmap(effectUris[1]), effectNames[1]));
            _images.Add(new EffectSquareThumbnailViewModel(FetchBitmap(effectUris[2]), effectNames[2]));
            _images.Add(new EffectSawThumbnailViewModel(FetchBitmap(effectUris[3]), effectNames[3]));
        }

        private BitmapImage FetchBitmap(Uri uriSource)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = uriSource;
            image.EndInit();
            return image;
        }

        List<Uri> effectUris = new List<Uri>()
        {
            new Uri("pack://application:,,,/Effects/00_effect.png"),
            new Uri("pack://application:,,,/Effects/01_effect.png"),
            new Uri("pack://application:,,,/Effects/02_effect.png"),
            new Uri("pack://application:,,,/Effects/03_effect.png"),
        };

        List<string> effectNames = new List<string>()
        {
            "Linear Transition",
            "Sine Wave",
            "Square Wave",
            "Saw-tooth Wave"
        };

        List<Uri> filterUris = new List<Uri>()
        {
            new Uri("pack://application:,,,/Filters/02_filter.png"),
            new Uri("pack://application:,,,/Filters/03_filter.png"),
            new Uri("pack://application:,,,/Filters/04_filter.png"),
            new Uri("pack://application:,,,/Filters/05_filter.png"),
        };

        List<string> filterNames = new List<string>()
        {
            "Cross Linear",
            "Cross Curve",
            "Cross LeftWeight",
            "Cross RightWeight"
        };

        ObservableCollection<TimeLineItemViewModel> _images = new ObservableCollection<TimeLineItemViewModel>();
        ReadOnlyObservableCollection<TimeLineItemViewModel> _readonyImagesCollection = null;
        public ReadOnlyObservableCollection<TimeLineItemViewModel> ImagesCollection
        {
            get
            {
                if (_readonyImagesCollection == null)
                    _readonyImagesCollection = new ReadOnlyObservableCollection<TimeLineItemViewModel>(_images);

                return _readonyImagesCollection;
            }
        }

        TimeLineItemViewModel _selectedItem = null;
        public TimeLineItemViewModel SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                _selectedItem = value;
                RaisePropertyChanged("IsSomethingSelected");
            }
        }

    }
}
