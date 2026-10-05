using EpicPlayer.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EpicPlayer
{
    /// <summary>
    /// Interaction logic for WifiTcpDevicesWindow.xaml
    /// </summary>
    public partial class WifiTcpDevicesWindow : Window, INotifyPropertyChanged
    {
        private TcpListener _server;
        private ObservableCollection<WifiInfoDevice> _devices;
        private UdpClient _udpSender;
        private IPEndPoint _broadcastEP;
        private byte[] _packet = new byte[] { 0x49, 0x00, 0x0d };
        private Dictionary<string, WifiInfoDevice> _devicesDictionary;
        private StateObject state;
        private bool _goOn;

        public ObservableCollection<WifiInfoDevice> Devices
        {
            get
            {
                return _devices;
            }

            set
            {
                _devices = value;
            }
        }

        public WifiTcpDevicesWindow(UdpClient udpSender, IPEndPoint remoteEP)
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

            List<byte> list = new List<byte> { 0x4c, 0x06 };
            list.AddRange(remoteEP.Address.GetAddressBytes());
            list.AddRange(BitConverter.GetBytes((ushort)3331));
            list.Add(0x0d);
            _udpSender.Send(list.ToArray(), 9, _broadcastEP);
        }
        /*
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
                                Devices.Add(d);
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
        */
        
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
                    WifiInfoDevice d = new WifiInfoDevice(remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
                    _devices.Add(d);
                    _devicesDictionary.Add(remoteEndPoint.Address.ToString(), d);
                    //SerialDeviceBase d = SerialDeviceBase.Create(new TcpTransport(client));
                    //CreateModel(d, remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
                }
            }
            catch (Exception)
            {
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
