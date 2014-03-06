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
using PixelTuner.ViewModel;

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

            RaisePropertyChanged("Stretching");
            RaisePropertyChanged("DefaultDuration");

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
            set { _stretching = value; RaisePropertyChanged("Stretching"); }
        }

        float _defaultDuration = (float)Settings.Default.DefaultDuration;
        public float DefaultDuration
        {
            get { return _defaultDuration; }
            set { _defaultDuration = value; RaisePropertyChanged("DefaultDuration"); }
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

        bool _collapseGaps = Settings.Default.CollapseGaps;
        public bool CollapseGaps
        {
            get
            {
                return _collapseGaps;
            }
            set
            {
                _collapseGaps = value;
            }
        }

        bool _exitMenuOnDoubleClick = Settings.Default.ExitMenuOnDoubleClick;
        public bool ExitMenuOnDoubleClick
        {
            get { return _exitMenuOnDoubleClick; }
            set { _exitMenuOnDoubleClick = value; }
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
            Settings.Default.ExitMenuOnDoubleClick = ExitMenuOnDoubleClick;
            Settings.Default.CollapseGaps = CollapseGaps;
            Settings.Default.Save();
            
            PixelTuner.ViewModel.RootViewModel.NotifySettingsChanged();

            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #region WriteSettingsCommand
        RelayCommand _writeSettingsCommand = null;
        public ICommand WriteSettingsCommand
        {
            get
            {
                if (_writeSettingsCommand == null)
                {
                    _writeSettingsCommand = new RelayCommand((p) => OnWriteSettings(p), (p) => CanWriteSettings(p));
                }

                return _writeSettingsCommand;
            }
        }

        private bool CanWriteSettings(object parameter)
        {
            return true;
        }

        private void OnWriteSettings(object parameter)
        {
            List<DeviceFeature> settings = new List<DeviceFeature>();
            settings.Add(new DeviceFeature(1, FadeDownMenuAfter));
            settings.Add(new DeviceFeature(2, Settings.Default.ShutdownAfterIdle));
            settings.Add(new DeviceFeature(3, (ushort)(ExitMenuOnDoubleClick ? 1 : 0)));

            DeviceHelper.WriteSettings(settings);
        }

        #endregion 

    }
}
