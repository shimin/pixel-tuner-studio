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

        // Ports that did not answer as a supported device. They are closed right away and
        // not probed again while the program runs, until the port disappears from the
        // system (device unplugged): if it comes back it is probed once more.
        private static readonly object _blacklistSync = new object();
        private static readonly HashSet<string> _blacklistedPorts = new HashSet<string>();

        // Copy-on-write: the list is never modified after it is published, every change
        // swaps in a new list. Devices is read from the UI thread, the seeker thread and
        // the thread pool at the same time; mutating one shared List<T> from several
        // threads gave "collection was modified" errors and torn reads (a null element),
        // and a NullReferenceException on the seeker thread killed the process.
        private static readonly object _devicesSync = new object();
        private static volatile List<SerialDeviceBase> _devices = new List<SerialDeviceBase>();
        public static List<SerialDeviceBase> Devices
        {
            get
            {
                return _devices;
            }
        }

        private static void AddDevice(SerialDeviceBase device)
        {
            lock (_devicesSync)
            {
                var copy = new List<SerialDeviceBase>(_devices);
                copy.Add(device);
                _devices = copy;
            }
        }

        private static void RemoveDevice(SerialDeviceBase device)
        {
            lock (_devicesSync)
            {
                var copy = new List<SerialDeviceBase>(_devices);
                copy.Remove(device);
                _devices = copy;
            }
        }
        
        public static void StartSeeking()
        {
            //Devices.Add(new VirtualDevice());
            //OnDeviceFound(Devices.First());
            // Wait for the previous seeker thread, not the new one (joining a thread that
            // was never started throws ThreadStateException).
            var previous = DeviceSeekerThread;
            if (_keepSeeking == true)
            {
                _keepSeeking = false;
                if (previous != null)
                {
                    previous.Join(1000);
                    if (previous.IsAlive)
                        throw new InvalidOperationException("Таймаут при попытке повторного запуска сканирования портов.");
                }
            }

            DeviceSeekerThread = new Thread(DeviceListUpdater);
            DeviceSeekerThread.IsBackground = true;
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
                try
                {
                    ScanPorts();
                }
                catch (Exception ex)
                {
                    // An exception escaping this thread would terminate the process.
                }
                Thread.Sleep(500);
            }
        }

        private static void ScanPorts()
        {
            List<string> portNames = new List<string> (SerialPort.GetPortNames());
            //List<string> portNames = GetVcpList();
            ForgetRemovedPorts(portNames);
            portNames.ForEach(portName =>
            {
                if (!DeviceExists(portName))
                {
                    if (IsBlacklisted(portName))
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
                // in time, etc.): release it and stop probing it.
                BlacklistPort(p);
                return;
            }

            try
            {
                AddDevice(d);
                OnDeviceFound(d);
            }
            catch (Exception ex) { }
        }

        private static void CheckConnection(string portName)
        {
            var device = Devices.Find((d) => { return d.ChannelID == portName; });
            if (device == null) return;
            var transport = device.Transport;
            if (transport == null || !transport.IsOpen)
            {
                RemoveDevice(device);
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
                    RemoveDevice(device);
                    device.Dispose();
                    OnDeviceLost(device);
                }
            }
        }

        private static bool IsBlacklisted(string portName)
        {
            lock (_blacklistSync)
            {
                return _blacklistedPorts.Contains(portName);
            }
        }

        private static void BlacklistPort(Win32SerialPort port)
        {
            // Blacklist first, then close: the seeker must not reopen it in between.
            lock (_blacklistSync)
            {
                _blacklistedPorts.Add(port.PortName);
            }
            port.Close();
        }

        /// <summary>
        /// Drops blacklisted ports that are no longer present in the system, so a device
        /// plugged in later under the same COM name gets probed.
        /// </summary>
        private static void ForgetRemovedPorts(List<string> currentPortNames)
        {
            lock (_blacklistSync)
            {
                _blacklistedPorts.RemoveWhere(name => !currentPortNames.Contains(name));
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
