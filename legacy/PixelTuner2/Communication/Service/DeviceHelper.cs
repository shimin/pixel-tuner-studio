using System;
using System.Collections.Generic;
using System.Threading;
using System.IO.Ports;
using System.ComponentModel;

namespace Communication.Service
{
    public static class DeviceHelper
    {
        static Thread DeviceSeekerThread = new Thread(DeviceListUpdater);
        static bool _keepSeeking = false;
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
        }

        /// <summary>
        /// Keeps list of opened COM ports up to date.
        /// </summary>
        private static void DeviceListUpdater()
        {
            while (_keepSeeking)
            {
                List<string> portNames = new List<string> (SerialPort.GetPortNames());
                portNames.ForEach(portName =>
                {
                    if (!DeviceExists(portName))
                    {
                        var port = new SerialPort();
                        port.PortName = portName;
                        port.BaudRate = 19200;
                        try
                        {
                            port.Open();
                            var tryCreateDeviceOperation = AsyncOperationManager.CreateOperation(portName);
                            tryCreateDeviceOperation.Post(TryCreateDeice, port);
                        }
                        catch
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

        private static void TryCreateDeice(object arg)
        {
            var p = arg as SerialPort;
            if (p != null && p.IsOpen)
            {
                try
                {
                    var d = SerialDeviceBase.Create(p);
                    Devices.Add(d);
                    OnDeviceFound(d);
                }
                catch { }
            }
        }

        private static void CheckConnection(string portName)
        {
            var device = Devices.Find((d) => { return d.PortName == portName; });
            if (device.Port == null || !device.Port.IsOpen)
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
            bool deviceExists = Devices.Exists((d) => { return d.PortName == portName; });
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

        public static void WriteSettings(List<DeviceFeature> settings)
        {
            foreach (PixelDevice d in _devices)
            {
                d.WriteSettings(settings);
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
