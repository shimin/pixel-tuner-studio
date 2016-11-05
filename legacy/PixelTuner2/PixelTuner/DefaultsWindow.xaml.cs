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
using Communication;

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

            boxIP.SetIP(Settings.Default.WifiIP);
            boxMulticastIP.SetIP(Settings.Default.MulticastIP);
            boxLedsCount1.Value = Settings.Default.StripLedsCount1;
            boxLedsCount2.Value = Settings.Default.StripLedsCount2;
            boxLedsType.SelectedIndex = Settings.Default.LedsType - 1 < 0 ? 0 : Settings.Default.LedsType - 1;
            boxPass.Text = Settings.Default.WifiPass;

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
        
        int _discreteness = Settings.Default.Discreteness;
        public int Discreteness
        {
            get { return _discreteness; }
            set { _discreteness = value; }
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

        private string _ssid = Settings.Default.Ssid;

        public string Ssid
        {
            get { return _ssid; }
            set { _ssid = value; }
        }

        private string _wifiPass = Settings.Default.WifiPass;

        public string WifiPass
        {
            get { return _wifiPass; }
            set { _wifiPass = value; }
        }

        private string _wifiIP = Settings.Default.WifiIP;

        public string WifiIp
        {
            get { return _wifiIP; }
            set { _wifiIP = value; boxIP.SetIP(value); }
        }
        
        private ushort _wifiPort = Settings.Default.WifiPort;

        public ushort WifiPort
        {
            get { return _wifiPort; }
            set { _wifiPort = value; }
        }

        private string[] _supportedLedTypes = new string[] { "ws2812", "ws2801", "apa102", "lpd6803", "lpd8803", "dmx" };

        public string[] SupportedLedTypes
        {
            get { return _supportedLedTypes; }
            set { _supportedLedTypes = value; }
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
            Settings.Default.Discreteness = Discreteness;
            Settings.Default.Ssid = boxSSId.Text;
            Settings.Default.WifiPass = boxPass.Text;
            Settings.Default.WifiIP =
                $"{boxIP.FirstBox.Text}.{boxIP.SecondBox.Text}.{boxIP.ThirdBox.Text}.{boxIP.FourthBox.Text}";
            Settings.Default.MulticastIP =
                $"{boxMulticastIP.FirstBox.Text}.{boxMulticastIP.SecondBox.Text}.{boxMulticastIP.ThirdBox.Text}.{boxMulticastIP.FourthBox.Text}";
            Settings.Default.LedsType = ++boxLedsType.SelectedIndex;
            Settings.Default.StripLedsCount1 = (ushort)boxLedsCount1.Value;
            Settings.Default.StripLedsCount2 = (ushort)boxLedsCount2.Value;
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
            settings.Add(new DeviceFeature(1, BitConverter.GetBytes((ushort)FadeDownMenuAfter)));
            settings.Add(new DeviceFeature(2, BitConverter.GetBytes((ushort)Settings.Default.ShutdownAfterIdle)));
            settings.Add(new DeviceFeature(3, BitConverter.GetBytes((ushort)(ExitMenuOnDoubleClick ? 1 : 0))));

            DeviceHelper.WriteSettings<PixelDevice>(settings);
        }

        #endregion

        private void boxDoIt_Click(object sender, RoutedEventArgs e)
        {
            List<DeviceFeature> settings = new List<DeviceFeature>();
            settings.Add(new DeviceFeature(5, Encoding.ASCII.GetBytes(boxSSId.Text + "\n")));
            settings.Add(new DeviceFeature(6, Encoding.ASCII.GetBytes(boxPass.Text + "\n")));
            settings.Add(new DeviceFeature(7, boxIP.GetByteArray()));
            settings.Add(new DeviceFeature(8, BitConverter.GetBytes(WifiPort)));

            byte[] ledStripLengthParameter = new byte[4];
            BitConverter.GetBytes((ushort)boxLedsCount1.Value).CopyTo(ledStripLengthParameter, 0);
            BitConverter.GetBytes((ushort)boxLedsCount2.Value).CopyTo(ledStripLengthParameter, 2);

            settings.Add(new DeviceFeature(9, ledStripLengthParameter));
            settings.Add(new DeviceFeature(10, BitConverter.GetBytes((ushort)(boxLedsType.SelectedIndex + 1))));

            DeviceHelper.WriteSettings<VideoDevice>(settings);
        }
    }
}
