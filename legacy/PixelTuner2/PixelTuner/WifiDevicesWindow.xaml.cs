using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private UdpClient _udpClient = null;

        Dictionary<string, WifiInfoDevice> _devicesDictionary = new Dictionary<string, WifiInfoDevice>();

        StateObject state = new StateObject();

        private bool _goOn = true;
        
        public WifiDevicesWindow(UdpClient udpClient)
        {
            _udpClient = new UdpClient(Settings.Default.WifiPort);
            _goOn = true;
            InitializeComponent();
            DataContext = this;
            Closing += WifiDevicesWindow_Closing;
            Devices.CollectionChanged += (sender, args) => { OnPropertyChanged("TotalDevices"); };
            _udpClient.BeginReceive(new AsyncCallback(ReceivedCallback), state);
            //_client = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Udp);
            ////_client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            ////_client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);
            //_client.Bind(new IPEndPoint(IPAddress.Any, 333));
            //try
            //{
            //    Receive(_client, state, _devicesDictionary, _devices);
            //}
            //catch (Exception e)
            //{
            //    MessageBox.Show(e.ToString());
            //}
        }

        private void ReceivedCallback(IAsyncResult result)
        {
            IPEndPoint remoteEnd = new IPEndPoint(IPAddress.Any, 333);
            try
            {
                if (_udpClient != null && _udpClient.Client != null)
                {
                    byte[] received = _udpClient.EndReceive(result, ref remoteEnd);
                    var ipString = remoteEnd.ToString();

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        if (state.DevicesDictionary.ContainsKey(ipString))
                        {
                            _devicesDictionary[ipString].LastAnswer = BytesToString(state.GetData());
                        }
                        else
                        {
                            var d = new WifiInfoDevice(remoteEnd.Address.ToString(), Settings.Default.WifiPort);
                            if (received[0] == 0x50)
                            {
                                d.LastAnswer = BytesToString(received);
                                _devicesDictionary.Add(ipString, d);
                                _devices.Add(d);
                            }
                        }
                    }));
                    if (_goOn) _udpClient.BeginReceive(ReceivedCallback, state);
                }
            }
            catch
            {
                if (_udpClient != null && _udpClient.Client != null && _goOn)
                    _udpClient.BeginReceive(ReceivedCallback, state);
            }
        }

        private static void Receive(Socket client, 
            StateObject state, 
            Dictionary<string, WifiInfoDevice> devicesDictionary, 
            ObservableCollection<WifiInfoDevice> devices)
        {
            try
            {
                state.workSocket = client;
                state.DevicesDictionary = devicesDictionary;
                state.Devices = devices;

                var ep = (EndPoint)new IPEndPoint(IPAddress.Any, 333);
                SocketFlags flags = SocketFlags.None;
                client.BeginReceiveMessageFrom(state.buffer, 0, StateObject.BufferSize, flags, ref ep, ReceiveCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void WifiDevicesWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _goOn = false;
            _udpClient.Close();
            _udpClient = null;
        }

        private static void ReceiveCallback(IAsyncResult iar)
        {
            StateObject state = (StateObject)iar.AsyncState;
            Socket client = state.workSocket;

            IPPacketInformation packetInfo;
            EndPoint remoteEnd = new IPEndPoint(IPAddress.Any, 333);
            SocketFlags flags = SocketFlags.None;

            if (client != null)
            {
                int received = client.EndReceiveMessageFrom(iar, ref flags, ref remoteEnd, out packetInfo);
                var epstr = remoteEnd.ToString();
                //Console.WriteLine($@"{received} bytes received from {remoteEnd} to {packetInfo.Address}");

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (state.DevicesDictionary.ContainsKey(remoteEnd.ToString()))
                    {
                        state.DevicesDictionary[remoteEnd.ToString()].LastAnswer = BytesToString(state.GetData());
                    }
                    else
                    {
                        var d = new WifiInfoDevice();
                        d.IPString = remoteEnd.ToString();
                        var data = state.GetData();
                        if (data[0] == 0x50)
                        {
                            d.LastAnswer = BytesToString(data);
                            d.Init();
                            state.DevicesDictionary.Add(remoteEnd.ToString(), d);
                            state.Devices.Add(d);
                        }
                    }
                }));
                if (state.Continue)
                {
                    var ep = (EndPoint) new IPEndPoint(IPAddress.Any, 0);
                    flags = SocketFlags.None;
                    client.BeginReceiveMessageFrom(state.buffer, 0, StateObject.BufferSize, flags, ref ep,
                        ReceiveCallback, state);
                }
                else
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
            }
        }
        
        static string BytesToString(byte[] src)
        {
            string result = "";
            for (int i = 0; i < src.Length; i++)
                result += src[i].ToString("X2") + " ";
            return result;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void boxRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (RootViewModel.This.UdpСlient != null)
            {
                try
                {
                    RootViewModel.This.UdpСlient.Send(new byte[] { 0x50, 0x05, 0x04, 0x00, 0x00, 0x00, 0x00, 0x0d }, 8);
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
