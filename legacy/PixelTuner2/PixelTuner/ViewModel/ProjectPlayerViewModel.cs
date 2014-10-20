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
                if (_displayModel != value)
                {
                    if (_displayModel != null) _displayModel.PropertyChanged -= _displayModel_PropertyChanged;

                    _displayModel = value;
                    
                    if (value != null)
                    {
                        _displayModel.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(_displayModel_PropertyChanged);
                        Image = (BitmapImage)value.ImageSource;
                        ModelStretching = value.Stretching;
                    }
                    else
                    {
                        Image = ImageHelper.BitmapImageFromBitmap(new System.Drawing.Bitmap(50, 50));
                    }
                    RaisePropertyChanged("Dash");
                    RaisePropertyChanged("Dot");
                }
            }
        }

        void _displayModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Stretching")
                ModelStretching = _displayModel.Stretching;
        }

        double _tilingX;
        public double TilingX
        {
            get { return _tilingX; }
            set { _tilingX = value + 0.272; RaisePropertyChanged("TilingX"); RaisePropertyChanged("Image"); }
        }

        public double Dash
        {
            get
            {
                double dash = 0;
                if (_displayModel != null)
                {
                    if (_displayModel.ActualHeight < 50)
                        dash = 0.01;
                    else if (_displayModel.ActualHeight < 65)
                        dash = 0.005;
                }
                
                return dash;
            }
            set { }
        }

        public double Dot
        {
            get
            {
                double dot = 0.01;
                if (_displayModel != null)
                {
                    if (_displayModel.ActualHeight < 50)
                        dot = 0.02;
                }
                return dot;
            }
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
            set { 
                if (_displayModel != null && _image != null) 
                {
                    if (_displayModel.Stretching != value) 
                        _displayModel.Stretching = value;

                    if (_displayModel.ActualHeight <= 50)
                    {
                        Stretching = TilingConverter50[Math.Max(4, Math.Min(50, value))];
                    }
                    else if (_displayModel.ActualHeight <= 70)
                    {
                        Stretching = TilingConverter65[Math.Max(4, Math.Min(50, value))];
                    }
                    else if (_displayModel.ActualHeight <= 100)
                    {
                        Stretching = TilingConverter80[Math.Max(4, Math.Min(50, value))];
                    }

                    RaisePropertyChanged("ModelStretching"); 
                } 
            }
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

        private Dictionary<int, double> TilingConverter50 = new Dictionary<int, double>()
        {
            {4, 2.16},
            {5, 1.79},
            {6, 1.42},
            {7, 1.19},
            {8, 0.96},
            {9, 0.92},
            {10 ,0.88},
            {11 ,0.82},
            {12 ,0.76},
            {13 ,0.69},
            {14 ,0.62},
            {15 ,0.57},
            {16 ,0.52},
            {17 ,0.47},
            {18 ,0.42},
            {19 ,0.40},
            {20 ,0.38},
            {21 ,0.365},
            {22 ,0.35},
            {23 ,0.335},
            {24 ,0.32},
            {25 ,0.295},
            {26 ,0.27},
            {27 ,0.25},
            {28 ,0.23},
            {29 ,0.215},
            {30 ,0.2},
            {31 ,0.185},
            {32 ,0.17},
            {33 ,0.15},
            {34 ,0.13},
            {35 ,0.12},
            {36 ,0.11},
            {37 ,0.105},
            {38 ,0.1},
            {39 ,0.09},
            {40 ,0.08},
            {41 ,0.0725},
            {42 ,0.065},
            {43 ,0.0575},
            {44 ,0.05},
            {45 ,0.0425},
            {46 ,0.035},
            {47 ,0.0275},
            {48 ,0.02},
            {49 ,0.0125},
            {50 ,0.005}
        };

        private Dictionary<int, double> TilingConverter65 = new Dictionary<int, double>()
        {
            {4, 1.19},
            {5, 1.005},
            {6, 0.82},
            {7, 0.685},
            {8, 0.58},
            {9, 0.505},
            {10, 0.46},
            {11, 0.42},
            {12, 0.38},
            {13, 0.35},
            {14, 0.32},
            {15, 0.29},
            {16, 0.26},
            {17, 0.24},
            {18, 0.22},
            {19, 0.2},
            {20, 0.185},
            {21, 0.175},
            {22, 0.165},
            {23, 0.155},
            {24, 0.14},
            {25, 0.13},
            {26, 0.12},
            {27, 0.11},
            {28, 0.1},
            {29, 0.095},
            {30, 0.09},
            {31, 0.08},
            {32, 0.07},
            {33, 0.06},
            {34, 0.05},
            {35, 0.045},
            {36, 0.042},
            {37, 0.04},
            {38, 0.035},
            {39, 0.03},
            {40, 0.025},
            {41, 0.024},
            {42, 0.023},
            {43, 0.022},
            {44, 0.02},
            {45, 0.0175},
            {46, 0.015},
            {47, 0.0125},
            {48, 0.01},
            {49, 0.0075},
            {50, 0.005}
        };

        private Dictionary<int, double> TilingConverter80 = new Dictionary<int, double>()
        {
            {4, 1.49},
            {5, 1.32},
            {6, 1.15},
            {7, 1},
            {8, 0.85},
            {9, 0.735},
            {10, 0.65},
            {11, 0.585},
            {12, 0.52},
            {13, 0.47},
            {14, 0.42},
            {15, 0.385},
            {16, 0.35},
            {17, 0.315},
            {18, 0.285},
            {19, 0.26},
            {20, 0.24},
            {21, 0.225},
            {22, 0.21},
            {23, 0.2},
            {24, 0.19},
            {25, 0.18},
            {26, 0.17},
            {27, 0.16},
            {28, 0.15},
            {29, 0.14},
            {30, 0.13},
            {31, 0.12},
            {32, 0.11},
            {33, 0.105},
            {34, 0.1},
            {35, 0.095},
            {36, 0.09},
            {37, 0.085},
            {38, 0.08},
            {39, 0.075},
            {40, 0.07},
            {41, 0.065},
            {42, 0.06},
            {43, 0.055},
            {44, 0.05},
            {45, 0.045},
            {46, 0.04},
            {47, 0.035},
            {48, 0.03},
            {49, 0.025},
            {50, 0.02}
        };
    }
}
