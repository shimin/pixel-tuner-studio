using System;
using System.Collections.Generic;
using System.Threading;
using System.IO.Ports;
using System.ComponentModel;
using System.Linq;
using System.Management;

namespace Communication.Service
{
    public static class DeviceHelper
    {
        static Thread DeviceSeekerThread;
        static bool _keepSeeking = false;
        public static bool IsSeeking
        {
            get { return _keepSeeking; } 
        }
        //static Dictionary<string, SerialPort> PortList = new Dictionary<string, SerialPort>();

        private static List<SerialDeviceBase> _devices = new List<SerialDeviceBase>();
        public static List<SerialDeviceBase> Devices
        {
            get
            {
                return _devices;
            }
        }
        
        public static void StartSeeking()
        {
            //Devices.Add(new VirtualDevice());
            //OnDeviceFound(Devices.First());
            DeviceSeekerThread = new Thread(DeviceListUpdater);
            if (_keepSeeking == true)
            {
                _keepSeeking = false;
                DeviceSeekerThread.Join(1000);
                if (DeviceSeekerThread.IsAlive)
                    throw new InvalidOperationException("Таймаут при попытке повторного запуска сканирования портов.");
            }

            _keepSeeking = true;
            DeviceSeekerThread.Start();
        }

        public static void StopSeeking()
        {
            _keepSeeking = false;
            if (DeviceSeekerThread != null && DeviceSeekerThread.IsAlive)
                DeviceSeekerThread.Join();
            Devices.ForEach((d) => { d.Disconnect(); });
        }

        /// <summary>
        /// Keeps list of opened COM ports up to date.
        /// </summary>
        private static void DeviceListUpdater()
        {
            while (_keepSeeking)
            {
                List<string> portNames = new List<string> (SerialPort.GetPortNames());
                //List<string> portNames = GetVcpList();
                portNames.ForEach(portName =>
                {
                    if (!DeviceExists(portName))
                    {
                        var port = new SerialPort();
                        port.PortName = portName;
                        port.BaudRate = 115200;
                        port.ReadTimeout = 10000;
                        port.WriteTimeout = 10000;
                        try
                        {
                            port.Open();
                            var tryCreateDeviceOperation = AsyncOperationManager.CreateOperation(portName);
                            tryCreateDeviceOperation.Post(TryCreateDevice, port);
                        }
                        catch (Exception ex)
                        {
                            port.Dispose();
                            port = null;
                        }
                    }
                    else
                    {
                        CheckConnection(portName);
                    }
                });
                Thread.Sleep(500);
            }
        }

        private static List<string> GetVcpList()
        {
            var portNames = new List<string>();

            try
            {
                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM MSSerial_PortName");
                
                if (searcher.Get().Count != 0)
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    Console.WriteLine("-----------------------------------");
                    Console.WriteLine("MSSerial_PortName instance");
                    Console.WriteLine("-----------------------------------");
                    Console.WriteLine("InstanceName: {0}", queryObj["InstanceName"]);

                    Console.WriteLine("-----------------------------------");
                    Console.WriteLine("MSSerial_PortName instance");
                    Console.WriteLine("-----------------------------------");
                    Console.WriteLine("PortName: {0}", queryObj["PortName"]);

                    if (queryObj["InstanceName"].ToString().Contains("USB"))
                    {
                        Console.WriteLine(queryObj["PortName"] + " is a USB to SERIAL adapter / converter");
                    }
                }
            }
            catch (ManagementException e)
            {
                Console.WriteLine("An error occurred while querying for WMI data: " + e.Message);
            }

            return portNames;
        }

        private static void TryCreateDevice(object arg)
        {
            var p = arg as SerialPort;
            if (p != null && p.IsOpen)
            {
                try
                {
                    SerialTransport t = new SerialTransport(p);
                    var d = SerialDeviceBase.Create(t);
                    Devices.Add(d);
                    OnDeviceFound(d);
                }
                catch (Exception ex) { }
            }
        }

        private static void CheckConnection(string portName)
        {
            var device = Devices.Find((d) => { return d.ChannelID == portName; });
            if (device.Transport == null || !device.Transport.IsOpen)
            {
                Devices.Remove(device);
                device.Dispose();
                OnDeviceLost(device);
            }
            else
            {
                try
                {
                    device.GetInfo();
                }
                catch
                {
                    Devices.Remove(device);
                    device.Dispose();
                    OnDeviceLost(device);
                }
            }
        }

        private static bool DeviceExists(string portName)
        {
            bool deviceExists = Devices.Exists((d) => { return d.ChannelID == portName; });
            return deviceExists;
        }

        public static void UpdateFirmware(PixelDevice pixelDevice, List<byte> binaryData)
        {
            try
            {
                pixelDevice.WriteFirmware(binaryData);
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }
        }

        //public static void WriteSettings(List<DeviceFeature> settings)
        //{
        //    foreach (PixelDevice d in _devices)
        //    {
        //        d.WriteSettings(settings);
        //    }
        //}

        public static void WriteSettings<T>(List<DeviceFeature> settings)
            where T : SerialDeviceBase
        {
            try
            {
                var selected = _devices.Where(d => d is T);
                foreach (T d in selected)
                    d.WriteSettings(settings);
            }
            catch (Exception ex)
            {
                
            }
        }

        #region Events

        public delegate void DeviceChangedDelegate(DeviceEventArgs e);

        public static event DeviceChangedDelegate DeviceFound;

        private static void OnDeviceFound(DeviceBase d)
        {
            if (DeviceFound != null)
                DeviceFound(new DeviceEventArgs(d));
        }

        public static event DeviceChangedDelegate DeviceLost;

        private static void OnDeviceLost(DeviceBase d)
        {
            if (DeviceLost != null)
                try
                {
                    DeviceLost(new DeviceEventArgs(d));
                }
                catch { }
        } 

        #endregion
    }
}
