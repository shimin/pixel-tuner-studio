using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Communication.Service;
using FwUpdater.Properties;
using System.IO;

namespace FwUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private SerialDeviceBase _d = null;

        public string DeviceName { get; set; }

        public string FilePath { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Settings.Default.Reload();
            FilePath = Settings.Default.FilePath;
            DataContext = this;
            Closing += MainWindow_Closing;
            DeviceHelper.DeviceFound += DeviceHelper_DeviceFound;
            DeviceHelper.StartSeeking();
        }

        private void DeviceHelper_DeviceFound(DeviceEventArgs e)
        {
            _d = e.Device as SerialDeviceBase;
            if (_d != null)
            {
                DeviceName = _d.Name;
                OnPropertyChanged("DeviceName");
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            DeviceHelper.StopSeeking();
            _d?.Disconnect();
            Settings.Default.Save();
        }

        private void BoxUpload_OnDrop(object sender, DragEventArgs e)
        {
            string[] fileNames = (string[])e.Data.GetData(DataFormats.FileDrop, true);
            if (fileNames.Length > 0)
            {
                Settings.Default.FilePath = FilePath = fileNames[0];
                OnPropertyChanged("FilePath");
            }
        }

        private void BoxUpload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_d != null && _d.BootMode)
                {
                    if (File.Exists(FilePath))
                    {
                        byte[] data = File.ReadAllBytes(FilePath);
                        _d.WriteFirmware(new List<byte>(data));
                    }
                    else
                    {
                        MessageBox.Show(this, "File doesn't exist", "Firmware update", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show(this, "Device isn't ready", "Firmware update", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                _d.Disconnect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Firmware update", MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
