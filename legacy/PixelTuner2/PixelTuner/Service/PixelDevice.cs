using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.IO;

namespace PixelTuner.Service
{
    enum Command
    {
        WHO_IS_IT = 0x53,
        MK_STAT = 0x69,
        VERSION_BOOT = 0x56,
        VERSION_FW = 0x76,
        WRITE_IFLASH_MODE = 0x46,
        WRITE_IDATA_MODE = 0x66,
        WRITE_BLOCK = 0x42,
        END_WRITE_FLASH = 0x43,
        END_WRITE_DATA = 0x63,
    }

    public enum ConnectionStatus
        {
            [DescriptionAttribute("Dev_Props_State_Uploading")]
            Uploading = 0,
            [DescriptionAttribute("Dev_Props_State_Downloading")]
            Downloading = 1,
            [DescriptionAttribute("Dev_Props_State_Idle")]
            Idle = 2,
            [DescriptionAttribute("Dev_Props_State_Online")]
            Online = 3,
            [DescriptionAttribute("Dev_Props_State_Offline")]
            Offline = 4,
            [DescriptionAttribute("Dev_Props_State_Error")]
            Error = 5,
        }
    
    public delegate void InitProcessStatus(int min, int max, int start = 0, int step = 1);
    public delegate void StatusNotifierDelegate(string status);
    public delegate void PerformStep(ConnectionStatus status);
    public delegate void SetStatus(ConnectionStatus status);

    public class DelegateCommand : ICommand
    {
        private readonly Predicate<object> _canExecute;
        private readonly Action<object> _execute;

        public event EventHandler CanExecuteChanged;

        public DelegateCommand(Action<object> execute)
            : this(execute, null)
        {
        }

        public DelegateCommand(Action<object> execute,
                       Predicate<object> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }

