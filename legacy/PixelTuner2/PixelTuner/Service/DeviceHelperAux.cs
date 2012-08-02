using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.ComponentModel;
using PixelTuner.AuxClasses;
using PixelTuner.Service;
using System.IO.Ports;

namespace PixelTuner.Service
{
    public enum DeviceType
    {
        OEM = 0,
        Node = 1,
        Volume = 2,
        Port = 3,
        Net = 4,
        Interface = 5,
        Handle = 6,
        Instance = 7
    }

    public enum DeviceStatus
    {
        Offline = 0,
        Online = 1
    }
    
    public class DeviceStatusEventArgs : EventArgs
    {
        DeviceStatus _status;
        private DeviceStatus Status
        {
            get { return _status; }
        }

        public DeviceStatusEventArgs(DeviceStatus status)
        {
            _status = status;
        }
    }

    public class DeviceEventArgs : EventArgs
    {
        DeviceType _devType;
        private DeviceType DevType
        {
            get { return _devType; }
        }

        string _source;
        public string Source
        {
            get { return _source; }
        }

        object[] _extraParameters;
        public object[] ExtraParameters
        {
            get { return _extraParameters; }
        }

        public DeviceEventArgs(DeviceType devType, string source, object[] extraParameters)
        {
            this._devType = devType;
            this._source = source;
            this._extraParameters = extraParameters;
        }
    }

    public static class DeviceHelperAux
    {
        public static string[] GetPortList()
        {
            string[] ports = System.IO.Ports.SerialPort.GetPortNames();
            System.Text.RegularExpressions.Regex rx = new System.Text.RegularExpressions.Regex("[0-9]{1,2}");
            List<string> result = new List<string>();
            for (int i = 0; i < ports.Length; ++i)
            {
                if (ports[i] != null)
                {
                    string s = "COM" + rx.Match(ports[i]).Value;
                    if (s.Length >= 4) result.Add(s);
                }
            }
            return result.ToArray();
        }
        private static string[] _portList;
        private static bool _listen = false;
        private static Thread t = new Thread(MainThread);
        private static AsyncOperation op = AsyncOperationManager.CreateOperation(null);

        public static void StartListening()
        {
            _portList = GetPortList();
            _listen = true;

            t = new Thread(MainThread);
            t.Start();
        }

        private static void MainThread()
        {
            while (_listen) 
            {
                string[] newPortList = GetPortList();
                if (newPortList.Length > _portList.Length)
                    for (int i = newPortList.Length - 1; i >= 0; i--)
                    {
                        bool portRecognizedAsNew = true;
                        foreach (string existingPort in _portList)
                        {
                            if (newPortList[i] == existingPort)
                            {
                                portRecognizedAsNew = false;
                                break;
                            }
                        }
                        if (portRecognizedAsNew)
                        {
                            try
                            {
                                SerialPort port = new SerialPort(newPortList[i], 19200, Parity.None, 8, StopBits.One);
                                port.Open();
                                PixelDevice aDevice = new PixelDevice(port);
                                string name = aDevice.GetName();
                                _devices.Add(aDevice);
                                if (name.StartsWith("Pixel"))
                                    op.Post(RaiseDeviceFoundEvent,aDevice);
                                break;
                            }
                            catch { }
                        }
                    }
                _portList = GetPortList();
                Thread.Sleep(80);
            }
        }

        private static void RaiseDeviceFoundEvent(object parameters)
        {
            string pName = (string)parameters;
            OnDeviceFound(new DeviceEventArgs(DeviceType.Port, pName, null));
        }

        public static void StopListening()
        {
            try
            {
                _listen = false;
                t.Abort();
                t.Join();
            }
            catch { }
        }

        private static SafeObservable<PixelDevice> _devices = new SafeObservable<PixelDevice> { };
        public static SafeObservable<PixelDevice> Devices
        {
            get
            {
                return _devices;
            }
        }

        #region События
        public static event EventHandler<DeviceEventArgs> DeviceFound;
        public static void OnDeviceFound(DeviceEventArgs e)
        {
            EventHandler<DeviceEventArgs> raisingEvent = DeviceFound;
            if (raisingEvent != null)
                raisingEvent(null, e);
        }

        public static event EventHandler<DeviceStatusEventArgs> DeviceStatusChanged;
        public static void OnDeviceStatusChange(DeviceStatusEventArgs e)
        {
            EventHandler<DeviceStatusEventArgs> raisingEvent = DeviceStatusChanged;
            if (raisingEvent != null)
                raisingEvent(null, e);
        }
        #endregion
    }
}
