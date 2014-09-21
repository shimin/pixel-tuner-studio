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
using System.ComponentModel;

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
            protected set { _sourceImage = value; RaisePropertyChanged("SourceImage"); ResultImage = value; }
        }

        private BitmapSource _resultImage;
        public BitmapSource ResultImage
        {
            get { return _resultImage; }
            protected set { _resultImage = value; RaisePropertyChanged("ResultImage"); }
        }

        #endregion

        public TransformPage()
        {
            DataContext = this;
            InitializeComponent();
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

        public BitmapSource Result
        {
            get { return ResultImage; }
        }

        public void ApplyChanges()
        {

        }

        public void SetSource(BitmapSource bmp)
        {
            ResultImage = SourceImage = bmp;
        }

        #endregion
    }
}
