using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using Common.Imaging;
using System.Drawing;

namespace PixelTuner.ViewModel
{
    public class ProjectPlayerViewModel : ToolViewModel
    {
        public ProjectPlayerViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
            _tilingX = 1;
        }

        public const string ToolContentId = "ProjectPlayerTool";

        private int _blackFieldHeight = 40;

        BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
            protected set
            {
                _image = FormatImage(value);
                TilingX = ((double)400 / (double)_image.Width) * Stretching;
                RaisePropertyChanged("Image");
            }
        }

        TimeLineItemViewModel _displayModel;
        public TimeLineItemViewModel DisplayModel
        {
            get { return _displayModel; }
            set
            {
                _displayModel = value;
                ModelStretching = _displayModel.Stretching;
                Image = (BitmapImage)value.ImageSource;                
            }
        }

        double _tilingX;
        public double TilingX
        {
            get { return _tilingX; }
            set { _tilingX = value + 0.272; RaisePropertyChanged("TilingX"); RaisePropertyChanged("Image"); }
        }

        double _stretching = 1.0;
        public double Stretching
        {
            get { return _stretching; }
            set
            {
                _stretching = value;
                RaisePropertyChanged("Stretching");
                TilingX = ((double)400 / (double)_image.Width) * Stretching;
            }
        }

        public int ModelStretching
        {
            get { return _displayModel == null ? 0 : _displayModel.Stretching; }
            set { if (_displayModel != null) { _displayModel.Stretching = value; RaisePropertyChanged("ModelStretching"); } }
        }

        private BitmapImage FormatImage(BitmapImage value)
        {
            BitmapSource rotated = new TransformedBitmap(value, new System.Windows.Media.RotateTransform(180));
            
            var bmp = ImageHelper.BitmapImageToBitmap<BmpBitmapEncoder>(rotated);
            
            Bitmap bg = new Bitmap(bmp.Width, bmp.Height + _blackFieldHeight + 1);
            using (Graphics g = Graphics.FromImage(bg))
            {
                g.DrawImage(bmp, new Point(0, _blackFieldHeight));
                //int totalWidth = 0;
                //do
                //{
                //    g.DrawImage(bmp, new Point(totalWidth, _fieldWidth));
                //    totalWidth += bmp.Width;
                //} 
                //while (totalWidth <= bg.Width);
            }
            var result = (BitmapImage)ImageHelper.BitmapImageFromBitmap(bg);
            return result;
        }
    }
}
