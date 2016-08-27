using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.ComponentModel;

using Common.Imaging;
using System.Windows.Documents;
using Common.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System;

namespace PixelTuner.Import
{
    /// <summary>
    /// Interaction logic for TransformPage.xaml
    /// </summary>
    public partial class TransformPage : UserControl, INotifyPropertyChanged, IPicturePage
    {
        #region Properties

        private BitmapSource _sourceImage;
        public BitmapSource SourceImage
        {
            get { return _sourceImage; }
            protected set { _sourceImage = value; RaisePropertyChanged("SourceImage"); }
        }

        private BitmapSource _resultImage;
        public BitmapSource ResultImage
        {
            get { return _resultImage; }
            protected set { _resultImage = value; RaisePropertyChanged("ResultImage"); }
        }

        private double _topOffset = 0.65;
        public double TopOffset
        {
            get
            {
                return _topOffset;
            }
            set
            {
                if (value != _topOffset)
                {
                    _topOffset = value;
                    RaisePropertyChanged("TopOffset");
                    if (_imageUpdater.IsBusy) { _imageUpdater.CancelAsync(); }
                    else if (!_imageUpdater.CancellationPending) { _imageUpdater.RunWorkerAsync(SourceImage); }
                }
            }
        }

        private double _middleOffset = 0.8;
        public double MiddleOffset
        {
            get
            {
                return _middleOffset;
            }
            set
            {
                if (value != _middleOffset)
                {
                    _middleOffset = value;
                    RaisePropertyChanged("MiddleOffset");
                    if (_imageUpdater.IsBusy) { _imageUpdater.CancelAsync(); }
                    else if (!_imageUpdater.CancellationPending) { _imageUpdater.RunWorkerAsync(SourceImage); }
                }
            }
        }

        private double _bottomOffset = 1;
        public double BottomOffset
        {
            get
            {
                return _bottomOffset;
            }
            set
            {
                if (value != _bottomOffset)
                {
                    _bottomOffset = value;
                    RaisePropertyChanged("BottomOffset");
                    if (_imageUpdater.IsBusy) { _imageUpdater.CancelAsync(); }
                    else if (!_imageUpdater.CancellationPending) { _imageUpdater.RunWorkerAsync(SourceImage); }
                }
            }
        }



        Point topLeft = new Point(0, 0);
        Point topCenter = new Point(0.5, 0);
        Point topRight = new Point(0.6, 0);
        Point centerLeft = new Point(0, 0.5);
        Point centerRight = new Point(1, 0.5);
        Point bottomLeft = new Point(0, 1);
        Point bottomCenter = new Point(0.5, 1);
        Point bottomRight = new Point(1, 1);

        BackgroundWorker _imageUpdater = new BackgroundWorker();

        #endregion

        public TransformPage(bool isPreviewOn)
        {
            _imageUpdater.WorkerSupportsCancellation = true;
            _imageUpdater.DoWork += (sender, args) =>
            {
                topRight.X = _topOffset;
                topLeft.X = 1 - _topOffset;
                centerRight.X = _middleOffset;
                centerLeft.X = 1 - _middleOffset;
                bottomRight.X = _bottomOffset;
                bottomLeft.X = 1 - _bottomOffset;

                BitmapImage bmp = args.Argument as BitmapImage;
                var imageFactory = new ImageProcessor.ImageFactory(true);
                var img = imageFactory.Load(ImageHelper.BitmapImageToBytes<BmpBitmapEncoder>(bmp)).FttImage(
                            topLeft, topCenter, topRight,
                            centerLeft, new Point(0.5, 0.5), centerRight,
                            bottomLeft, bottomCenter, bottomRight).Image;
                
                Dispatcher.Invoke(new Action(() =>
                {
                    ResultImage = ImageHelper.BitmapImageFromBitmap(img);
                }));
            };
            _imageUpdater.RunWorkerCompleted += _imageUpdater_RunWorkerCompleted;

            IsPreviewOn = isPreviewOn;
            DataContext = this;
            InitializeComponent();
        }

        private void _imageUpdater_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
                _imageUpdater.RunWorkerAsync();
        }


        #region INotifyPropertyChanged

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region IPicturePage

        bool _isPreviewOn = false;
        public bool IsPreviewOn
        {
            get { return _isPreviewOn; }
            set { _isPreviewOn = value; RaisePropertyChanged("IsPreviewOn"); }
        }

        public BitmapSource Result
        {
            get { return ResultImage; }
        }

        public void ApplyChanges()
        {

        }

        public void SetSource(BitmapSource bitmap)
        {
            if (bitmap.Height > 800)
            {
                var gdiImage = Common.Imaging.ImageHelper.BitmapImageToBitmap<BmpBitmapEncoder>(bitmap);
                gdiImage = Common.Imaging.ImageHelper.ResizeImage(gdiImage, (int)(bitmap.Width / (bitmap.Height / 800)), 800);
                bitmap = Common.Imaging.ImageHelper.BitmapImageFromBitmap(gdiImage);
            }
            ResultImage = SourceImage = bitmap;
            _imageUpdater.RunWorkerAsync(SourceImage);
        }

        #endregion
        
    }
}
