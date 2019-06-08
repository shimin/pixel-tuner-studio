using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using Common.Collections;
using System.Net.Sockets;
using PixelTuner.ViewModel.Devices;
using System.Collections.ObjectModel;
using System.Net;
using PixelTuner.Properties;
using PixelTuner.ViewModel.Network;
using System.Net.NetworkInformation;
using System.Windows.Input;

namespace PixelTuner.ViewModel
{
    public partial class NetworkViewModel : ToolViewModel
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
                if (buffer == null || buffer.Length < 29) return new byte[] { };
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
        
        #region fields
        public const string ToolContentId = "NetworkTool";

        public byte[] MasterMac { get; set; }

        private SafeObservable<WifiInfoDevice> _devices = new SafeObservable<WifiInfoDevice>();

        private SafeObservable<InterfaceInfo> _interfaces = new SafeObservable<InterfaceInfo>();

        private InterfaceInfo _interface = null;

        private UdpClient _udpListener = null;

        private UdpClient _udpSender = null;

        private IPEndPoint _broadcastEP = null;

        byte[] _packet = new byte[] { 0x49, 0x00, 0x0d };

        Dictionary<string, WifiInfoDevice> _devicesDictionary = new Dictionary<string, WifiInfoDevice>();

        StateObject state = new StateObject();

        private bool _goOn = false;
        #endregion

        #region Properties

        private bool GoOn
        {
            get { return _goOn; }
            set { if (value != _goOn) _goOn = value; RaisePropertyChanged("CanChangeInterface"); }
        }

        public SafeObservable<WifiInfoDevice> Devices
        {
            get { return _devices; }
            set { _devices = value; }
        }

        public SafeObservable<InterfaceInfo> Interfaces
        {
            get { return _interfaces; }
            set { _interfaces = value; }
        }
        
        public InterfaceInfo ActiveInterface
        {
            get
            {
                return _interface;
            }

            set
            {
                _interface = value;
            }
        }

        public string TotalDevices
        {
            get { return $@"Total: {Devices.Count}"; }
            set { RaisePropertyChanged("TotalDevices"); }
        }

        public bool CanChangeInterface
        {
            get { return !_goOn; }
        }

        #endregion

        #region Commands
        
        #region ConnectCommand
        RelayCommand _connectCommand = null;
        public ICommand ConnectCommand
        {
            get
            {
                if (_connectCommand == null)
                {
                    _connectCommand = new RelayCommand((p) => OnConnect(p), (p) => CanConnect(p));
                }

                return _connectCommand;
            }
        }

        private bool CanConnect(object parameter)
        {
            return ActiveInterface != null && _udpListener == null && _udpSender == null && GoOn == false;
        }

        private void OnConnect(object parameter)
        {
            try
            {
                var remoteEP = InitializeInterface();

                if (remoteEP == null)
                {
                    MessageBox.Show("No interface specified.");
                    return;
                }
                _broadcastEP = remoteEP;
                _udpSender = new UdpClient(AddressFamily.InterNetwork);
                _udpListener = new UdpClient(Settings.Default.WifiPort);
                GoOn = true;
                Devices.CollectionChanged += (sender, args) => { RaisePropertyChanged("TotalDevices"); };
                _udpListener.BeginReceive(new AsyncCallback(ReceivedCallback), state);
                _udpSender.Send(_packet, 3, _broadcastEP);
            }
            catch
            {
                GoOn = false;
                _udpSender = null;
                _udpListener = null;
            }
        }

        #endregion

        #region RefreshCommand
        RelayCommand _refreshCommand = null;
        public ICommand RefreshCommand
        {
            get
            {
                if (_refreshCommand == null)
                {
                    _refreshCommand = new RelayCommand((p) => OnRefresh(p), (p) => CanRefresh(p));
                }

                return _refreshCommand;
            }
        }

        private bool CanRefresh(object parameter)
        {
            return _udpListener != null && _udpSender != null && GoOn == true;
        }

        private void OnRefresh(object parameter)
        {
            if (_udpSender != null)
            {
                try
                {
                    _udpSender.Send(_packet, 3, _broadcastEP);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Application.Current.MainWindow, ex.Message, "Error");
                }
            }
        }

        #endregion

        #region DisconnectCommand
        RelayCommand _disconnectCommand = null;
        public ICommand DisconnectCommand
        {
            get
            {
                if (_disconnectCommand == null)
                {
                    _disconnectCommand = new RelayCommand((p) => OnDisconnect(p), (p) => CanDisconnect(p));
                }

                return _disconnectCommand;
            }
        }

        private bool CanDisconnect(object parameter)
        {
            return _udpListener != null || _udpSender != null;
        }

        private void OnDisconnect(object parameter)
        {
            GoOn = false;

            try
            {
                _udpListener.Close();
                _udpListener = null;

                _udpSender.Close();
                _udpSender = null;
            }
            catch { }
        }

        #endregion

        #endregion

        public NetworkViewModel(string name)
            : base(name)
        {
            IsVisible = true;
            ContentId = ToolContentId;

            Devices = new SafeObservable<WifiInfoDevice>();

            InitializeNetworkList();
        }

        private void InitializeNetworkList()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].OperationalStatus == OperationalStatus.Up &&
                    (interfaces[i].NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     interfaces[i].NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    _interfaces.Add(new InterfaceInfo(interfaces[i]));
                }
            }
            //if (_interfaces.Count > 0) boxInterfaces.SelectedIndex = Settings.Default.InterfaceIdx;
        }

        private IPEndPoint InitializeInterface()
        {
            try
            {
                if (_interface != null)
                {
                    var ni = _interface;
                    var ipProps = ni.NetworkInterfaceInfo.GetIPProperties();
                    string ipstr = "0.0.0.0";

                    foreach (var ip in ipProps.UnicastAddresses)
                    {
                        if ((ni.NetworkInterfaceInfo.OperationalStatus == OperationalStatus.Up)
                            && (ip.Address.AddressFamily == AddressFamily.InterNetwork))
                        {
                            ipstr = ip.Address.ToString();
                        }
                    }
                    ipstr = ipstr.Substring(0, ipstr.LastIndexOf(".")) + ".255";
                    return new IPEndPoint(IPAddress.Parse(ipstr), 333);
                }
            }
            catch { }
            return null;
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
                    if (GoOn) _udpListener.BeginReceive(ReceivedCallback, state);
                }
            }
            catch
            {
                if (_udpListener != null && _udpListener.Client != null && GoOn)
                    _udpListener.BeginReceive(ReceivedCallback, state);
            }
        }

        private void WifiDevicesWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DisconnectCommand.CanExecute(null)) DisconnectCommand.Execute(null);
        }
    }
}
