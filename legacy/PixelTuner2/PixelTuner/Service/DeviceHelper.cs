using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.IO.Ports;

namespace PixelTuner.Service
{
    internal static class DeviceHelper
    {
        static Thread DeviceSeekerThread = new Thread(SeekDevice);
        static bool _keepSeeking = false;
        static Dictionary<string, SerialPort> PortList = new Dictionary<string, SerialPort>();
        static Dictionary<string, SerialPort> ActivePortList = new Dictionary<string, SerialPort>();
        enum Command
        {
            WHO_IS_IT = 0x53,
            MK_STAT = 0x69,
            VERSION_BOOT = 0x56,
            VERSION_FW = 0x76,
            WRITE_IFLASH_MODE = 0x46,
            WRITE_BLOCK = 0x42,
            END_WRITE_FLASH = 0x43,
        }

        public static void StartSeeking()
        {
            _keepSeeking = true;
            DeviceSeekerThread.Start();
        }

        public static void StopSeeking()
        {
            _keepSeeking = false;
            DeviceSeekerThread.Join();
        }

        private static void SeekDevice()
        {
            while (_keepSeeking)
            {
                string[] newList = SerialPort.GetPortNames();
                List<string> justAdded = newList.Where(name => !PortList.ContainsKey(name)).ToList<string>();
                foreach (string p in justAdded)
                {
                    SerialPort port = new SerialPort(p, 19200, Parity.None, 8, StopBits.One);
                    port.ReadTimeout = 200;
                    port.WriteTimeout = 100;
                    try
                    {
                        port.Open();
                        byte[] buf = GetCommandBytes(Command.WHO_IS_IT).ToArray();
                        port.Write(buf, 0, buf.Length);
                        PortList.Add(p, port);
                        int ans = port.ReadByte();
                        if (ans == (int)Command.WHO_IS_IT)
                        {
                            ActivePortList.Add(p, port);
                            _devicesList.Add(p);
                        }
                        else { port.Close(); };
                    }
                    catch (Exception ex)
                    {
                        port.Close();
                        Trace.WriteLine("Ошибка доступа к порту " + p);
                        Trace.Write(ex);
                    }
                }
                Thread.Sleep(100);
            }
        }

        private static List<byte> GetCommandBytes(Command command, List<byte> args = null)
        {
            List<byte> result = new List<byte>() { (byte)command };
            switch (command)
            {
                case Command.WHO_IS_IT: result.Add(0); break;
                default: break;
            }
            result.Add(0x0D);
            return result;
        }

        private static ObservableCollection<string> _devicesList = new ObservableCollection<string>();
        public static ReadOnlyObservableCollection<string> Devices = new ReadOnlyObservableCollection<string>(_devicesList);
    }
}
