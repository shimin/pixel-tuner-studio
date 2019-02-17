using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PixelTuner.Annotations;
using PixelTuner.Properties;
using PixelTuner.ViewModel;
using PixelTuner.ViewModel.Devices;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for WifiDevicesWindow.xaml
    /// </summary>
    public partial class WifiDevicesWindow : Window, INotifyPropertyChanged
    {
        // State object for receiving data from remote device.
        public class StateObject
        {
            // Client socket.
            public Socket workSocket = null;
            // Size of receive buffer.
            public const int BufferSize = 256;
            // Receive buffer.
            public byte[] buffer = new byte[BufferSize];

            public byte[] GetData()
            {
                if (buffer == null || buffer.Length < 29) return new byte[] {};
                else
                {
                    var result = new byte[buffer.Length - 28];
                    for (int i = 28; i < buffer.Length; i++)
                        result[i - 28] = buffer[i];
                    return result;
                }
            }
            // Received data string.
            public StringBuilder sb = new StringBuilder();

            public Dictionary<string, WifiInfoDevice> DevicesDictionary = new Dictionary<string, WifiInfoDevice>();

            public ObservableCollection<WifiInfoDevice> Devices = new ObservableCollection<WifiInfoDevice>();

            public bool Continue = true;
        }

        public ObservableCollection<WifiInfoDevice> _devices = new ObservableCollection<WifiInfoDevice>();

        public ObservableCollection<WifiInfoDevice> Devices
        {
            get { return _devices; }
            set { _devices = value; }
        }
        
        public string TotalDevices
        {
            get { return $@"Total: {Devices.Count}"; }
            set { OnPropertyChanged("TotalDevices"); }
        }

        //private Socket _client = null;
        private UdpClient _udpListener = null;

        private UdpClient _udpSender = null;

        private IPEndPoint _broadcastEP = null;

        byte[] _packet = new byte[] { 0x49, 0x00, 0x0d };

        Dictionary<string, WifiInfoDevice> _devicesDictionary = new Dictionary<string, WifiInfoDevice>();

        StateObject state = new StateObject();

        private bool _goOn = true;

        public WifiDevicesWindow(UdpClient udpSender)
        {
            _udpSender = udpSender;
            _udpListener = new UdpClient(Settings.Default.WifiPort);
            _goOn = true;
            InitializeComponent();
            DataContext = this;
            Closing += WifiDevicesWindow_Closing;
            Devices.CollectionChanged += (sender, args) => { OnPropertyChanged("TotalDevices"); };
            _udpListener.BeginReceive(new AsyncCallback(ReceivedCallback), state);
            _udpSender.Send(_packet, 3);
        }

        private void ReceivedCallback(IAsyncResult result)
        {
            IPEndPoint remoteEnd = new IPEndPoint(IPAddress.Any, 333);
            try
            {
                if (_udpListener != null && _udpListener.Client != null)
                {
                    byte[] received = _udpListener.EndReceive(result, ref remoteEnd);
                    var ipString = remoteEnd.ToString();

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (_devicesDictionary.ContainsKey(ipString))
                        {
                            _devicesDictionary[ipString].RawData = received;
                        }
                        else
                        {
                            var d = new WifiInfoDevice(remoteEnd.Address.ToString(), Settings.Default.WifiPort);
                            if (received[0] == 0x49 && received.Length > 3)
                            {
                                d.RawData = received;
                                _devicesDictionary.Add(ipString, d);
                                _devices.Add(d);
                            }
                        }
                    }));
                    if (_goOn) _udpListener.BeginReceive(ReceivedCallback, state);
                }
            }
            catch
            {
                if (_udpListener != null && _udpListener.Client != null && _goOn)
                    _udpListener.BeginReceive(ReceivedCallback, state);
            }
        }

        private void WifiDevicesWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _goOn = false;
            _udpListener.Close();
            _udpListener = null;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void boxRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_udpSender != null)
            {
                try
                {
                    _udpSender.Send(_packet, 3);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Error");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
