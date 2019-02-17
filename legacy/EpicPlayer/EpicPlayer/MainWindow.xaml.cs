using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Threading;
using EpicPlayer.Properties;
using System.Net.Sockets;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Net;

namespace SongReader
{
    public static class EnumHelper
    {
        public static string GetDescription<T>(this T enumerationValue)
            where T : struct
        {
            Type type = enumerationValue.GetType();
            if (!type.IsEnum)
            {
                throw new ArgumentException("EnumerationValue must be of Enum type", "enumerationValue");
            }

            //Tries to find a DescriptionAttribute for a potential friendly name
            //for the enum
            MemberInfo[] memberInfo = type.GetMember(enumerationValue.ToString());
            if (memberInfo != null && memberInfo.Length > 0)
            {
                object[] attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attrs != null && attrs.Length > 0)
                {
                    //Pull out the description value
                    return ((DescriptionAttribute)attrs[0]).Description;
                }
            }
            //If we have no description attribute, just return the ToString of the enum
            return enumerationValue.ToString();
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UdpClient _udpClient = null; 
        private enum Status
        {
            [Description("Status: Play")]
            play,
            [Description("Status: Pause")]
            pause,
            [Description("Status: Stop")]
            stop
        }


        private Status _playbackStatus = Status.stop;

        IPEndPoint ep = null;
        
        ArtNet.Sockets.ArtNetSocket artnet = new ArtNet.Sockets.ArtNetSocket();

        public MainWindow()
        {
            UInt32 src = 3155340801;
            var b = System.BitConverter.GetBytes(src);
            InitializeComponent();
            Closing += MainWindow_Closing;
            playbackPanel.IsEnabled = false;

            try
            {
                Settings.Default.Reload();
                _udpClient = new UdpClient(AddressFamily.InterNetwork);
                boxMulticastAddress.Text = Settings.Default.Multicast;

                boxArtnetIP.Text = Settings.Default.IP;
                boxArtnetMask.Text = Settings.Default.Mask;
                boxArtnetPort.Text = Settings.Default.Port;
                boxArtnetUni.Text = Settings.Default.Universe;
                boxArtnetChannel.Text = Settings.Default.Channel;
                boxArtnetStart.Text = Settings.Default.StartCmd;
                boxArtnetStop.Text = Settings.Default.StopCmd;
                boxArtnetPause.Text = Settings.Default.PauseCmd;

                ep = new IPEndPoint(IPAddress.Parse(boxMulticastAddress.Text), 333);
                _lastMsSent = 0;
                if (_stopTimer == null)
                    _stopTimer = new Timer(StopTimerCallback, null, 1000, 1000);

                var path = Settings.Default.FilePath;
                if (!string.IsNullOrEmpty(path))
                {
                    player.Open(new Uri(Settings.Default.FilePath));
                    songTitle.Content = path;
                    //sets default items
                    _playbackStatus = Status.play;
                    playStatus.Content = EnumHelper.GetDescription<Status>(_playbackStatus);

                    if (firstTime == 0)
                    {
                        volumeLabel.Content = "Volume: 50%";
                        volumeSlider.Value = .5;

                        //sets up timer to track song progress
                        songTime.Tick += songTime_Tick;
                        songTime.Interval = new TimeSpan(0, 0, 1);
                    }
                    firstTime = 1;
                    playbackPanel.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_stopTimer != null)
            {
                _stopTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _stopTimer.Dispose();
                _stopTimer = null;
            }
            Settings.Default.Multicast = boxMulticastAddress.Text;
            Settings.Default.FilePath = player.Source?.ToString();

            Settings.Default.IP = boxArtnetIP.Text;
            Settings.Default.Mask = boxArtnetMask.Text;
            Settings.Default.Port = boxArtnetPort.Text;
            Settings.Default.Universe = boxArtnetUni.Text;
            Settings.Default.Channel = boxArtnetChannel.Text;
            Settings.Default.StartCmd = boxArtnetStart.Text;
            Settings.Default.StopCmd = boxArtnetStop.Text;
            Settings.Default.PauseCmd = boxArtnetPause.Text;  

            Settings.Default.Save();
        }

        private MediaPlayer player = new MediaPlayer();

        private DispatcherTimer songTime = new DispatcherTimer();

        //used to tell if first song loaded
        private int firstTime = 0;

        private void loadFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            //uncomment for a starting path
            //openDialog.InitialDirectory = "C:";
            
            //options for types of files allowed
            openDialog.Filter = "MP3 files (*.mp3)|*.mp3|WAV Files|*wav|All Files|*.*";
            

            if (openDialog.ShowDialog() == true)
            {
                if (firstTime == 1)
                {
                    playbackPanel.IsEnabled = false;
                    //stops song to add new one
                    cleanClose();
                }

                if (_stopTimer == null)
                    _stopTimer = new Timer(StopTimerCallback, null, 1000, 1000);

                //MediaOpened needed for TimeSpan
                player.MediaOpened += new EventHandler(player_MediaOpened);

                //shows songfile and opens in in the mediaplayer
                songTitle.Content = openDialog.SafeFileName;
                player.Open(new Uri(openDialog.FileName));

                //sets default items
                _playbackStatus = Status.play;
                playStatus.Content = EnumHelper.GetDescription<Status>(_playbackStatus);

                if (firstTime == 0)
                {
                    volumeLabel.Content = "Volume: 50%";
                    volumeSlider.Value = .5;

                    //sets up timer to track song progress
                    songTime.Tick += songTime_Tick;
                    songTime.Interval = new TimeSpan(0, 0, 1);
                }

                //marks first time loaded a song
                firstTime = 1;
                playbackPanel.IsEnabled = true;
            }
        }

