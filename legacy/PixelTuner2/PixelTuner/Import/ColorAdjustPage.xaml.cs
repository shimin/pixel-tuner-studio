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
using Common.Imaging;
using System.Collections.ObjectModel;

namespace PixelTuner.Import
{
    /// <summary>
    /// Interaction logic for ColorAdjustPage.xaml
    /// </summary>
    public partial class ColorAdjustPage : UserControl, INotifyPropertyChanged, IPicturePage
    {
        delegate void ResetDelegate();

        #region Properties

        System.Drawing.Bitmap _sourceBitmap = null;
        System.Drawing.Bitmap _currentBitmap = null;
        ImageProcessor _processor = new ImageProcessor();

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

        private ObservableCollection<HistoryEntry> _historyStack = new ObservableCollection<HistoryEntry>();
        public ObservableCollection<HistoryEntry> HistoryStack
        {
            get { return _historyStack; }
            set { _historyStack = value; RaisePropertyChanged("HistoryStack"); }
        }

        #endregion

        public ColorAdjustPage()
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
            get { return ImageHelper.BitmapImageFromBitmap(_processor.GetImage()); }
        }

        public void ApplyChanges()
        {
            ResultImage = ImageHelper.BitmapImageFromBitmap(ImageHelper.Pixelate(_processor.GetImage(), 4));
        }

        public void SetSource(BitmapSource bmp)
        {
            _sourceBitmap = ImageHelper.BitmapImageToBitmap<BmpBitmapEncoder>(bmp);
            _processor.SetImage(_sourceBitmap);

            ResetAll(true, null);
        }

        private void ResetAll(bool clearHistory, ResetDelegate excludeResetting)
        {
            if (excludeResetting != ResetBrightnessControls) ResetBrightnessControls();
            if (excludeResetting != ResetHsbControls) ResetHsbControls();
            if (excludeResetting != ResetSharpenControls) ResetSharpenControls();
            if (excludeResetting != ResetGrayscaleControls) ResetGrayscaleControls();

            if (clearHistory)
            {
                HistoryStack.Clear();
                HistoryStack.Add(new HistoryEntry(_sourceBitmap, "Open"));
                _currentBitmap = _sourceBitmap;
                var pixelated = ImageHelper.BitmapImageFromBitmap(ImageHelper.Pixelate(_currentBitmap, 4));
                ResultImage = SourceImage = pixelated;
            }
        }

        private void UpdatePreview()
        {
            if (_processor.GetImage() == null) return;
            var pixelated = ImageHelper.BitmapImageFromBitmap(ImageHelper.Pixelate(_processor.GetImage(), 4));
            ResultImage = pixelated;
        }

        #endregion

        #region Brightness/Contrast

        private void ApplyBrightnessContrast(int b, double c)
        {
            if (_currentBitmap != null)
            {
                ResetAll(false, ResetBrightnessControls);
                _processor.SetImage(_currentBitmap);
                if (boxMidtonesOnly.IsChecked == true)
                {
                    _processor.ApplyBrightness(b, 100);
                }
                else
                {
                    _processor.ApplyBrightness(b);
                }
                _processor.ApplyContrast(c);
            }
        }

