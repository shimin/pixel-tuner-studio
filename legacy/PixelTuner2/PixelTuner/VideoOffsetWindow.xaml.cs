using PixelTuner.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for VideoOffsetWindow.xaml
    /// </summary>
    public partial class VideoOffsetWindow : Window, INotifyPropertyChanged
    {
        private VideoThumbnailViewModel _thumbnail;
        public VideoThumbnailViewModel Thumbnail
        {
            get { return _thumbnail ?? (_thumbnail = new VideoThumbnailViewModel()); }
            set
            {
                if (_thumbnail != null) _thumbnail.PropertyChanged -= ThumbnailPropertyChanged;
                _thumbnail = value;
                _thumbnail.PropertyChanged += ThumbnailPropertyChanged;
                RaisePropertyChanged("Thumbnail");
                RaisePropertyChanged("MediaMargin");
                RaisePropertyChanged("MediaWidth");
                RaisePropertyChanged("MediaYScale");
            }
        }

        private void ThumbnailPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChanged("MediaMargin");
            RaisePropertyChanged("MediaWidth");
            RaisePropertyChanged("MediaYScale");
        }

        public bool MediaVisible => Thumbnail != null;

        private CanvasElementViewModel _canvasElement;
        public CanvasElementViewModel CanvasElement
        {
            get
            {
                return _canvasElement;
            }

            set
            {
                _canvasElement = value; RaisePropertyChanged("CanvasElement");
            }
        }
        
        public Thickness MediaMargin
        {
            get
            {
                double pixelSizeW = PathWidth / CanvasElement?.ColumnsCount ?? 0;
                double pixelSizeH = PathHeight / CanvasElement?.RowsCount ?? 0;
                var result = new Thickness()
                {
                    Left = -1 * Thumbnail.XOffset * pixelSizeW,
                    Top = -1 * Thumbnail.YOffset * pixelSizeH,
                    Right = -pixelSizeW * MediaNaturalWidth + PathWidth,
                    Bottom = -pixelSizeH * MediaNaturalHeight + PathHeight
                };

                return result;
            }
            set { }
        }
        
        public double MediaWidth
        {
            get
            {
                if (CanvasElement == null) return 0;
                double pixelSize = PathWidth/CanvasElement?.ColumnsCount ?? 0;
                return MediaNaturalWidth * pixelSize == 0 ? 100 : MediaNaturalWidth * pixelSize;
            }
            set { }
        }
        
        public double MediaYScale
        {
            get
            {
                double pixelSizeW = PathWidth / CanvasElement?.ColumnsCount ?? 1;
                double pixelSizeH = PathHeight / CanvasElement?.RowsCount ?? 1;
                return pixelSizeH/pixelSizeW;
            }
            set { }
        }

        public double PathWidth { get; set; }

        public double PathHeight { get; set; }

        public int MediaNaturalWidth => boxMediaElement.NaturalVideoWidth;

        public int MediaNaturalHeight => boxMediaElement.NaturalVideoHeight;

        public VideoOffsetWindow()
        {
            InitializeComponent();

            boxFigure.SizeChanged += (sender, args) =>
            {
                PathWidth = boxFigure.ActualWidth;
                PathHeight = boxFigure.ActualHeight;
                RaisePropertyChanged("MediaWidth");
                RaisePropertyChanged("MediaMargin");
                RaisePropertyChanged("MediaYScale");
            };

            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void BoxMediaElement_OnMediaOpened(object sender, RoutedEventArgs e)
        {
            RaisePropertyChanged("MediaWidth");
            RaisePropertyChanged("MediaMargin");
            RaisePropertyChanged("MediaYScale");
        }
    }
}
