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
        static Thread DeviceSeekerThread = new Thread(SeekDevice);
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
                    PixelDevice aDevice = _devices.First(dev => _devices.Count == 0 ? false : dev.Port.PortName == portName);
                    if (aDevice != null)
                    {
                        aDevice.Disconnect();
                        _devices.Remove(aDevice);
                    }
                    lock (PortList) { PortList.Remove(portName); }
                }
                // проверить обнаруженные порты
                foreach (var portName in justAdded)
                {

                }
                Thread.Sleep(500);
            }
        }

        delegate void CheckPortDelegate(CheckPortResult state);

        static CheckPortResult CheckPort(object o)
        {
            CheckPortResult state = o as CheckPortResult;
            string portName = state.PortName;
            SerialPort port = null;
            using (System.IO.FileStream fs = new System.IO.FileStream("CheckPort " + portName + ".txt", System.IO.FileMode.Append))
            {
                byte[] msg = Encoding.UTF8.GetBytes("Попытка подключения к порту " + portName + "\n");
                fs.Write(msg, 0, msg.Length);
                try
                {
                    port = new SerialPort(portName, 19200, Parity.None, 8, StopBits.One);

                    lock (PortList)
                    {
                        if (!PortList.ContainsKey(portName))
                            PortList.Add(portName, port);
                    }

                    PixelDevice aDevice = new PixelDevice(port);
                    if (!_devices.Contains(aDevice))
                    {
                        msg = Encoding.UTF8.GetBytes("Открытие " + portName + "\n");
                        fs.Write(msg, 0, msg.Length);
                        port.Open();
                        msg = Encoding.UTF8.GetBytes("Порт открыт " + portName + "\n");
                        fs.Write(msg, 0, msg.Length);
                    }

                    if (!string.IsNullOrEmpty(aDevice.GetName()))
                    {
                        state.Device = aDevice;
                    }
                    //foreach (string p in newList)
                    //{
                    //    msg = Encoding.UTF8.GetBytes(p + "\n");
                    //    fs.Write(msg, 0, msg.Length);
                    //}
                }
                catch (InvalidOperationException)
                {
                    Trace.WriteLine("Ошибка открытия порта " + portName);
                    msg = Encoding.UTF8.GetBytes("Ошибка открытия порта " + portName + "\n");
                    fs.Write(msg, 0, msg.Length);

                    if (port != null)
                        port.Close();
                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Ошибка доступа к порту " + portName);
                    Trace.Write(ex);
                    msg = Encoding.UTF8.GetBytes("Ошибка доступа к порту " + portName + "\n");
                    fs.Write(msg, 0, msg.Length);
                    msg = Encoding.UTF8.GetBytes(ex.Message + "\n\n\n");
                    fs.Write(msg, 0, msg.Length);

                    msg = Encoding.UTF8.GetBytes(ex.StackTrace + "\n");
                    fs.Write(msg, 0, msg.Length);
                    if (port != null)
                        port.Close();
                }
                finally
                {
                    //CheckPortDelegate callback = new CheckPortDelegate(CheckPortFinished);
                    //callback.Invoke(state);
                }
            }

            return state;
        }

        private static void SeekDevice()
        {
            while (_keepSeeking)
            {
                string[] newList = SerialPort.GetPortNames();
                List<string> justAdded = new List<string>();
                List<string> justRemoved = new List<string>();
                lock (PortList)
                {
                    justAdded = newList.Where(name => !PortList.ContainsKey(name)).ToList<string>();
                    justAdded.Sort(ComparePorts);
                    justRemoved = PortList.Keys.Where(name => !newList.Contains(name)).ToList<string>();
                }
                LogExistingPorts(newList, justAdded, justRemoved);
                foreach (string portName in justRemoved)
                {
                    
                }
                int portCount = 0;
                foreach (string portName in justAdded)
                {
                    if (portCount > 3) break;
                    BackgroundWorker cpWorker = new BackgroundWorker();
                    cpWorker.DoWork += (s, e) => { e.Result = CheckPort(e.Argument); };
                    cpWorker.RunWorkerCompleted += (s, e) =>
                    {
                        if (e.Result as CheckPortResult != null)
                        {
                            CheckPortResult state = (e.Result as CheckPortResult);
                            if (state.Device != null)
                            {
                                lock (_devices)
                                    if (!_devices.Contains(state.Device))
                                        _devices.Add(state.Device);
                                return;
                            }
                        }
                        if (_keepSeeking)
                        {
                            cpWorker.RunWorkerAsync(e.Result as CheckPortResult);
                        }
                    };
                    cpWorker.RunWorkerAsync(new CheckPortResult(portName));
                    portCount++;
                }
                Thread.Sleep(500);
            }
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
