using Common.Collections;
using Communication.Service;
using PixelTuner.Properties;
using PixelTuner.ViewModel.Devices;
using PixelTuner.ViewModel.Network;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PixelTuner.ViewModel
{
    public class NetworkViewModel : ToolViewModel
    {
        private TcpListener _server;
        public const string ToolContentId = "NetworkTool";
        private SafeObservable<DeviceViewModel> _devices;
        private SafeObservable<InterfaceInfo> _interfaces;
        private InterfaceInfo _interface;
        private UdpClient _udpSender;
        private IPEndPoint _broadcastEP;
        private byte[] _packet;
        private Dictionary<string, WifiInfoDevice> _devicesDictionary;
        private StateObject state;
        private bool _goOn;
        private RelayCommand _connectCommand;
        private RelayCommand _refreshCommand;
        private RelayCommand _disconnectCommand;

        public NetworkViewModel(string name) : base(name)
        {
            _devices = new SafeObservable<DeviceViewModel>();
            _interfaces = new SafeObservable<InterfaceInfo>();
            byte[] buffer1 = new byte[3];
            buffer1[0] = 0x49;
            buffer1[2] = 13;
            _packet = buffer1;
            _devicesDictionary = new Dictionary<string, WifiInfoDevice>();
            state = new StateObject();
            base.IsVisible = true;
            base.ContentId = "NetworkTool";
            Devices = new SafeObservable<DeviceViewModel>();
            InitializeNetworkList();
        }

        private bool CanConnect(object parameter) => ActiveInterface != null && _udpSender == null && !GoOn;

        private bool CanDisconnect(object parameter) => _udpSender != null;

        private bool CanRefresh(object parameter) => _udpSender != null && GoOn;

        private void CreateModel(SerialDeviceBase d, string ip, int port)
        {
            if (d.GetType() == typeof(LightMasterDevice))
            {
                WifiLightMasterDeviceViewModel model = new WifiLightMasterDeviceViewModel(d as LightMasterDevice)
                {
                    IP = ip,
                    Port = port
                };
                Devices.Add(model);
                d.Disconnected += e => Devices.Remove(model);
            }
            else if (d.GetType() == typeof(PixelDevice))
            {
                WifiPixelDeviceViewModel model = new WifiPixelDeviceViewModel(d as PixelDevice)
                {
                    IP = ip,
                    Port = port
                };
                Devices.Add(model);
                d.Disconnected += e => Devices.Remove(model);
            }
            else if (d.GetType() == typeof(PixelHoop))
            {
                WifiPixelHoopDeviceViewModel model = new WifiPixelHoopDeviceViewModel(d as PixelHoop)
                {
                    IP = ip,
                    Port = port
                };
                Devices.Add(model);
                d.Disconnected += e => Devices.Remove(model);
            }
            else if (!(d.GetType() == typeof(VideoDevice)))
            {
                GenericSerialDevice device1 = d as GenericSerialDevice;
            }
            else
            {
                WifiVideoDeviceViewModel model = new WifiVideoDeviceViewModel(d as VideoDevice)
                {
                    IP = ip,
                    Port = port
                };
                Devices.Add(model);
                d.Disconnected += e => Devices.Remove(model);
            }
        }

        private void EthernetDevicesLookup()
        {
            try
            {
                UnicastIPAddressInformation information = null;
                foreach (UnicastIPAddressInformation information2 in _interface.NetworkInterfaceInfo.GetIPProperties().UnicastAddresses)
                {
                    if (information2.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        information = information2;
                        break;
                    }
                }
                List<byte> list = new List<byte> { 0x4c, 0x06 };
                list.AddRange(information.Address.GetAddressBytes());
                list.AddRange(BitConverter.GetBytes((ushort)3331));
                list.Add(0x0d);
                _udpSender.Send(list.ToArray(), 9, _broadcastEP);
            }
            catch (Exception)
            {
            }
        }

        private void IncomingConnectionHandler(IAsyncResult result)
        {
            try
            {
                if (_server != null)
                {
                    if (_goOn)
                    {
                        _server.BeginAcceptTcpClient(new AsyncCallback(IncomingConnectionHandler), null);
                    }
                    TcpClient client = _server.EndAcceptTcpClient(result);
                    IPEndPoint remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    SerialDeviceBase d = SerialDeviceBase.Create(new TcpTransport(client));
                    CreateModel(d, remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private IPEndPoint InitializeInterface()
        {
            try
            {
                if (_interface != null)
                {
                    InterfaceInfo info = _interface;
                    string str = "0.0.0.0";
                    foreach (UnicastIPAddressInformation information in info.NetworkInterfaceInfo.GetIPProperties().UnicastAddresses)
                    {
                        if (info.NetworkInterfaceInfo.OperationalStatus != OperationalStatus.Up) continue;
                        if (information.Address.AddressFamily == AddressFamily.InterNetwork)
                            str = information.Address.ToString();
                    }
                    return new IPEndPoint(IPAddress.Parse(str.Substring(0, str.LastIndexOf(".")) + ".255"), Settings.Default.WifiPort);
                }
            }
            catch
            {
            }
            return null;
        }

        private void InitializeNetworkList()
        {
            NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < allNetworkInterfaces.Length; i++)
            {
                if ((allNetworkInterfaces[i].OperationalStatus == OperationalStatus.Up) && ((allNetworkInterfaces[i].NetworkInterfaceType == NetworkInterfaceType.Ethernet) || (allNetworkInterfaces[i].NetworkInterfaceType == NetworkInterfaceType.Wireless80211)))
                {
                    _interfaces.Add(new InterfaceInfo(allNetworkInterfaces[i]));
                }
            }
            SelectedInterfaceIdx = Settings.Default.InterfaceIdx;
        }

        private void OnConnect(object parameter)
        {
            try
            {
                IPEndPoint point = InitializeInterface();
                if (point == null)
                {
                    MessageBox.Show("No interface specified.");
                }
                else
                {
                    _broadcastEP = point;
                    _udpSender = new UdpClient(Settings.Default.WifiPort);
                    _server = new TcpListener(IPAddress.Any, 3331);
                    _server.Start();
                    _server.BeginAcceptTcpClient(new AsyncCallback(IncomingConnectionHandler), null);
                    GoOn = true;
                    Devices.CollectionChanged += (sender, args) => RaisePropertyChanged("TotalDevices");
                    EthernetDevicesLookup();
                }
            }
            catch
            {
                GoOn = false;
                _udpSender = null;
            }
        }

        private void OnDisconnect(object parameter)
        {
            GoOn = false;
            _devicesDictionary.Clear();
            while (_devices.Count > 0)
            {
                _devices?[0].Device?.Transport?.Write(new byte[] { 0x4d, 0x00, 0x0d }, 0, 3);
                _devices[0].Device.Disconnect();
            }
            try
            {
                _udpSender.Close();
                _udpSender = null;
                _server.Stop();
                _server = null;
            }
            catch
            {
            }
        }

        private void OnRefresh(object parameter)
        {
            EthernetDevicesLookup();
        }

        private void ReceivedCallback(IAsyncResult result)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0x14d);
            try
            {
                if ((_udpSender != null) && (_udpSender.Client != null))
                {
                    byte[] received = _udpSender.EndReceive(result, ref remoteEP);
                    string ipString = remoteEP.ToString();
                    
                    Application.Current.Dispatcher.Invoke(new Action(() => 
                    {
                        if (_devicesDictionary.ContainsKey(ipString))
                            _devicesDictionary[ipString].RawData = received;
                    }));

                    if (GoOn) _udpSender.BeginReceive(new AsyncCallback(ReceivedCallback), state);
                }
            }
            catch
            {
                if (_udpSender != null && _udpSender.Client != null && GoOn)
                    _udpSender.BeginReceive(new AsyncCallback(ReceivedCallback), state);
            }
        }

        private void WifiDevicesWindow_Closing(object sender, CancelEventArgs e)
        {
            if (DisconnectCommand.CanExecute(null))
                DisconnectCommand.Execute(null);
        }

        public byte[] MasterMac { get; set; }

        private bool GoOn
        {
            get { return _goOn; }
            set
            {
                if (value != _goOn)
                {
                    _goOn = value;
                }
                RaisePropertyChanged("CanChangeInterface");
            }
        }

        public SafeObservable<DeviceViewModel> Devices
        {
            get { return _devices; }
            set { _devices = value; }
        }

        public SafeObservable<InterfaceInfo> Interfaces
        {
            get { return _interfaces; }
            set { _interfaces = value; }
        }

        public int SelectedInterfaceIdx
        {
            get { return Settings.Default.InterfaceIdx; }
            set
            {
                if (value < _interfaces.Count)
                {
                    Settings.Default.InterfaceIdx = value;
                }
                else
                {
                    Settings.Default.InterfaceIdx = _interfaces.Count - 1;
                }
            }
        }

        public InterfaceInfo ActiveInterface
        {
            get { return _interface; }
            set { _interface = value; }
        }

        public string TotalDevices
        {
            get { return $"Total: {Devices.Count}"; }
            set { RaisePropertyChanged("TotalDevices"); }
        }

        public bool CanChangeInterface => !_goOn;

        public ICommand ConnectCommand
        {
            get
            {
                if (_connectCommand == null)
                {
                    _connectCommand = new RelayCommand(delegate (object p) {
                        OnConnect(p);
                    }, p => CanConnect(p));
                }
                return _connectCommand;
            }
        }

        public ICommand RefreshCommand
        {
            get
            {
                if (_refreshCommand == null)
                {
                    _refreshCommand = new RelayCommand(delegate (object p) {
                        OnRefresh(p);
                    }, p => CanRefresh(p));
                }
                return _refreshCommand;
            }
        }

        public ICommand DisconnectCommand
        {
            get
            {
                if (_disconnectCommand == null)
                {
                    _disconnectCommand = new RelayCommand(delegate (object p) {
                        OnDisconnect(p);
                    }, p => CanDisconnect(p));
                }
                return _disconnectCommand;
            }
        }

        public class StateObject
        {
            public Socket workSocket;
            public const int BufferSize = 0x100;
            public byte[] buffer = new byte[0x100];
            public StringBuilder sb = new StringBuilder();
            public Dictionary<string, WifiInfoDevice> DevicesDictionary = new Dictionary<string, WifiInfoDevice>();
            public ObservableCollection<WifiInfoDevice> Devices = new ObservableCollection<WifiInfoDevice>();
            public bool Continue = true;

            public byte[] GetData()
            {
                if ((buffer == null) || (buffer.Length < 0x1d))
                {
                    return new byte[0];
                }
                buffer = new byte[buffer.Length - 0x1c];
                for (int i = 0x1c; i < buffer.Length; i++)
                {
                    buffer[i - 0x1c] = buffer[i];
                }
                return buffer;
            }
        }
    }
}
