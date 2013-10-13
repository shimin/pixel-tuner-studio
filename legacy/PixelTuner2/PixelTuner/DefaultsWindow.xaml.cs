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

        byte _turnOffMenuAfter = Settings.Default.TurnOffMenuAfter;
        public byte TurnOffMenuAfter
        {
            get { return _turnOffMenuAfter; }
            set { _turnOffMenuAfter = value; RaisePropertyChanged("DontTurnOffVisibility"); RaisePropertyChanged("TurnOffVisibility"); RaisePropertyChanged("TurnOffText"); }
        }

        public Visibility DontTurnOffVisibility
        {
            get { return _turnOffMenuAfter == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; }
        }

        public Visibility TurnOffVisibility
        {
            get { return _turnOffMenuAfter == 0 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible; }
        }

        public string TurnOffText
        {
            get
            {
                return String.Format((String)Application.Current.TryFindResource("Menu_Settings_Defaults_TurnOffAfter"), _turnOffMenuAfter);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.TurnOffMenuAfter = TurnOffMenuAfter;
            Settings.Default.DefaultDuration = DefaultDuration;
            Settings.Default.DefaultStretching = Stretching;
            Settings.Default.Save();
            
            List<DeviceFeature> settings = new List<DeviceFeature>();
            settings.Add(new DeviceFeature(1, TurnOffMenuAfter));
            settings.Add(new DeviceFeature(2, Settings.Default.TurnOffAfterIdle));
            
            DeviceHelper.WriteSettings(settings);
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void numeric_KeyDown(object sender, KeyEventArgs e)
        {

        }

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
