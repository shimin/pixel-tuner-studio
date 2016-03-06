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
using System.Net;
using PixelTuner.ViewModel;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for DefaultsWindow.xaml
    /// </summary>
    public partial class ArtNetWindow : Window, INotifyPropertyChanged
    {
        public ArtNetWindow()
        {
            this.DataContext = this;
            InitializeComponent();
        }

        #region Properties

        ArtNet.Sockets.ArtNetSocket artnet = new ArtNet.Sockets.ArtNetSocket();

        public string ArtnetLocalIP
        {
            get { return Settings.Default.ArtnetLocalIP; }
            set
            {
                if (Settings.Default.ArtnetLocalIP != value)
                {
                    Settings.Default.ArtnetLocalIP = value;
                    RaisePropertyChanged("ArtnetLocalIP");
                }
            }
        }

        public string ArtnetSubnet
        {
            get { return Settings.Default.ArtnetSubnet; }
            set
            {
                if (Settings.Default.ArtnetSubnet != value)
                {
                    Settings.Default.ArtnetSubnet = value;
                    RaisePropertyChanged("ArtnetSubnet");
                }
            }
        }

        public int ArtnetPort
        {
            get { return Settings.Default.ArtnetPort; }
            set
            {
                if (Settings.Default.ArtnetPort != value)
                {
                    Settings.Default.ArtnetPort = value;
                    RaisePropertyChanged("ArtnetPort");
                }
            }
        }

        public byte ArtnetUniverse
        {
            get { return Settings.Default.ArtnetUniverse; }
            set
            {
                if (Settings.Default.ArtnetUniverse != value)
                {
                    Settings.Default.ArtnetUniverse = value;
                    RaisePropertyChanged("ArtnetUniverse");
                }
            }
        }

        public int ArtnetChannel
        {
            get { return Settings.Default.ArtnetChannel; }
            set
            {
                if (Settings.Default.ArtnetChannel != value)
                {
                    Settings.Default.ArtnetChannel = value;
                    RaisePropertyChanged("ArtnetChannel");
                }
            }
        }

        public byte ArtnetStartValue
        {
            get { return Settings.Default.ArtnetStartValue; }
            set
            {
                if (Settings.Default.ArtnetStartValue != value)
                {
                    Settings.Default.ArtnetStartValue = value;
                    RaisePropertyChanged("ArtnetStartValue");
                }
            }
        }

        public byte ArtnetPauseValue
        {
            get { return Settings.Default.ArtnetPauseValue; }
            set
            {
                if (Settings.Default.ArtnetPauseValue != value)
                {
                    Settings.Default.ArtnetPauseValue = value;
                    RaisePropertyChanged("ArtnetPauseValue");
                }
            }
        }

        public byte ArtnetStopValue
        {
            get { return Settings.Default.ArtnetStopValue; }
            set
            {
                if (Settings.Default.ArtnetStopValue != value)
                {
                    Settings.Default.ArtnetStopValue = value;
                    RaisePropertyChanged("ArtnetStopValue");
                }
            }
        }

        byte[] _dmxData = new byte[511];

        byte _lastCmd = 0; 

        #endregion

        private void boxRunServer_Click(object sender, RoutedEventArgs e)
        {
            artnet.EnableBroadcast = true;
            try
            {
                artnet.Open(IPAddress.Parse(ArtnetLocalIP), IPAddress.Parse(ArtnetSubnet));
                artnet.NewPacket += new EventHandler<ArtNet.Sockets.NewPacketEventArgs<ArtNet.Packets.ArtNetPacket>>(artnet_NewPacket);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }

        void artnet_NewPacket(object sender, ArtNet.Sockets.NewPacketEventArgs<ArtNet.Packets.ArtNetPacket> e)
        {
            if (e.Packet.OpCode == ArtNet.Enums.ArtNetOpCodes.Dmx)
            {
                var packet = e.Packet as ArtNet.Packets.ArtNetDmxPacket;
                if (packet.DmxData != _dmxData)
                {
                    if (RootViewModel.This.ActiveDocument != null && packet.DmxData.Length > 0)
                    {
                        byte cmd = packet.DmxData[ArtnetChannel];
                        if (cmd != _lastCmd)
                        {
                            if (cmd == ArtnetStartValue) {
                                    Application.Current.Dispatcher.Invoke(new Action(() =>
                                    {
                                        if (RootViewModel.This.ActiveDocument.PlayAudio.CanExecute(null))
                                            RootViewModel.This.ActiveDocument.PlayAudio.Execute(null);
                                    }));
                            } else if (cmd == ArtnetStopValue) {
                                    Application.Current.Dispatcher.Invoke(new Action(() =>
                                    {
                                        if (RootViewModel.This.ActiveDocument.StopAudio.CanExecute(null))
                                            RootViewModel.This.ActiveDocument.StopAudio.Execute(null);
                                    }));
                            } else if (cmd == ArtnetPauseValue) {
                                    Application.Current.Dispatcher.Invoke(new Action(() =>
                                    {
                                        if (RootViewModel.This.ActiveDocument.PauseAudio.CanExecute(null))
                                            RootViewModel.This.ActiveDocument.PauseAudio.Execute(null);
                                    }));
                            }
                        };
                        _lastCmd = cmd;
                    }
                    _dmxData = packet.DmxData;
                }
            }
        }

        private void boxStopServer_Click(object sender, RoutedEventArgs e)
        {
            if (artnet != null)
            {
                try
                {
                    artnet.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #region Events

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
