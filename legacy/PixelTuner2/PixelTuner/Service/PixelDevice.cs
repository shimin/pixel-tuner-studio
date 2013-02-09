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
using System.Management;

namespace PixelTuner.Service
{
    enum Command
    {
        WHO_IS_IT = 0x53,
        INFO = 0x49,
        VERSION_BOOT = 0x56,
        VERSION_FW = 0x76,
        WRITE_IFLASH_MODE = 0x46,
        WRITE_IDATA_MODE = 0x66,
        WRITE_BLOCK = 0x42,
        END_WRITE_FLASH = 0x43,
        END_WRITE_DATA = 0x63,
        SWITCH_TO_BOOT = 0x62,
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

    public enum Timeout
    {
        Short = 500,
        Long = 30000
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
        int CURRENTVERSION = 5;

        public SerialPort Port
        {
            get { return _port; }
            set { RaisePropertyChanged("Port"); _port = value; }
        }

        #region Properties

        private const int Umax = 4200;

        private const int Umin = 3000;

        private bool _deviceBusy = false;
        /// <summary>
        /// Показывает что устройство в данный момент выполняет долгую операцию
        /// </summary>
        public bool DeviceBusy
        {
            get { return _deviceBusy; }
            set { _deviceBusy = value; RaisePropertyChanged("DeviceBusy"); }
        }
        
        int _usualPacketSize = 60;
        /// <summary>
        /// Стандартный размер пакета для передачи любых данных
        /// </summary>
        public int UsualPacketSize
        {
            get { return _usualPacketSize; }
            set { _usualPacketSize = value; RaisePropertyChanged("UsualPacketSize"); }
        }

        int _writeReadDelay = 10;
        /// <summary>
        /// Задержка в мсиллисекундах между операцей записи и чтения.
        /// </summary>
        public int WriteReadDelay
        {
            get { return _writeReadDelay; }
            set { _writeReadDelay = value; RaisePropertyChanged("WriteReadDelay"); }
        }

        bool _bootMode = false;
        /// <summary>
        /// Показывает, находится ли устройство в ржиме бутлоадера.
        /// </summary>
        public bool BootMode
        {
            get { return _bootMode; }
            set {  }
        }

        string _name = "Unknown device";
        public string Name
        {
            get { return _name; }
            private set { _name = value; RaisePropertyChanged("Name"); }
        }

        public string FullName
        {
            get { return String.Format("Pixel Poi {0}.{1}.{2}", this.PlatformVersion, this.BootVersion, this.FirmwareVersion); ; }
            private set { RaisePropertyChanged("FullName"); }
        }

        int _maxMemory;
        /// <summary>
        /// Доступный максимум памяти в байтах
        /// </summary>
        public int MaxMemory
        {
            get { return _maxMemory; }
            set { _maxMemory = value; RaisePropertyChanged("MaxMemory"); }
        }

        float _energyLevel = 0;
        /// <summary>
        /// Уровень заряда аккумулятора (1..100)
        /// </summary>
        public float EnergyLevel
        {
            get { return _energyLevel; }
            set { _energyLevel = value; RaisePropertyChanged("EnergyLevel"); }
        }

        string _productID = "";
        /// <summary>
        /// Уникальный идентификатор устройства
        /// </summary>
        public string ProductID
        {
            get { return _productID; }
            set { _productID = value; RaisePropertyChanged("ProductID"); }
        }

        byte _ledsTotal = 0;
        /// <summary>
        /// Число диодов в устройстве
        /// </summary>
        public byte LedsTotal
        {
            get { return _ledsTotal; }
            set { _ledsTotal = value; RaisePropertyChanged("LedsTotal"); }
        }

        int _firmwareVersion = 0;
        /// <summary>
        /// Версия прошивки
        /// </summary>
        public int FirmwareVersion
        {
            get { return _firmwareVersion; }
            set { _firmwareVersion = value; RaisePropertyChanged("FirmwareVersion"); RaisePropertyChanged("FullName"); }
        }

        int _bootVersion = 0;
        /// <summary>
        /// Версия загрузчика
        /// </summary>
        public int BootVersion
        {
            get { return _bootVersion; }
            set { _bootVersion = value; RaisePropertyChanged("BootVersion"); RaisePropertyChanged("FullName"); }
        }
        
