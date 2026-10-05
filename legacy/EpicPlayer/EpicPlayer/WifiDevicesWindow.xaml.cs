using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using EpicPlayer.Properties;
using EpicPlayer.ViewModel;

namespace SongReader
{
    /// <summary>
    /// Interaction logic for WifiDevicesWindow.xaml
    /// </summary>
    public partial class WifiDevicesWindow : Window, INotifyPropertyChanged
    {
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

        private UdpClient _udpSender = null;

        private IPEndPoint _broadcastEP = null;

        byte[] _packet = new byte[] { 0x49, 0x00, 0x0d };

        Dictionary<string, WifiInfoDevice> _devicesDictionary = new Dictionary<string, WifiInfoDevice>();

        StateObject state = new StateObject();

        private bool _goOn = true;

        public WifiDevicesWindow(UdpClient udpSender, IPEndPoint remoteEP)
        {
            InitializeComponent();

            if (remoteEP == null)
            {
                MessageBox.Show("No interface specified.");
                return;
            }
            _broadcastEP = remoteEP;
            _udpSender = udpSender;
            _goOn = true;
            DataContext = this;
            Closing += WifiDevicesWindow_Closing;
            Devices.CollectionChanged += (sender, args) => { OnPropertyChanged("TotalDevices"); };
            _udpSender.Send(_packet, 3, _broadcastEP);
            _udpSender.BeginReceive(new AsyncCallback(ReceivedCallback), state);
        }

        private void ReceivedCallback(IAsyncResult result)
        {
            IPEndPoint remoteEnd = new IPEndPoint(IPAddress.Any, 333);
            try
            {
                if (_udpSender != null && _udpSender.Client != null)
                {
                    byte[] received = _udpSender.EndReceive(result, ref remoteEnd);
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
                    if (_goOn) _udpSender.BeginReceive(ReceivedCallback, state);
                }
            }
            catch
            {
                if (_udpSender != null && _udpSender.Client != null && _goOn)
                    _udpSender.BeginReceive(ReceivedCallback, state);
            }
        }

        private void WifiDevicesWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _goOn = false;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void boxRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_udpSender != null)
            {
                var infoPacket = new byte[] { 0x49, 00, 0x0d };
                try
                {
                    _udpSender.Send(infoPacket, 3, _broadcastEP);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Error");
                }
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        } 

        #endregion
    }
}
