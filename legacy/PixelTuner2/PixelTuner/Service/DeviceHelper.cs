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
using System.Windows.Input;

namespace PixelTuner.Service
{
    public static class DeviceHelper
    {
        static Thread DeviceSeekerThread = new Thread(PortListController);
        static bool _keepSeeking = false;
        static Dictionary<string, SerialPort> PortList = new Dictionary<string, SerialPort>();
        static List<BackgroundWorker> CheckPortWorkers = new List<BackgroundWorker>();

        public static void StartSeeking()
        {
            if (_keepSeeking == true)
            {
                _keepSeeking = false;
                DeviceSeekerThread.Join(1000);
                if (DeviceSeekerThread.IsAlive)
                    throw new InvalidOperationException("Таймаут при попытке повторного запуска сканирования портов.");
                foreach (var worker in CheckPortWorkers)
                {
                    worker.CancelAsync();
                }
            }
            _keepSeeking = true;
            CheckPortWorkers = new List<BackgroundWorker>()
            {
                new BackgroundWorker(), new BackgroundWorker(), new BackgroundWorker()
            };
            foreach (var worker in CheckPortWorkers)
            {
                worker.DoWork += (s, e) => { e.Result = CheckPort(e.Argument); };
                worker.RunWorkerCompleted += (s, e) =>
                {
                    if (e.Result as CheckPortResult != null)
                    {
                        CheckPortResult state = (e.Result as CheckPortResult);
                        if (state.Device != null)
                        {
                            lock (_devices) { _devices.Add(state.Device); }
                        }
                    }
                };
            }
            DeviceSeekerThread.Start();
        }

        public static void StopSeeking()
        {
            _keepSeeking = false;
            if (DeviceSeekerThread != null && DeviceSeekerThread.IsAlive)
                DeviceSeekerThread.Join();
        }

        private static void PortListController()
        {
            while (_keepSeeking)
            {
                string[] newList = SerialPort.GetPortNames();
                List<string> justAdded = new List<string>();
                List<string> justRemoved = new List<string>();
                lock (PortList)
                {
                    justAdded = newList.Where(name => !PortList.ContainsKey(name)).ToList<string>();
                    justRemoved = PortList.Keys.Where(name => !newList.Contains(name)).ToList<string>();
                }
                justAdded.Sort(ComparePorts);
                // удалить исчезнувшие порты и отключенные устройства
                foreach (var portName in justRemoved)
                {
                    lock (PortList) { if (PortList.ContainsKey(portName)) PortList.Remove(portName); }
                    if (_devices.Count == 0) break;
                    PixelDevice aDevice = _devices.First(dev => _devices.Count == 0 ? false : dev.Port.PortName == portName);
                    if (aDevice != null)
                    {
                        aDevice.Disconnect();
                        _devices.Remove(aDevice);
                    }
                }
                // проверить обнаруженные порты
                foreach (var portName in justAdded)
                {
                    var worker = GetFreeWorker();   // попытаться найти свободного работника
                    if (worker == null) break;

                    lock (PortList)
                    {                   // если найден, добавить порт в список известных
                        if (!PortList.ContainsKey(portName))
                            PortList.Add(portName, new SerialPort(portName, 19200, Parity.None, 8, StopBits.One));
                    }
                    // и сделать задание на проверку
                    worker.RunWorkerAsync(new CheckPortResult(portName));
                }
                Thread.Sleep(500);
            }
        }

        static BackgroundWorker GetFreeWorker()
        {
            foreach (var w in CheckPortWorkers)
            {
                if (!w.IsBusy) { return w; }
            }
            return null;
        }

        delegate void CheckPortDelegate(CheckPortResult state);

        static CheckPortResult CheckPort(object o)
        {
            CheckPortResult state = o as CheckPortResult;
            string portName = state.PortName;
            SerialPort port = null;

            try
            {
                lock (PortList)
                {
                    if (!PortList.ContainsKey(portName))
                        PortList.Add(portName, new SerialPort(portName, 19200, Parity.None, 8, StopBits.One));

                    port = PortList[portName];
                }

                PixelDevice aDevice = new PixelDevice(port);
                lock (_devices)
                {
                    if (!_devices.Contains(aDevice)) { if (!port.IsOpen) port.Open(); }
                }

                if (!string.IsNullOrEmpty(aDevice.GetName()))
                {
                    state.Device = aDevice;
                    aDevice.UpdateInfo();
                }
            }
            catch (InvalidOperationException)
            {
                Trace.WriteLine("Ошибка открытия порта " + portName);
                if (port != null)
                    if (port.IsOpen)
                        port.Close();
            }
            catch (Exception ex)
            {
                if (port != null)
                    if (port.IsOpen)
                        port.Close();
            }
            return state;
        }

        private static void LogExistingPorts(string[] newList, List<string> justAdded, List<string> justRemoved)
        {
            using (System.IO.FileStream fs = new System.IO.FileStream("PortsInfo.txt", System.IO.FileMode.Append))
            {
                byte[] msg = Encoding.UTF8.GetBytes("Список портов текущей итерации:\n");
                fs.Write(msg, 0, msg.Length);
                foreach (string p in newList)
                {
                    msg = Encoding.UTF8.GetBytes(p + "\n");
                    fs.Write(msg, 0, msg.Length);
                }
                msg = Encoding.UTF8.GetBytes("Список добавленных портов:\n");
                fs.Write(msg, 0, msg.Length);
                foreach (string p in justAdded)
                {
                    msg = Encoding.UTF8.GetBytes(p + "\n");
                    fs.Write(msg, 0, msg.Length);
                }
                msg = Encoding.UTF8.GetBytes("Список удаленных портов:\n");
                fs.Write(msg, 0, msg.Length);
                foreach (string p in justRemoved)
                {
                    msg = Encoding.UTF8.GetBytes(p + "\n");
                    fs.Write(msg, 0, msg.Length);
                }
            }
        }

        private static SafeObservable<PixelDevice> _devices = new SafeObservable<PixelDevice> {  };
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

        private static int ComparePorts(string x, string y)
        {
            int intX = 0;
            int intY = 0;
            if (x == null || !int.TryParse(x.Substring(3), out intX))
            {
                if (y == null || !int.TryParse(y.Substring(3), out intY))
                {
                    // If x is null and y is null, they're equal. 
                    return 0;
                }
                else
                {
                    // If x is null and y is not null, y is greater. 
                    return -1;
                }
            }
            else
            {
                // If x is not null...
                if (y == null || !int.TryParse(y.Substring(3), out intY))
                // ...and y is null, x is greater.
                {
                    return 1;
                }
                else
                {
                    // ...and y is not null, compare the 
                    // lengths of the two strings.
                    //
                    int retval = -intX.CompareTo(intY);
                    
                    if (retval != 0)
                    {
                        // If the strings are not of equal length,
                        // the longer string is greater.
                        //
                        return retval;
                    }
                    else
                    {
                        // If the strings are of equal length,
                        // sort them with ordinary string comparison.
                        //
                        return x.CompareTo(y);
                    }
                }
            }
        }
    }
}
