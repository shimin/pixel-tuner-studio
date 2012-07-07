using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.ComponentModel;

namespace PixelTuner.Service
{
    internal static class DeviceHelper
    {
        static Thread DeviceSeekerThread = new Thread(SeekDevice);
        static bool _keepSeeking = false;
        static Dictionary<string, SerialPort> PortList = new Dictionary<string, SerialPort>();
        static Dictionary<string, Device> ActiveConnections = new Dictionary<string, Device>();
        static AsyncOperation asyncPortCheck = AsyncOperationManager.CreateOperation(null);


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

        static void CheckPort(object state)
        {
            string portName = state as string;
            SerialPort port = null;
            
            try
            {
                port = new SerialPort(portName, 19200, Parity.None, 8, StopBits.One);
                if (!PortList.ContainsKey(portName))
                    PortList.Add(portName, port);
                
                port.Open();
                port.ReadTimeout = 200;
                port.WriteTimeout = 100;

                Device aDevice = new Device(port);
                if (!string.IsNullOrEmpty(aDevice.WhoIsIt()))
                {
                    ActiveConnections.Add(portName, aDevice);
                    Devices.Add(portName);
                }                
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Ошибка доступа к порту " + portName);
                Trace.Write(ex);
            }
            finally
            {
                if (port != null)
                    port.Close();
            }
        }

        private static void SeekDevice()
        {
            while (_keepSeeking)
            {
                string[] newList = SerialPort.GetPortNames();
                List<string> justAdded = newList.Where(name => !PortList.ContainsKey(name)).ToList<string>();
                foreach (string p in justAdded)
                {
                    AsyncOperationManager.CreateOperation(null).Post(CheckPort, p);
                }
                Thread.Sleep(4000);
            }
        }

        //private static ObservableCollection<string> _devicesList = new ObservableCollection<string>();
        public static ObservableCollection<string> Devices = new ObservableCollection<string>();
        //public static ReadOnlyObservableCollection<string> Devices
        //{
        //    get
        //    {
        //        if (_devices == null)
        //            _devices = new ObservableCollection<string>(_devicesList);

        //        return _devices;
        //    }
        //}
    }
}