        private void deviceLookup(object sender, RoutedEventArgs e)
        {
            WifiDevicesWindow w = new WifiDevicesWindow(_udpClient, ep);
            w.Owner = this;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            w.ShowDialog();
        }

        UInt32 _lastMsSent = 0;

        void songTime_Tick(object sender, EventArgs e)
        {
            //shows time where song is
            songStartTime.Content = TimeSpan.FromSeconds(player.Position.TotalSeconds).ToString(@"mm\:ss");

            //shows percent of song done on progress bar
            try
            {
                var percentDone = player.Position.TotalSeconds / player.NaturalDuration.TimeSpan.TotalSeconds * 100;
                songProgress.Value = percentDone;
            }
            catch { }

            var timepoint = (UInt32)player.Position.TotalMilliseconds;
            if (_playbackStatus == Status.play && timepoint - _lastMsSent > 5000)
            {
                try
                {
                    var cmd = _playBytes;
                    BitConverter.GetBytes(timepoint).CopyTo(cmd, 3);
                    if (_udpClient != null) _udpClient.Send(cmd, 8, ep);
                }
                catch (Exception ex)
                {
                    playStatus.Content = ex.Message;
                }
            }
        }
       
        private void player_MediaOpened(object sender, EventArgs e)
        {
            if (player.NaturalDuration.HasTimeSpan)
            {
                _lastMsSent = 0;
                //gets song's duration 
               songEndTime.Content = TimeSpan.FromSeconds(player.NaturalDuration.TimeSpan.TotalSeconds).ToString(@"mm\:ss");
            }
        }

        private void Pause(object sender, RoutedEventArgs e)
        {
            player.Pause();
            _playbackStatus = Status.pause;
            playStatus.Content = EnumHelper.GetDescription<Status>(_playbackStatus);

            try
            {
                var cmd = _playbackStatus == Status.play ? _pauseBytes : _playBytes;
                if (_playbackStatus == Status.stop) return;
                var timepoint = (UInt32)player.Position.TotalMilliseconds;
                BitConverter.GetBytes(timepoint).CopyTo(cmd, 3);
                if (_udpClient != null) _udpClient.Send(cmd, 8, ep);
            }
            catch (Exception ex)
            {
                playStatus.Content = ex.Message;
            }
        }

        private void Stop(object sender, RoutedEventArgs e)
        {
            _lastMsSent = 0;
            player.Stop();
            _playbackStatus = Status.stop;
            playStatus.Content = EnumHelper.GetDescription<Status>(_playbackStatus);

            try
            {
                var cmd = _stopBytes;
                var timepoint = (UInt32)player.Position.TotalMilliseconds;
                BitConverter.GetBytes(timepoint).CopyTo(cmd, 3);
                if (_udpClient != null) _udpClient.Send(cmd, 8, ep);
            }
            catch (Exception ex)
            {
                playStatus.Content = ex.Message;
            }
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            cleanClose();
        }

