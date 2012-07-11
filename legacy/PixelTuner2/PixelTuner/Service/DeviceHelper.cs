using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.ComponentModel;
using PixelTuner.AuxClasses;

namespace PixelTuner.Service
{
    public static class DeviceHelper
    {
        static Thread DeviceSeekerThread = new Thread(SeekDevice);
        static bool _keepSeeking = false;
        static Dictionary<string, SerialPort> PortList = new Dictionary<string, SerialPort>();
        //static Dictionary<string, PixelDevice> ActiveConnections = new Dictionary<string, PixelDevice>();

        public static void StartSeeking()
        {
            _keepSeeking = true;
            //DeviceSeekerThread.Start();
        }

        public static void StopSeeking()
        {
            _keepSeeking = false;
            DeviceSeekerThread.Join();
        }

        delegate void CheckPortDelegate(CheckPortResult state);

        static CheckPortResult CheckPort(object o)
        {
            CheckPortResult state = o as CheckPortResult;
            string portName = state.PortName;
            SerialPort port = null;
            
            try
            {
                port = new SerialPort(portName, 19200, Parity.None, 8, StopBits.One);
                if (!PortList.ContainsKey(portName))
                    PortList.Add(portName, port);
                
                port.Open();
                port.ReadTimeout = 200;
                port.WriteTimeout = 100;

                PixelDevice aDevice = new PixelDevice(port);
                if (!string.IsNullOrEmpty(aDevice.GetName()))
                {
                    state.Device = aDevice;
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
                //CheckPortDelegate callback = new CheckPortDelegate(CheckPortFinished);
                //callback.Invoke(state);
            }

            return state;
        }

        private static void SeekDevice()
        {
            while (_keepSeeking)
            {
                string[] newList = SerialPort.GetPortNames();
                List<string> justAdded = newList.Where(name => !PortList.ContainsKey(name)).ToList<string>();
                List<string> justRemoved = PortList.Keys.Where(name => !newList.Contains(name)).ToList<string>();
                foreach (string portName in justRemoved)
                {
                    PixelDevice aDevice = _devices.First(dev => dev.Port.PortName == portName);
                    if (aDevice != null)
                    {
                        aDevice.Disconnect();
                        _devices.Remove(aDevice);
                        PortList.Remove(portName);
                    }
                }

                foreach (string portName in justAdded)
                {
                    BackgroundWorker cpWorker = new BackgroundWorker();
                    cpWorker.DoWork += (s, e) => { e.Result = CheckPort(e.Argument); };
                    cpWorker.RunWorkerCompleted += (s, e) => {
                        if (e.Result as CheckPortResult != null) 
                        {
                            CheckPortResult state = (e.Result as CheckPortResult);
                            if (state.Device != null)
                            {
                                _devices.Add(state.Device);
                            }
                        } 
                    };
                    cpWorker.RunWorkerAsync(new CheckPortResult(portName));
                }
                Thread.Sleep(200);
            }
        }

        private static SafeObservable<PixelDevice> _devices = new SafeObservable<PixelDevice> { new PixelDevice(new SerialPort()) };
        public static SafeObservable<PixelDevice> Devices
        {
            get
            {
                return _devices;
            }
        }

        class CheckPortResult
        {
            string _portName;
            public string PortName
            {
                get { return _portName; }
                set { _portName = value; }
            }

            PixelDevice _device = null;
            public PixelDevice Device
            {
                get { return _device; }
                set { _device = value; }
            }

            public CheckPortResult(string portName)
            {
                _portName = portName;
            }
        }
    }
}
