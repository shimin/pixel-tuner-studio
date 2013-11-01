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
using System.Windows.Shapes;
using PixelTuner.Properties;
using System.ComponentModel;
using Communication.Service;
using Communication.Pixelate;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for DefaultsWindow.xaml
    /// </summary>
    public partial class DefaultsWindow : Window, INotifyPropertyChanged
    {
        public DefaultsWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            var depth = ColorDepth.Bit24;
            try { 
                depth = (ColorDepth)ColorDepth.Parse(typeof(ColorDepth), Settings.Default.ColorDepth); 
                Compression = (int)depth;
                RaisePropertyChanged("ColorDepthInt");
            }
            catch { }
        }

        int _stretching = Settings.Default.DefaultStretching;
        public int Stretching
        {
            get { return _stretching; }
            set { _stretching = value; }
        }

        int _defaultDuration = (int)Settings.Default.DefaultDuration;
        public int DefaultDuration
        {
            get { return _defaultDuration; }
            set { _defaultDuration = value; }
        }

        bool _useTimeSpan = Settings.Default.TimeSpanMode;
        public bool UseTimeSpan
        {
            get
            {
                return _useTimeSpan;
            }
            set
            {
                _useTimeSpan = value;
            }
        }

        byte _fadeDownMenuAfter = Settings.Default.FadeDownMenuAfter;
        public byte FadeDownMenuAfter
        {
            get { return _fadeDownMenuAfter; }
            set { _fadeDownMenuAfter = value; RaisePropertyChanged("DontFadeDownVisibility"); RaisePropertyChanged("FadeDownVisibility"); RaisePropertyChanged("FadeDownText"); }
        }

        public Visibility DontFadeDownVisibility
        {
            get { return _fadeDownMenuAfter == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; }
        }

        public Visibility FadeDownVisibility
        {
            get { return _fadeDownMenuAfter == 0 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible; }
        }

        public string FadeDownText
        {
            get
            {
                return String.Format((String)Application.Current.TryFindResource("Menu_Settings_Defaults_TurnOffAfter"), _fadeDownMenuAfter);
            }
        }

        int _compression = 1;
        public int Compression
        {
            get { return _compression; }
            set { _compression = value; RaisePropertyChanged("Compression"); RaisePropertyChanged("CompressionText"); }
        }

        public string CompressionText
        {
            get
            {
                string text = "";
                switch (Compression)
                {
                    case 0: text = (string)Application.Current.TryFindResource("Menu_Settings_Defaults_CompressionHigh");  break;
                    case 1: text = (string)Application.Current.TryFindResource("Menu_Settings_Defaults_CompressionMedium"); break;
                    case 2: text = (string)Application.Current.TryFindResource("Menu_Settings_Defaults_CompressionNone"); break;
                }
                return text;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.FadeDownMenuAfter = FadeDownMenuAfter;
            Settings.Default.DefaultDuration = DefaultDuration;
            Settings.Default.DefaultStretching = Stretching;
            Settings.Default.TimeSpanMode = UseTimeSpan;
            Settings.Default.ColorDepth = ((ColorDepth)Compression).ToString();
            Settings.Default.Save();
            
            PixelTuner.ViewModel.RootViewModel.NotifySettingsChanged();

            List<DeviceFeature> settings = new List<DeviceFeature>();
            settings.Add(new DeviceFeature(1, FadeDownMenuAfter));
            settings.Add(new DeviceFeature(2, Settings.Default.ShutdownAfterIdle));
            
            DeviceHelper.WriteSettings(settings);
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void numeric_KeyDown(object sender, KeyEventArgs e)
        {
            var c = sender as PixelTuner.Controls.NumericUpDownControl;
            if (c != null && e.Key == Key.Up)
            {
                c.Value = c.Value < c.MaxValue ? c.Value + 1 : c.MaxValue;
            }
            if (c != null && e.Key == Key.Down)
            {
                c.Value = c.Value > c.MinValue ? c.Value - 1 : c.MinValue;
            }
        }

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

    }
}