            return _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                CanExecuteChanged(this, EventArgs.Empty);
            }
        }
    }

    public class PixelDevice : ViewModelBase
    {
        SerialPort _port = null;
        StatusNotifierDelegate statusSetter = null;
        InitProcessStatus procStatusInit = null;
        PerformStep procPerformStep = null;
        SetStatus procSetStatus = null;

        public SerialPort Port
        {
            get { return _port; }
            set { RaisePropertyChanged("Port"); _port = value; }
        }

        #region Properties

        int _usualPacketSize = 60;
        /// <summary>
        /// Стандартный размер пакета для передачи любых данных
        /// </summary>
        public int UsualPacketSize
        {
            get { return _usualPacketSize; }
            set { RaisePropertyChanged("UsualPacketSize"); _usualPacketSize = value; }
        }

        int _memoryEraseTime = 3000;
        /// <summary>
        /// Время, требуемое перед записью для стирания флеш (мс)
        /// </summary>
        public int MemoryEraseTime
        {
            get { return _memoryEraseTime; }
            set { RaisePropertyChanged("MemoryEraseTime"); _memoryEraseTime = value; }
        }

        int _writeReadDelay = 100;
        /// <summary>
        /// Задержка в мсиллисекундах между операцей записи и чтения.
        /// </summary>
        public int WriteReadDelay
        {
            get { return _writeReadDelay; }
            set { RaisePropertyChanged("WriteReadDelay"); _writeReadDelay = value; }
        }

        string _name = "Unknown device";
        public string Name
        {
            get { return _name; }
            private set { RaisePropertyChanged("Name"); _name = value; }
        }

        int _maxMemory;
        /// <summary>
        /// Доступный максимум памяти
        /// </summary>
        public int MaxMemory
        {
            get { return _maxMemory; }
            set { RaisePropertyChanged("MaxMemory"); _maxMemory = value; }
        }

        int _usedMemory;
        /// <summary>
        /// Использовано памяти
        /// </summary>
        public int UsedMemory
        {
            get { return _usedMemory; }
            set { RaisePropertyChanged("UsedMemory"); _usedMemory = value; }
        }

        int _memoryRate = 24;
        /// <summary>
        /// Отношение занятой памяти к общему объему (1..100)
        /// </summary>
        public int MemoryRate
        {
            get { return _memoryRate; }
            set { RaisePropertyChanged("MemoryRate"); _memoryRate = value; }
        }

        float _energyLevel = 100;
        /// <summary>
        /// Уровень заряда аккумулятора (1..100)
        /// </summary>
        public float EnergyLevel
        {
            get { return _energyLevel; }
            set { RaisePropertyChanged("EnergyLevel"); _energyLevel = value; }
        }

        string _productID = "";
        /// <summary>
        /// Уникальный идентификатор устройства
        /// </summary>
        public string ProductID
        {
            get { return _productID; }
            set { RaisePropertyChanged("ProductID"); _productID = value; }
        }

        #endregion

        #region Status Info

        public static string GetDescription(Enum value)
        {
            System.Reflection.FieldInfo fi = value.GetType().GetField(value.ToString());
            DescriptionAttribute[] attributes =
                  (DescriptionAttribute[])fi.GetCustomAttributes(
                  typeof(DescriptionAttribute), false);
            return (attributes.Length > 0) ? attributes[0].Description : value.ToString();
        }

        ConnectionStatus _status = ConnectionStatus.Offline;
        private void SetStatus(ConnectionStatus connectionStatus)
        {
            _status = connectionStatus;

            RaisePropertyChanged("Status");
            RaisePropertyChanged("StatusRaw");
        }

        public int StatusRaw
        {
            get { return (int)_status; }
        }

        public string Status
        {
            get
            {
                return (String)Application.Current.TryFindResource(GetDescription(_status));
            }
        }

        private int _progressMax = 100;
        public int ProgressMax
        {
            get { return _progressMax; }
            set { RaisePropertyChanged("ProgressMax"); RaisePropertyChanged("Progress"); _progressMax = value; }
        }

        int _progressMin = 0;

        public int ProgressMin
        {
            get { return _progressMin; }
            set { RaisePropertyChanged("ProgressMin"); RaisePropertyChanged("Progress"); _progressMin = value; }
        }

        int _progress = 0;
        public int Progress
        {
            get { return _progress; }
            set
            {
                _progress = value % (_progressMax - _progressMin);
                RaisePropertyChanged("Progress");
            }
        }

        int _step = 1;
        public int Step
        {
            get { return _step; }
            set { RaisePropertyChanged("Step"); RaisePropertyChanged("Progress"); _step = value; }
        }

        #endregion
        
        #region Constructor

        public PixelDevice(SerialPort port)
        {
            _port = port;

            _port.ReadTimeout = 2000;
            _port.WriteTimeout = 2000;
            procStatusInit = new InitProcessStatus((min, max, start, step) =>
                {
                    ProgressMin = min;
                    ProgressMax = max;
                    Progress = start;
                    Step = step;
                }
            );
            procPerformStep = new PerformStep(status => {
                SetStatus(status);
                Progress += _step; });
            procSetStatus = new SetStatus(status => SetStatus(status));
            if (_port != null && _port.IsOpen) SetStatus(ConnectionStatus.Online);
            FileStream fs = new FileStream(_port.PortName + ".log", FileMode.OpenOrCreate);
            statusSetter = (s => { 
                byte[] b = Encoding.UTF8.GetBytes( DateTime.Now.ToString("HH:mm:ss.ffff") + " " + s + "\n"); 
                fs.Write(b, 0, b.Length); 
            });
            //_uploadProjectCommand = new DelegateCommand(x => System.Diagnostics.Debug.WriteLine("8888888888888888888888888888"));
        }

        public PixelDevice(SerialPort port, StatusNotifierDelegate statusSetter)
            : this(port)
        {
            this.statusSetter = statusSetter;
        }

        #endregion

        #region ViewModel commands

        #region UploadFirmwareCommand
        RelayCommand _uploadFirmwareCommand = null;
        public ICommand UploadFirmwareCommand
        {
            get
            {
                if (_uploadFirmwareCommand == null)
                {
                    _uploadFirmwareCommand = new RelayCommand((p) => OnUploadFirmware(p), (p) => CanUploadFirmware(p));
                }

                return _uploadFirmwareCommand;
            }
        }

        private bool CanUploadFirmware(object parameter)
        {
            return _port != null && _port.IsOpen && 
                File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin"));
            // && RootViewModel.This.ActiveDocument != null;
        }

        private void OnUploadFirmware(object parameter)
        {
            try
            {
                byte[] binaryData = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin"));
                BackgroundWorker bgWorker = new BackgroundWorker();
                bgWorker.DoWork += (x, e) => WriteFirmware(new List<byte>(binaryData));
                bgWorker.RunWorkerCompleted += (s, e) =>
                {
                    procSetStatus.Invoke(ConnectionStatus.Online);
                };
                bgWorker.RunWorkerAsync();
                //WriteFirmware(new List<byte>(binaryData));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Application.Current.MainWindow.Title);
            }
        }
        #endregion 

        #region UploadCommand
        RelayCommand _uploadCommand = null;
        public ICommand UploadCommand
        {
            get
            {
                if (_uploadCommand == null)
                {
                    _uploadCommand = new RelayCommand((p) => OnUploadCommand(p), (p) => CanUploadCommand(p));
                }

                return _uploadCommand;
            }
        }

        private bool CanUploadCommand(object parameter)
        {
            return _port != null && _port.IsOpen;
        }

        private void OnUploadCommand(object parameter)
        {
            FileViewModel file = parameter as FileViewModel;
            if (file == null) return;

            List<byte> data = file.GetBinaryPresentation();
            BackgroundWorker bgWorker = new BackgroundWorker();
            bgWorker.DoWork += (x, e) => WriteFile(data);
            bgWorker.RunWorkerCompleted += (s, e) =>
            {
                procSetStatus.Invoke(ConnectionStatus.Online);
            };
            bgWorker.RunWorkerAsync();
            //WriteFile(data);
        }

        #endregion

        #endregion

        #region Commands

        public void Disconnect()
        {
            if (_port != null)
            {
                try
                {
                    _port.Close();
                    _port = null;
                    SetStatus(ConnectionStatus.Offline);
                }
                catch { }
            }
        }

        public string GetName()
        {
            byte[] q = GetCommandBytes(Command.WHO_IS_IT).ToArray();
            string result = "";
            result = Encoding.ASCII.GetString(DoSession(q));
            if (statusSetter != null) statusSetter.Invoke("Строка: " + result);
            Name = PixelDeviceSpecs.TryGetName(result);
            SetStatus(ConnectionStatus.Online);
            return result;
        }
        
        public object GetState()
        {
            byte[] q = GetCommandBytes(Command.MK_STAT).ToArray();
            DoSession(q);

            return null;
        }

        public void WriteFile(List<byte> binaryData)
        {
            List<byte> buffer = new List<byte>();

            int max = binaryData.Count / UsualPacketSize +
                binaryData.Count % UsualPacketSize == 0 ? 2 : 3 +
                MemoryEraseTime / WriteReadDelay;
            procStatusInit.Invoke(0, max);

            // переход в режим записи
            DoSession(GetCommandBytes(Command.WRITE_IDATA_MODE).ToArray());
            procPerformStep.Invoke(ConnectionStatus.Uploading);

            // ожидание стирания прошивки
            for (int t = MemoryEraseTime; t > 0; t -= WriteReadDelay)
            {
                Thread.Sleep(MemoryEraseTime / WriteReadDelay);
                procPerformStep.Invoke(ConnectionStatus.Uploading);
            }

            // запись данных
            foreach (byte b in binaryData)
            {
                buffer.Add(b);
                if (buffer.Count == UsualPacketSize)
                {
                    DoSession(GetCommandBytes(Command.WRITE_BLOCK, buffer).ToArray());
                    procPerformStep.Invoke(ConnectionStatus.Uploading);
                    buffer.Clear();
                }
            }
            if (buffer.Count > 0)
            {
                DoSession(GetCommandBytes(Command.WRITE_BLOCK, buffer).ToArray());
                procPerformStep.Invoke(ConnectionStatus.Uploading);
                buffer.Clear();
            }

            // выход из режима записи
            DoSession(GetCommandBytes(Command.END_WRITE_DATA).ToArray());
            procPerformStep.Invoke(ConnectionStatus.Uploading);
        }

        public void WriteFirmware(List<byte> binaryData)
        {
            List<byte> buffer = new List<byte>();

            int max = (binaryData.Count / UsualPacketSize) +
                (binaryData.Count % UsualPacketSize == 0 ? 2 : 3) +
                (MemoryEraseTime / WriteReadDelay);
            procStatusInit.Invoke(0, max);

            // переход в режим записи
            DoSession(GetCommandBytes(Command.WRITE_IFLASH_MODE).ToArray());
            procPerformStep.Invoke(ConnectionStatus.Uploading);

            // ожидание стирания прошивки
            for (int t = MemoryEraseTime; t > 0; t -= WriteReadDelay)
            {
                Thread.Sleep(WriteReadDelay);
                procPerformStep.Invoke(ConnectionStatus.Uploading);
            }

            // запись данных
            foreach (byte b in binaryData)
            {
                buffer.Add(b);
                if (buffer.Count == UsualPacketSize)
                {
                    DoSession(GetCommandBytes(Command.WRITE_BLOCK, buffer).ToArray());
                    procPerformStep.Invoke(ConnectionStatus.Uploading);
                    buffer.Clear();
                }
            }
            if (buffer.Count > 0)
            {
                DoSession(GetCommandBytes(Command.WRITE_BLOCK, buffer).ToArray());
                procPerformStep.Invoke(ConnectionStatus.Uploading);
                buffer.Clear();
            }

            // выход из режима записи
            DoSession(GetCommandBytes(Command.END_WRITE_FLASH).ToArray());
        }

        /// <summary>
        /// Возвращает ответ устройства на пользовательский запрос.
        /// </summary>
        /// <param name="query">Запрос</param>
        /// <exception cref="InvalidOperationException">Бросается если не совпал номер команды, пришедший в ответе.</exception>
        /// <returns>Ответ</returns>
        private byte[] DoSession(byte[] query)
        {
            byte[] receiveBuf = new byte[]{};
            try
            {
                if (statusSetter != null) statusSetter.Invoke("Запись: " + BytesToString(query));
                _port.Write(query, 0, query.Length);

                Thread.Sleep(WriteReadDelay);

                WaitBytes(1, statusSetter);
                int ans = _port.ReadByte();

                if (statusSetter != null) statusSetter.Invoke("Чтение: " + ans.ToString("X2"));
                if (ans == (int)query[0])   // если ответ на ту же команду
                {
                    WaitBytes(1, statusSetter);
                    int len = _port.ReadByte();

                    receiveBuf = new byte[len];
                    int bytesGot = _port.Read(receiveBuf, 0, len);

                    if (!WaitBytes(1, statusSetter)) return receiveBuf;
                    int dSymbol = _port.ReadByte();

                    Thread.Sleep(10);
                    if (statusSetter != null) statusSetter.Invoke("Чтение: (" + len.ToString("X2") + ") " + BytesToString(receiveBuf));
                    Thread.Sleep(WriteReadDelay);
                    return receiveBuf;
                }
                throw new InvalidOperationException(String.Format(
                    "Неверный ответ '{0}' на команду '{1}'", ans.ToString(), query[0])
                );
            }
            catch (Exception ex)
            {
                procPerformStep.Invoke(ConnectionStatus.Error);
                if (statusSetter != null) statusSetter.Invoke(ex.Message + "\n" + ex.StackTrace);
            }
            return receiveBuf;
        }

        private List<byte> GetCommandBytes(Command command, List<byte> args = null)
        {
            List<byte> result = new List<byte>() { (byte)command };
            switch (command)
            {
                case Command.WHO_IS_IT: result.Add(0); break;
                case Command.MK_STAT: result.Add(0); break;
                case Command.WRITE_IDATA_MODE: result.Add(0); break;
                case Command.WRITE_IFLASH_MODE: result.Add(0); break;
                case Command.END_WRITE_DATA: result.Add(0); break;
                case Command.END_WRITE_FLASH: result.Add(0); break;

                case Command.WRITE_BLOCK: 
                    if (args == null) 
                        throw new ArgumentNullException("Не задан отправляемый набор байт", "args");
                    if (args.Count > UsualPacketSize)
                        throw new ArgumentOutOfRangeException(String.Format("Длина пакета превышает допустимую {0}", UsualPacketSize), "args");
                    result.Add((byte)args.Count);
                    result.AddRange(args);
                    break;
                default: break;
            }
            result.Add(0x0D);
            return result;
        }

        #endregion

        #region Misc

        private bool WaitBytes(int p, StatusNotifierDelegate statusSetter = null)
        {
            try
            {
                PortHelper.WaitForBytes(_port, 1, 5000);
                return true;
            }
            catch
            {
                if (statusSetter != null) statusSetter.Invoke("Таймаут чтения.");
            }
            return false;
        }

        private string BytesToString(byte[] buf)
        {
            string result = "";
            foreach (byte b in buf) result += b.ToString("X2") + " ";
            return result;
        }

        private string BytesToString(List<byte> buf)
        {
            string result = "";
            foreach (byte b in buf) result += b.ToString("X2") + " ";
            return result;
        }

        public override string ToString()
        {
            return this.Name;
            //return _port != null ? this.Port.PortName : "";
        }

        public override bool Equals(object obj)
        {
            if (obj is PixelDevice)
                if ((obj as PixelDevice)._port.PortName == this._port.PortName) return true;

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }
}