        public void cleanClose()
        {
            player.Close();
            //set values back to zero
            _playbackStatus = Status.stop;
            playStatus.Content = EnumHelper.GetDescription<Status>(_playbackStatus);
            songTitle.Content = null;
            songProgress.Value = 0;
            songStartTime.Content = "00:00";
            songEndTime.Content = "00:00";
            //stops timer from crashing program
            songTime.Stop();
        }

        private byte[] _playBytes = new byte[] { 0x50, 0x05, 0x02, 0, 0, 0, 0, 0x0d };

        private byte[]  _pauseBytes = new byte[] { 0x50, 0x05, 0x03, 0, 0, 0, 0, 0x0d };

        private byte[] _stopBytes = new byte[] { 0x50, 0x05, 0x04, 0, 0, 0, 0, 0x0d };

        private Timer _stopTimer = null;

        private void Play(object sender, RoutedEventArgs e)
        {
            songStartTime.Content = TimeSpan.FromSeconds(player.Position.TotalSeconds).ToString(@"mm\:ss");
            songEndTime.Content = TimeSpan.FromSeconds(player.NaturalDuration.TimeSpan.TotalSeconds).ToString(@"mm\:ss");
            player.Play();
            songTime.Start();
            _playbackStatus = Status.play;
            playStatus.Content = EnumHelper.GetDescription<Status>(_playbackStatus);

            try
            {
                var cmd = _playBytes;
                var timepoint = (UInt32)player.Position.TotalMilliseconds;
                BitConverter.GetBytes(timepoint).CopyTo(cmd, 3);
                if (_udpClient != null) _udpClient.Send(cmd, 8, ep);
            }
            catch (Exception ex)
            {
                playStatus.Content = ex.Message;
            }
        }

        private void StopTimerCallback(object arg)
        {
            try
            {
                if (_udpClient != null && _playbackStatus == Status.stop)
                {
                    var cmd = _stopBytes;
                    UInt32 timepoint = 0;
                    BitConverter.GetBytes(timepoint).CopyTo(cmd, 3);
                    _udpClient.Send(cmd, 8, ep);
                }
            }
            catch(Exception ex)
            {

            }
        }

        private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            player.Volume = volumeSlider.Value;
            volumeLabel.Content = "Volume: " + (Math.Round(volumeSlider.Value * 100, 0)) + "%";
        }

        byte[] _dmxData = new byte[511];

        byte _lastCmd = 0;

        private void boxUseArtnet_Checked(object sender, RoutedEventArgs e)
        {
            artnet.EnableBroadcast = true;
            try
            {
                artnet.Open(IPAddress.Parse(boxArtnetIP.Text), IPAddress.Parse(boxArtnetMask.Text));
                artnet.NewPacket += new EventHandler<ArtNet.Sockets.NewPacketEventArgs<ArtNet.Packets.ArtNetPacket>>(artnet_NewPacket);
                //Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }

        private void boxUseArtnet_Unchecked(object sender, RoutedEventArgs e)
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

        void artnet_NewPacket(object sender, ArtNet.Sockets.NewPacketEventArgs<ArtNet.Packets.ArtNetPacket> e)
        {
            if (e.Packet.OpCode == ArtNet.Enums.ArtNetOpCodes.Dmx)
            {
                var packet = e.Packet as ArtNet.Packets.ArtNetDmxPacket;
                if (packet.DmxData != _dmxData)
                {
                    if (player.HasAudio && packet.DmxData.Length > 0)
                    {
                        int ArtnetChannel = int.Parse(boxArtnetChannel.Text);
                        byte ArtnetStartValue = byte.Parse(boxArtnetStart.Text);
                        byte ArtnetStopValue = byte.Parse(boxArtnetStop.Text);
                        byte ArtnetPauseValue = byte.Parse(boxArtnetPause.Text);

                        byte cmd = packet.DmxData[ArtnetChannel];
                        if (cmd != _lastCmd)
                        {
                            if (cmd == ArtnetStartValue)
                            {
                                Play(playButton, null);
                            }
                            else if (cmd == ArtnetStopValue)
                            {
                                Stop(stopButton, null);
                            }
                            else if (cmd == ArtnetPauseValue)
                            {
                                Pause(PauseButton, null);
                            }
                        };
                        _lastCmd = cmd;
                    }
                    _dmxData = packet.DmxData;
                }
            }
        }
    }
}