        int _platformVersion = 0;
        /// <summary>
        /// Версия платформы
        /// </summary>
        public int PlatformVersion
        {
            get { return _platformVersion; }
            set { _platformVersion = value; RaisePropertyChanged("PlatformVersion"); RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Свойство определяет, показывать ли контролы, связанные с обновлением прошивки
        /// </summary>
        public Visibility UpdateNeeded
        {
            get
            {
                return this.FirmwareVersion < CURRENTVERSION && !this.BootMode ? Visibility.Visible : Visibility.Collapsed;
            }
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
                //var info = GetInfo();
                //UsedMemory = info.UsedMemory;
                //MemoryRate = info.MemoryRate;
                //MaxMemory = info.MaxMemory;
                //EnergyLevel = info.EnergyLevel;
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

        FileStream fs = null;
        public PixelDevice(SerialPort port)
        {
            _port = port;
            if (_port != null)
            {
                _port.ReadTimeout = (int)Timeout.Long;
                _port.WriteTimeout = 2000;
            }

            procStatusInit = new InitProcessStatus((min, max, start, step) =>
                {
                    ProgressMin = min;
                    ProgressMax = max;
                    Progress = start;
                    Step = step;
                }
            );
            procPerformStep = new PerformStep(status =>
            {
                SetStatus(status);
                Progress += _step;
            });
            procSetStatus = new SetStatus(status => SetStatus(status));

            {
                if (_port != null && _port.IsOpen) SetStatus(ConnectionStatus.Online);
                try
                {
                    fs = new FileStream(Path.Combine(@"c:\", "log_" + _port.PortName + ".log"), FileMode.OpenOrCreate);
                }
                catch
                {

                }
            }
            statusSetter = (s =>
            {
                byte[] b = Encoding.UTF8.GetBytes(DateTime.Now.ToString("HH:mm:ss.ffff") + " " + s + "\n");
                fs.Write(b, 0, b.Length);
            });

            AddRemoveUSBHandler();
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
            return _port != null && _port.IsOpen && this.BootMode &&
                File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin"));
            // && RootViewModel.This.ActiveDocument != null;
        }

        private void OnUploadFirmware(object parameter)
        {
            try
            {
                byte[] binaryData = parameter as byte[];
                if (binaryData == null)
                    binaryData = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin"));
                if (binaryData == null) return;
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
            return _port != null && _port.IsOpen && !this.BootMode;
        }

        private void OnUploadCommand(object parameter)
        {
            FileViewModel file = parameter as FileViewModel;
            if (file == null) return;

            List<byte> data = file.GetBinaryPresentation(this.LedsTotal);
            if (data.Count >= this.MaxMemory)
            {
                MessageBox.Show((String)Application.Current.TryFindResource("Global_NotEnoughtSpace"), 
                    Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (file.LineHeight > this.LedsTotal)
            {
                if (MessageBox.Show(
                    String.Format(
                        (String)Application.Current.TryFindResource("Global_TimelineHigherThanDevice"),
                        file.LineHeight,
                        this.LedsTotal,
                        file.LineHeight - this.LedsTotal),
                    Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
            }
            if (file.LineHeight < this.LedsTotal)
            {
                if (MessageBox.Show(
                    String.Format(
                        (String)Application.Current.TryFindResource("Global_TimelineLowerThanDevice"), 
                        file.LineHeight, 
                        this.LedsTotal,
                        this.LedsTotal - file.LineHeight),
                    Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
            }

            BackgroundWorker bgWorker = new BackgroundWorker();
            bgWorker.DoWork += (x, e) => WriteFile(data);
            bgWorker.RunWorkerCompleted += (s, e) =>
            {
                procSetStatus.Invoke(ConnectionStatus.Online);
            };
            bgWorker.RunWorkerAsync();
        }

        #endregion

        #region AutoUpdateCommand
        RelayCommand _autoUpdateCommand = null;
        public ICommand AutoUpdateCommand
        {
            get
            {
                if (_autoUpdateCommand == null)
                {
                    _autoUpdateCommand = new RelayCommand((p) => OnAutoUpdateCommand(p), (p) => CanAutoUpdateCommand(p));
                }

                return _autoUpdateCommand;
            }
        }

        private bool CanAutoUpdateCommand(object parameter)
        {
            return _port != null && _port.IsOpen;
        }

        private void OnAutoUpdateCommand(object parameter)
        {
            SwitchToBoot();
        }
        
        #endregion

        #region UpdatePostponeCommand
        RelayCommand _updatePostponeCommand = null;
        public ICommand UpdatePostponeCommand
        {
            get
            {
                if (_updatePostponeCommand == null)
                {
                    _updatePostponeCommand = new RelayCommand((p) => OnUpdatePostponeCommand(p), (p) => CanUpdatePostponeCommand(p));
                }

                return _updatePostponeCommand;
            }
        }

        private bool CanUpdatePostponeCommand(object parameter)
        {
            return _port != null && _port.IsOpen;
        }

        private void OnUpdatePostponeCommand(object parameter)
        {
            CURRENTVERSION = this.FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        #endregion

        #endregion

        #region DisconnectWatcher

        private ManagementEventWatcher w = null;

        private void AddRemoveUSBHandler()
        {
            WqlEventQuery q;
            ManagementScope scope = new ManagementScope("root\\CIMV2");
            scope.Options.EnablePrivileges = true;

            try
            {
                q = new WqlEventQuery();
                q.EventClassName = "__InstanceDeletionEvent";
                q.WithinInterval = new TimeSpan(0, 0, 3);
                q.Condition = "TargetInstance ISA 'Win32_USBControllerdevice'";
                w = new ManagementEventWatcher(scope, q);
                w.EventArrived += USBRemoved;

                w.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (w != null)
                {
                    w.Stop();
                }
            }
        }

        private void USBRemoved(object sender, EventArgs e)
        {
            Console.WriteLine("A USB device removed");
            this.Disconnect();
        } 
        #endregion

        #region Protocol

        public void Disconnect()
        {
            lock (_longOperation)
            {
                if (_port != null)
                {
                    try
                    {
                        _port.Close();
                        _port = null;
                        SetStatus(ConnectionStatus.Offline);

                        if (fs != null)
                            fs.Close();
                        fs = null;
                    }
                    catch { }
                }
            }
        }

        public string GetName()
        {
            lock (_longOperation)
            {
                byte[] q = GetCommandBytes(Command.WHO_IS_IT).ToArray();
                string result = "";
                result = Encoding.ASCII.GetString(DoSession(q));
                if (result == "STM32BOOT") _bootMode = true;
                if (statusSetter != null) statusSetter.Invoke("Строка: " + result);
                Name = PixelDeviceSpecs.TryGetName(result);
                SetStatus(ConnectionStatus.Online);
                return result;
            }
        }

        public DeviceInfo GetInfo()
        {
            lock (_longOperation)
            {
                var result = new DeviceInfo();
                var buffer = DoSession(GetCommandBytes(Command.INFO).ToArray());
                int Uizm = (buffer[4] << 8 | buffer[5]);
                result.PlatformVersion = buffer[0];
                result.EnergyLevel = (Uizm - Umin) * 100 / (Umax - Umin);
                result.BootVersion = buffer[1];
                result.FirmwareVersion = buffer[2];
                result.LedsTotal = buffer[3];
                int memoryBytes = buffer[6] << 24 | buffer[7] << 16 | buffer[8] << 8 | buffer[9]; // в байтах
                result.MaxMemory = (memoryBytes / 100000) * 100000;
                return result;
            }
        }

        public void UpdateInfo()
        {
            var info = GetInfo();
            this.EnergyLevel = info.EnergyLevel;
            this.PlatformVersion = info.PlatformVersion;
            this.LedsTotal = info.LedsTotal;
            this.MaxMemory = info.MaxMemory;
            this.BootVersion = info.BootVersion;
            this.FirmwareVersion = info.FirmwareVersion;
        }

        private void SwitchToBoot()
        {
            lock (_longOperation)
            {
                if (this.Port == null) return;

                this.Port.ReadTimeout = (int)Timeout.Short;
                DoSession(GetCommandBytes(Command.SWITCH_TO_BOOT).ToArray());
                this.Port.ReadTimeout = (int)Timeout.Long;
            }
        }

        public void WriteFile(List<byte> binaryData)
        {
            lock (_longOperation)
            {
                DeviceBusy = true;
                List<byte> buffer = new List<byte>();

                int max = (binaryData.Count / UsualPacketSize) +
                    (binaryData.Count % UsualPacketSize == 0 ? 2 : 3);
                procStatusInit.Invoke(0, max);
                procPerformStep.Invoke(ConnectionStatus.Uploading);

                // переход в режим записи
                DoSession(GetCommandBytes(Command.WRITE_IDATA_MODE, BitConverter.GetBytes(binaryData.Count)).ToArray());
                procPerformStep.Invoke(ConnectionStatus.Uploading);

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
                DeviceBusy = false;
            }
        }

        public void WriteFirmware(List<byte> binaryData)
        {
            lock (_longOperation)
            {
                DeviceBusy = true;
                List<byte> buffer = new List<byte>();

                int max = (binaryData.Count / UsualPacketSize) +
                    (binaryData.Count % UsualPacketSize == 0 ? 2 : 3);
                procStatusInit.Invoke(0, max);
                procPerformStep.Invoke(ConnectionStatus.Uploading);

                // переход в режим записи
                DoSession(GetCommandBytes(Command.WRITE_IFLASH_MODE).ToArray());
                procPerformStep.Invoke(ConnectionStatus.Uploading);

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
                byte[] finishProcess = GetCommandBytes(Command.END_WRITE_FLASH).ToArray();
                _port.Write(finishProcess, 0, finishProcess.Length);
                DeviceBusy = false;
            }
        }

        object _syncSession = new object();

        /// <summary>
        /// Объект синхронизации. Не использовать внутри DoSession!
        /// </summary>
        object _longOperation = new object();
        /// <summary>
        /// Возвращает ответ устройства на пользовательский запрос.
        /// </summary>
        /// <param name="query">Запрос</param>
        /// <exception cref="InvalidOperationException">Бросается если не совпал номер команды, пришедший в ответе.</exception>
        /// <returns>Ответ</returns>
        private byte[] DoSession(byte[] query)
        {
            if (statusSetter != null) statusSetter.Invoke("Запись: " + BytesToString(query));
            lock (_syncSession)
            {
                List<byte> result = new List<byte>();
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
                _port.Write(query, 0, query.Length);
                Thread.Sleep(WriteReadDelay);
                int ans = -1;
                try { ans = _port.ReadByte(); }
                catch { }

                if (statusSetter != null) statusSetter.Invoke("Прочитан заголовок: " + ans.ToString("X2"));
                if (ans == (int)query[0])   // если ответ на ту же команду
                {
                    int len = -1;
                    len = _port.ReadByte();
                    if (statusSetter != null) statusSetter.Invoke("Прочитана длина: " + len.ToString("X2"));
                    byte[] receiveBuf = new byte[len];
                    int bytesGot = _port.Read(receiveBuf, 0, len);
                    if (statusSetter != null) statusSetter.Invoke("Получено (" + bytesGot.ToString("X2") + "): " + BytesToString(receiveBuf));
                    return receiveBuf;
                }
            }
            return new byte[] { };
        }

        private List<byte> GetCommandBytes(Command command, IList<byte> args = null)
        {
            List<byte> result = new List<byte>() { (byte)command };
            switch (command)
            {
                case Command.INFO: result.Add(0); break;
                case Command.WHO_IS_IT: result.Add(0); break;
                case Command.WRITE_IDATA_MODE: result.Add((byte)args.Count); result.AddRange(args); break;
                case Command.WRITE_IFLASH_MODE: result.Add(0); break;
                case Command.END_WRITE_DATA: result.Add(0); break;
                case Command.END_WRITE_FLASH: result.Add(0); break;
                case Command.SWITCH_TO_BOOT: result.Add(0); break;
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
        }

        public override bool Equals(object obj)
        {
            if (obj is PixelDevice)
            {
                if ((obj as PixelDevice)._port != null && this._port != null)
                    if ((obj as PixelDevice)._port.PortName == this._port.PortName) return true;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }
}
