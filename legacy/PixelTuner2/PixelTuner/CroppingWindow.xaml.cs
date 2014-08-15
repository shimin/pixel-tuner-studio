using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Common.Controls;
using PixelTuner.Properties;
using System.IO;
using Common.Imaging;


namespace PixelTuner
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>

	public partial class CroppingWindow : System.Windows.Window
	{
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

        Uri _imageToCrop;
        public Uri ImageToCrop
        {
            get { return _imageToCrop; }
        }

        private ImageSource _croppedImage;
        public ImageSource CroppedImage
        {
            get { return _croppedImage; }
        }

        private System.Drawing.Image _resizedImageGDI;
        public System.Drawing.Image ResizedImageGDI
        {
            get { return _resizedImageGDI; }
        }

        int _resizeHeightValue;
        public int ResizeHeightValue
        {
            get { return _resizeHeightValue; }
            set { _resizeHeightValue = value; }
        }

        public CroppingWindow(Uri imageToCropURI )
		{
            _imageToCrop = imageToCropURI;
            _resizeHeightValue = Settings.Default.LineHeight;
            DataContext = this;
			InitializeComponent();
		}
		
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

		private void Window_Loaded(object sender, RoutedEventArgs e)
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

        private void boxOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;

            // first way
            var rc = _cropAdorner.ClippingRectangle;            
            var srcImage = System.Drawing.Image.FromFile(_imageToCrop.LocalPath);

            int left = (int)Math.Round((rc.Left / boxImage.ActualWidth) * srcImage.Width);
            int right = (int)Math.Round((rc.Right / boxImage.ActualWidth) * srcImage.Width);
            int top = (int)Math.Round((rc.Top / boxImage.ActualHeight) * srcImage.Height);
            int bottom = (int)Math.Round((rc.Bottom / boxImage.ActualHeight) * srcImage.Height);
            
            var crpImage = ImageUtilities.CropImage(srcImage, left, top, right, bottom);
            double aspect = (double)crpImage.Width / (double)crpImage.Height;
            _resizedImageGDI = ImageUtilities.ResizeImage(crpImage, (int)Math.Round(ResizeHeightValue * aspect), ResizeHeightValue);

            this.Close();
        }

        private void boxCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        #region Picture processing

        private static byte[] GetEncodedImageData(ImageSource image, string preferredFormat)
        {
            byte[] result = null;
            BitmapEncoder encoder = null;
            switch (preferredFormat.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                    encoder = new JpegBitmapEncoder();
                    break;

                case ".bmp":
                    encoder = new BmpBitmapEncoder();
                    break;

                case ".png":
                    encoder = new PngBitmapEncoder();
                    break;

                case ".tif":
                case ".tiff":
                    encoder = new TiffBitmapEncoder();
                    break;

                case ".gif":
                    encoder = new GifBitmapEncoder();
                    break;

                case ".wmp":
                    encoder = new WmpBitmapEncoder();
                    break;
            }

            if (image is BitmapSource)
            {
                MemoryStream stream = new MemoryStream();
                encoder.Frames.Add(BitmapFrame.Create(image as BitmapSource));
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                result = new byte[stream.Length];
                BinaryReader br = new BinaryReader(stream);
                br.Read(result, 0, (int)stream.Length);
                br.Close();
                stream.Close();
            }
            return result;
        }

        private static BitmapSource CreateImage(byte[] imageData, int decodePixelWidth, int decodePixelHeight)
        {
            if (imageData == null) return null;

            BitmapImage result = new BitmapImage();
            result.BeginInit();
            if (decodePixelWidth > 0)
            {
                result.DecodePixelWidth = decodePixelWidth;
            }
            if (decodePixelHeight > 0)
            {
                result.DecodePixelHeight = decodePixelHeight;
            }
            result.StreamSource = new MemoryStream(imageData);
            result.CreateOptions = BitmapCreateOptions.None;
            result.CacheOption = BitmapCacheOption.Default;
            result.EndInit();
            return result;
        }

        #endregion
	}
}