        private void boxBrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyBrightnessContrast((int)boxBrightnessSlider.Value, boxContrastSlider.Value);
            UpdatePreview();
        }

        private void boxContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyBrightnessContrast((int)boxBrightnessSlider.Value, boxContrastSlider.Value);
            UpdatePreview();
        }

        private void boxApplyBC_Click(object sender, RoutedEventArgs e)
        {
            if (!IsBrightnessContrastDefault())
            {
                ApplyBrightnessContrast((int)boxBrightnessSlider.Value, boxContrastSlider.Value);
                HistoryNew("Brightness/Contrast");
                ResetBrightnessControls();
            }
        }

        private void boxCancelBC_Click(object sender, RoutedEventArgs e)
        {
            ResetBrightnessControls();
            ApplyBrightnessContrast((int)boxBrightnessSlider.Value, boxContrastSlider.Value);
            UpdatePreview();
        }

        private void ResetBrightnessControls()
        {
            boxContrastSlider.ValueChanged -= boxContrastSlider_ValueChanged;
            boxBrightnessSlider.ValueChanged -= boxBrightnessSlider_ValueChanged;

            boxBrightnessSlider.Value = 0;
            boxContrastSlider.Value = 0;

            boxContrastSlider.ValueChanged += boxContrastSlider_ValueChanged;
            boxBrightnessSlider.ValueChanged += boxBrightnessSlider_ValueChanged;
        }

        private bool IsBrightnessContrastDefault()
        {
            return boxBrightnessSlider.Value == 0 && boxContrastSlider.Value == 0;
        }

        #endregion

        #region Hue/Saturation

        private void ApplyHsb()
        {
            if (_currentBitmap != null)
            {
                ResetAll(false, ResetHsbControls);
                _processor.SetImage(_currentBitmap);
                _processor.ApplyHsbCorrection((int)boxColorHue.Value, boxColorSaturation.Value / 100.0, boxColorValue.Value / 100.0);
            }
        }

        private void boxColorHue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyHsb();
            UpdatePreview();
        }

        private void boxColorSaturation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyHsb();
            UpdatePreview();
        }

        private void boxColorValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyHsb();
            UpdatePreview();
        }

        private void boxApplyColor_Click(object sender, RoutedEventArgs e)
        {
            if (!IsHsbDefault())
            {
                ApplyHsb();
                HistoryNew("Hue/Saturation");
                ResetHsbControls();
            }
        }

        private void boxCancelColor_Click(object sender, RoutedEventArgs e)
        {
            ResetHsbControls();
            ApplyHsb();
            UpdatePreview();
        }

        private void ResetHsbControls()
        {
            boxColorHue.ValueChanged -= boxColorHue_ValueChanged;
            boxColorSaturation.ValueChanged -= boxColorSaturation_ValueChanged;
            boxColorValue.ValueChanged -= boxColorValue_ValueChanged;

            boxColorHue.Value = 0;
            boxColorSaturation.Value = 0;
            boxColorValue.Value = 0;

            boxColorValue.ValueChanged += boxColorValue_ValueChanged;
            boxColorSaturation.ValueChanged += boxColorSaturation_ValueChanged;
            boxColorHue.ValueChanged += boxColorHue_ValueChanged;
        }

        private bool IsHsbDefault()
        {
            return boxColorHue.Value == 0 && boxColorSaturation.Value == 0 && boxColorValue.Value == 0;
        }

        #endregion

        #region Sharpening

        private void ApplySharpen()
        {
            if (_currentBitmap != null)
            {
                ResetAll(false, ResetSharpenControls);
                _processor.SetImage(_currentBitmap);
                _processor.ApplyUnsharpGaussian((float)boxSharpAmount.Value);
            }
        }

        private void boxApplySharpening_Checked(object sender, RoutedEventArgs e)
        {
            ApplySharpen();
            UpdatePreview();
        }

        private void boxApplySharpening_Unchecked(object sender, RoutedEventArgs e)
        {
            ResetSharpenControls();
            _processor.SetImage(_currentBitmap);
            UpdatePreview();
        }

        private void boxSharpAmount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplySharpen();
            UpdatePreview();
        }

        private void boxApplySharpen_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSharpenDefault())
            {
                ApplySharpen();
                HistoryNew("Sharpen");
                ResetSharpenControls();
            }
        }

        private void boxCancelSharpen_Click(object sender, RoutedEventArgs e)
        {
            ResetSharpenControls();
            _processor.SetImage(_currentBitmap);
            UpdatePreview();
        }

        private void ResetSharpenControls()
        {
            boxApplySharpening.Checked -= boxApplySharpening_Checked;
            boxApplySharpening.Unchecked -= boxApplySharpening_Unchecked;
            boxSharpAmount.ValueChanged -= boxSharpAmount_ValueChanged;

            boxApplySharpening.IsChecked = false;
            boxSharpAmount.Value = 1.0;

            boxSharpAmount.ValueChanged += boxSharpAmount_ValueChanged;
            boxApplySharpening.Unchecked += boxApplySharpening_Unchecked;
            boxApplySharpening.Checked += boxApplySharpening_Checked;
        }

        private bool IsSharpenDefault()
        {
            return boxSharpAmount.Value == 1.0 && boxApplySharpening.IsChecked == false;
        }

        #endregion

        #region Grayscale/Invert

        private void ApplyMonoInvert()
        {
            if (_currentBitmap != null)
            {
                ResetAll(false, ResetGrayscaleControls);
                _processor.SetImage(_currentBitmap);
                if (boxInvert.IsChecked == true) _processor.ApplyInvert();
                if (boxMono.IsChecked == true) _processor.ApplyGreyscale();
            }
        }

        private void boxMono_Checked(object sender, RoutedEventArgs e)
        {
            ApplyMonoInvert();
            UpdatePreview();
        }

        private void boxInvert_Checked(object sender, RoutedEventArgs e)
        {
            ApplyMonoInvert();
            UpdatePreview();
        }

        private void boxApplyOther_Click(object sender, RoutedEventArgs e)
        {
            if (!IsGrayscaleDefault())
            {
                ApplyMonoInvert();
                HistoryNew("Grayscale/Invert");
                ResetGrayscaleControls();
            }
        }

        private void boxCancelOther_Click(object sender, RoutedEventArgs e)
        {
            ResetGrayscaleControls();
        }

        private bool IsGrayscaleDefault()
        {
            return boxMono.IsChecked == false && boxInvert.IsChecked == false;
        }

        private void ResetGrayscaleControls()
        {
            boxMono.Checked -= boxMono_Checked;
            boxMono.Unchecked -= boxMono_Checked;
            boxInvert.Checked -= boxInvert_Checked;
            boxInvert.Unchecked -= boxInvert_Checked;

            boxMono.IsChecked = boxInvert.IsChecked = false;

            boxInvert.Unchecked += boxInvert_Checked;
            boxInvert.Checked += boxInvert_Checked;
            boxMono.Unchecked += boxMono_Checked;
            boxMono.Checked += boxMono_Checked;
        }

        #endregion

        #region History stack

        private void HistoryNew(string note)
        {
            _currentBitmap = _processor.GetImage();
            while (boxHistoryList.SelectedIndex >= 0 && boxHistoryList.SelectedIndex < HistoryStack.Count - 1)
            {
                HistoryStack.RemoveAt(boxHistoryList.SelectedIndex + 1);
            }
            HistoryStack.Add(new HistoryEntry(_currentBitmap, note));
            boxHistoryList.SelectionChanged -= boxHistoryList_SelectionChanged;
            if (boxHistoryList.HasItems) boxHistoryList.SelectedIndex = boxHistoryList.Items.Count - 1;
            boxHistoryList.SelectionChanged += boxHistoryList_SelectionChanged;
        }

        private void boxHistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (boxHistoryList.SelectedIndex >= 0)
            {
                _currentBitmap = HistoryStack[boxHistoryList.SelectedIndex].Image;
                _processor.SetImage(_currentBitmap);
                UpdatePreview();
            }
        }

        #endregion

        private void boxSepia_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
        }

        private void imgageResult_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var img = sender as Image;
            img.Opacity = 0;
        }

        private void imgageResult_MouseLeave(object sender, MouseEventArgs e)
        {
            var img = sender as Image;
            img.Opacity = 1;
        }

        private void imgageResult_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var img = sender as Image;
            img.Opacity = 1;
        }

    }
}
