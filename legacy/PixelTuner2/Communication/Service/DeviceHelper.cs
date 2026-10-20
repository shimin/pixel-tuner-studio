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

        // Ports that were opened but did not answer as a supported device. They stay open
        // (and are not probed again) until the port disappears or stops being open, the
        // same way such ports used to stay held by the abandoned SerialPort object.
        private static readonly object _rejectedPortsSync = new object();
        private static readonly Dictionary<string, Win32SerialPort> _rejectedPorts = new Dictionary<string, Win32SerialPort>();

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
            ReleaseRejectedPorts(null);
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
                ReleaseRejectedPorts(portNames);
                portNames.ForEach(portName =>
                {
                    if (!DeviceExists(portName))
                    {
                        if (IsRejectedPort(portName))
                            return;

                        var port = new Win32SerialPort(portName, 115200);
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
                            port.Close();
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
            var p = arg as Win32SerialPort;
            if (p == null) return;

            SerialDeviceBase d = null;
            if (p.IsOpen)
            {
                try
                {
                    d = SerialDeviceBase.Create(p);
                }
                catch (Exception ex) { d = null; }
            }

            if (d == null)
            {
                // Not one of our devices (another COM port, a device that did not answer
                // in time, etc.).
                HoldRejectedPort(p);
                return;
            }

            try
            {
                Devices.Add(d);
                OnDeviceFound(d);
            }
            catch (Exception ex) { }
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

        private static bool IsRejectedPort(string portName)
        {
            lock (_rejectedPortsSync)
            {
                return _rejectedPorts.ContainsKey(portName);
            }
        }

        private static void HoldRejectedPort(Win32SerialPort port)
        {
            Win32SerialPort toClose = null;
            lock (_rejectedPortsSync)
            {
                if (_keepSeeking)
                {
                    _rejectedPorts.TryGetValue(port.PortName, out toClose);
                    _rejectedPorts[port.PortName] = port;
                }
                else
                {
                    // Seeking was stopped meanwhile: nothing will release it later.
                    toClose = port;
                }
            }
            if (toClose != null) toClose.Close();
        }

        /// <summary>
        /// Closes held ports whose COM port is no longer listed or no longer open
        /// (device unplugged), so the port is probed again when it comes back.
        /// Pass null to release all of them.
        /// </summary>
        private static void ReleaseRejectedPorts(List<string> currentPortNames)
        {
            var toClose = new List<Win32SerialPort>();
            lock (_rejectedPortsSync)
            {
                foreach (var pair in _rejectedPorts.ToList())
                {
                    if (currentPortNames == null || !currentPortNames.Contains(pair.Key) || !pair.Value.IsOpen)
                    {
                        _rejectedPorts.Remove(pair.Key);
                        toClose.Add(pair.Value);
                    }
                }
            }
            toClose.ForEach(port => port.Close());
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
