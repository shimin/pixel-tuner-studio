using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Common.Controls;
using PixelTuner.Properties;
using System.ComponentModel;
using Common.Imaging;

namespace PixelTuner.Import
{
    /// <summary>
    /// Interaction logic for CropPage.xaml
    /// </summary>
    public partial class CropPage : UserControl, INotifyPropertyChanged, IPicturePage
    {
        #region Properties
        CroppingAdorner _cropAdorner;
        public CroppingAdorner CropAdorner
        {
            get { return _cropAdorner; }
        }

        FrameworkElement _felCur = null;
        Brush _brOriginal;

        public bool ApplySharpening
        {
            get { return Settings.Default.ApplySharpening; }
            set { Settings.Default.ApplySharpening = value; }
        }

        ImageSource _imageToCrop;
        public ImageSource ImageToCrop
        {
            get { return _imageToCrop; }
            private set { _imageToCrop = value; RaisePropertyChanged("ImageToCrop"); }
        }

        private ImageSource _croppedImage;
        public ImageSource CroppedImage
        {
            get { return _croppedImage; }
        }

        private System.Drawing.Bitmap _resizedImageGDI;
        public System.Drawing.Bitmap ResizedImageGDI
        {
            get { return _resizedImageGDI; }
            private set { _resizedImageGDI = value; RaisePropertyChanged("ResizedImageGDI"); }
        }

        private BitmapSource _resizedImage;
        public BitmapSource ResizedImage
        {
            get { return _resizedImage; }
            private set { _resizedImage = value; RaisePropertyChanged("ResizedImage"); }
        }

        int _resizeHeightValue;
        public int ResizeHeightValue
        {
            get { return _resizeHeightValue; }
            set { _resizeHeightValue = value; RaisePropertyChanged("ResizeHeightValue"); }
        }
        #endregion

        public CropPage(ImageSource src)
        {
            _imageToCrop = src;
            _resizeHeightValue = Settings.Default.LineHeight;
            DataContext = this;
            InitializeComponent();
        }

        #region Methods
        private void RemoveCropFromCur()
        {
            AdornerLayer aly = AdornerLayer.GetAdornerLayer(_felCur);
            aly.Remove(_cropAdorner);
        }

        private void AddCropToElement(FrameworkElement fel)
        {
            if (_felCur != null)
            {
                RemoveCropFromCur();
            }

            Rect rcInterior = new Rect(
                fel.ActualWidth * 0.0,
                fel.ActualHeight * 0.0,
                fel.ActualWidth * 1.0,
                fel.ActualHeight * 1.0);

            AdornerLayer aly = AdornerLayer.GetAdornerLayer(fel);
            _cropAdorner = new CroppingAdorner(fel, rcInterior);
            aly.Add(_cropAdorner);
            imgCrop.Source = _cropAdorner.BpsCrop();
            _cropAdorner.CropChanged += CropChanged;
            _felCur = fel;
            SetMaskColor(Color.FromArgb(160, 0, 0, 0));
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            AddCropToElement(boxImage);
            _brOriginal = _cropAdorner.Fill;
            RefreshCropImage();
        }

        private void RefreshCropImage()
        {
            if (_cropAdorner != null)
            {
                Rect rc = _cropAdorner.ClippingRectangle;

                boxClippingRectangleInfo.Text = string.Format(
                    "Clipping Rectangle: ({0:N1}, {1:N1}, {2:N1}, {3:N1}); width: {4:N1}, height: {5:N1}",
                    rc.Left,
                    rc.Top,
                    rc.Right,
                    rc.Bottom,
                    rc.Width,
                    rc.Height);
                _croppedImage = imgCrop.Source = _cropAdorner.BpsCrop();
            }
        }

        private void CropChanged(Object sender, RoutedEventArgs rea)
        {
            RefreshCropImage();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshCropImage();
        }

        private void SetMaskColor(Color maskColor)
        {
            if (_cropAdorner != null)
            {
                _cropAdorner.Fill = new SolidColorBrush(maskColor);
            }
        }

        private bool _isMouseMoves = false;
        private Point _lastMousePosition = new Point(0, 0);
        private void boxImage_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePosition = e.GetPosition((IInputElement)sender);
            _isMouseMoves = true;
            Mouse.Capture(boxImage, CaptureMode.None);
        }

        private void boxImage_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseMoves)
            {
                var p = e.GetPosition((IInputElement)sender);
                _cropAdorner.SetPosition((p.X - _lastMousePosition.X) / 4, (p.Y - _lastMousePosition.Y) / 4);
                _lastMousePosition = p;
            }
        }

        private void boxImage_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseMoves = false;
            boxImage.ReleaseMouseCapture();
        }
        #endregion

        #region INotifyPropertyChanged

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region IPicturePage

        public BitmapSource Result
        {
            get { return ResizedImage; }
        }

        public void ApplyChanges()
        {
            var rc = _cropAdorner.ClippingRectangle;
            var srcImage = ImageHelper.BitmapImageToBitmap<BmpBitmapEncoder>((BitmapSource)ImageToCrop); //System.Drawing.Image.FromFile(_imageToCrop.LocalPath);

            int left = (int)Math.Round((rc.Left / boxImage.ActualWidth) * srcImage.Width);
            int right = (int)Math.Round((rc.Right / boxImage.ActualWidth) * srcImage.Width);
            int top = (int)Math.Round((rc.Top / boxImage.ActualHeight) * srcImage.Height);
            int bottom = (int)Math.Round((rc.Bottom / boxImage.ActualHeight) * srcImage.Height);

            var crpImage = ImageHelper.CropImage(srcImage, left, top, right, bottom);
            double aspect = (double)crpImage.Width / (double)crpImage.Height;

            ResizedImageGDI = ImageHelper.ResizeImage(crpImage, (int)Math.Round(ResizeHeightValue * aspect), ResizeHeightValue);
            ResizedImage = ImageHelper.BitmapImageFromBitmap(ResizedImageGDI);
        }

        public void SetSource(BitmapSource bmp)
        {
            boxImage.Source = bmp;
        } 

        #endregion
    }
}
