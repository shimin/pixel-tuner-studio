using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;

using Communication.Pixelate;
using Communication.Service;
using PixelTuner.Properties;
using Common.Collections;
using WPFSoundVisualizationLib;
using NAudio;
using Common.Controls;
using Communication.Binarize;
using Communication.Gradient;
using Communication;
using System.Windows.Media;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Common.ViewModel;

namespace PixelTuner.ViewModel
{
    public class FileViewModel : PaneViewModel, IDisposable
    {
        public delegate void InitializePlaybackDelegate(NAudioEngine soundEngine);

        private List<Color> DefaultLineTagColors = null;

        public FileViewModel()
            : this(Path.GetTempFileName())
        {
            IsDirty = true;
        }

        public FileViewModel(string filePath)
        {
            ViewLoaded = false;
            FilePath = filePath;
            Title = FileName;
            DefaultLineTagColors = new List<Color>(new Color[]
            {
                Color.FromRgb(0,0,255),
                Color.FromRgb(255,0,0),
                Color.FromRgb(0,255,0),
                Color.FromRgb(255,0,255),
                Color.FromRgb(0,255,255),
                Color.FromRgb(255,255,0),
                Color.FromRgb(255,255,255),
                Color.FromRgb(127,127,0),
                Color.FromRgb(127,0,127),
                Color.FromRgb(0,127,127)
            });
            _timeLines.CollectionChanged += new NotifyCollectionChangedEventHandler(_timeLines_CollectionChanged);

            Devices = new SafeObservable<PixelDeviceViewModel>();
            FileChanged += (x, e) => { IsDirty = true; };
            DeviceHelper.Devices.ForEach((d) => { DeviceHelper_DeviceFound(new DeviceEventArgs(d)); });
            DeviceHelper.DeviceFound += new DeviceHelper.DeviceChangedDelegate(DeviceHelper_DeviceFound);
            DeviceHelper.DeviceLost += new DeviceHelper.DeviceChangedDelegate(DeviceHelper_DeviceLost);

            NAudioEngine soundEngine = new NAudioEngine();
            if (InitializePlayback != null) InitializePlayback.Invoke(soundEngine);
            soundEngine.PropertyChanged += NAudioEngine_PropertyChanged;
            SoundPlayer = soundEngine;

            RootViewModel.This.DevicesModel.Illuminators.CollectionChanged
                += new NotifyCollectionChangedEventHandler(Illuminators_CollectionChanged);
            OnLoadedCommand = new DelegateCommand(OnLoaded);
        }

        void Illuminators_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("IsSyncAvailable");
            RaisePropertyChanged("RadioModuleMac");
        }

        void DeviceHelper_DeviceFound(DeviceEventArgs e)
        {
            var pixelDevice = e.Device as PixelDevice;
            if (pixelDevice != null)
            {
                Devices.Add(new PixelDeviceViewModel(pixelDevice));
            }

            RaisePropertyChanged("MemoryListHeight");
        }

        void DeviceHelper_DeviceLost(DeviceEventArgs e)
        {
            RaisePropertyChanged("MemoryListHeight");

            List<PixelDeviceViewModel> modelsToRemove = new List<PixelDeviceViewModel>();
            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i].Device == e.Device)
                {
                    modelsToRemove.Add(Devices[i]);
                    break;
                }
            }
            modelsToRemove.ForEach((item) => { Devices.Remove(item); });
        }

        void _timeLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ClearSelection();

            if (e.Action == NotifyCollectionChangedAction.Add)
                ((TimeLineViewModel)e.NewItems[e.NewItems.Count - 1]).IsSelected = true;

            for (int i = 0; i < _timeLines.Count; i++)
            {
                _timeLines[i].Parent = this;
                string path = _timeLines[i].MetaDataPath;
                if (Canvas != null && !string.IsNullOrEmpty(path) && Canvas.ElementsDictionary.ContainsKey(path))
                    _timeLines[i].MetaData = Canvas.ElementsDictionary[_timeLines[i].MetaDataPath];
            }
            
            OnFileChanged(sender, e);
        }

        internal void UpdatePreview(object arg)
        {
            var timePoint = (TimeSpan)arg;
            try
            {
                if (RootViewModel.This.CanvasModel.IsVisible && _parsedProject != null)
                {
                    // update Pixel Poi preview
                    UpdatePixelPoiView(timePoint);

                    // update Light Suit preview
                    UpdateLightSuitView(timePoint);

                    // update Pixel Suit preview
                    UpdatePixelSuitView(timePoint);
                }
            }
            catch (Exception ex)
            {
                
            }
        }

        private void UpdatePixelSuitView(TimeSpan timeStamp)
        {
            var lines = TimeLines.Where(item => item.GetType() == typeof(VideoTimeLineViewModel));
            var timeLineViewModels = lines as TimeLineViewModel[] ?? lines.ToArray();
            if (!timeLineViewModels.Any()) return;
            foreach (VideoTimeLineViewModel l in timeLineViewModels)
            {
                if (l.Items.Count == 0 || l.MetaData == null || l.Start == null) continue;
                var time = l.Start.Value.Add(timeStamp);

                var element = RootViewModel.This.CanvasModel.ElementsDictionary[l.MetaData.Path];
                var item = l.FindItemByTime(time) as VideoThumbnailViewModel;
                if (item != null) element.SetFill(item.ImageSource, item.XOffset, item.YOffset);
                else element.SetFill(new SolidColorBrush(Color.FromRgb(0, 0, 0)));
            }
        }

        private void UpdateLightSuitView(TimeSpan timeStamp)
        {
            var lines = TimeLines.Where(item => item.GetType() == typeof(LightTimeLineViewModel));
            var timeLineViewModels = lines as TimeLineViewModel[] ?? lines.ToArray();
            if (!timeLineViewModels.Any()) return;

            var channelId = 0;
            var sample = _parsedProject.GetColors(timeStamp);
            var maxIdx = CalcMaxSampleColors(timeLineViewModels, sample);
            foreach (LightTimeLineViewModel l in timeLineViewModels)
            {
                if (l.Items.Count == 0 || l.MetaData == null) continue;
                var elementColor = Colors.Black;
                var element = RootViewModel.This.CanvasModel.ElementsDictionary[l.MetaData.Path];

                if (sample != null && channelId/3 < maxIdx)
                {
                    if (l.IsMonochrome)
                    {
                        var amount = sample[channelId/3].R;
                        if (channelId%3 == 1) amount = sample[channelId/3].G;
                        else if (channelId%3 == 2) amount = sample[channelId/3].B;

                        elementColor = Color.FromRgb(amount, amount, amount);
                        channelId++;
                    }
                    else
                    {
                        if (channelId%3 != 0) channelId += 3 - channelId%3;
                        elementColor = Color.FromRgb(sample[channelId/3].R, sample[channelId/3].G, sample[channelId/3].B);
                        channelId += 3;
                    }
                }
                var f = new SolidColorBrush(elementColor);
                l.MetaData.SetFill(f);
                element.SetFill(f);
            }
        }

        private void UpdatePixelPoiView(TimeSpan timeStamp)
        {
            var line = SelectedTimeline;
            if (line == null || !RootViewModel.This.ProjectPlayerModel.IsVisible || line is LightTimeLineViewModel)
                return;
            if (line.Start == null) return;
            var time = line.Start.Value.Add(timeStamp);
            RootViewModel.This.ProjectPlayerModel.DisplayModel = line.FindItemByTime(time);
        }

        private int CalcMaxSampleColors(IEnumerable<TimeLineViewModel> src, List<ColorStruct> sample)
        {
            int result = 0;
            int monochromeLines = 0;
            var lines = new List<TimeLineViewModel>();
            lines.AddRange(src);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i] as LightTimeLineViewModel;
                if (line != null)
                {
                    if (line.IsMonochrome)
                    {
                        monochromeLines++;
                        if (monochromeLines % 3 == 0) { result++; monochromeLines = 0; }
                    }
                    else
                    {
                        if (monochromeLines % 3 != 0)
                        {
                            result++;
                            monochromeLines = 0;
                        }
                        result++;
                    }
                }
            }
            if (monochromeLines % 3 != 0) result++;

            return result;
        }

        #region Properties

        #region IsDirty

        private bool _isDirty = false;
        [XmlIgnore]
        public bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    RaisePropertyChanged("IsDirty");
                    RaisePropertyChanged("FileName");
                }
            }
        }

        #endregion
        
        [XmlIgnore]
        public SafeObservable<PixelDeviceViewModel> Devices
        {
            get;
            private set;
        }

        [XmlElement]
        public CanvasViewModel Canvas
        {
            get; set;
        }

        ObservableCollection<TimeLineViewModel> _timeLines = new ObservableCollection<TimeLineViewModel>();
        [XmlElement(ElementName = "Line")]
        public ObservableCollection<TimeLineViewModel> TimeLines
        {
            get
            {
                if (_timeLines == null)
                    _timeLines = new ObservableCollection<TimeLineViewModel>(_timeLines);

                return _timeLines;
            }
            set
            {
                _timeLines = value;
            }
        }

        ObservableCollection<TimeLineViewModel> _filteredTimeLines = new ObservableCollection<TimeLineViewModel>();
        [XmlIgnore]
        public ObservableCollection<TimeLineViewModel> FilteredTimeLines
        {
            get
            {
                return _filteredTimeLines;
            }
            set
            {
                _filteredTimeLines = value;
            }
        }

        [XmlIgnore]
        public string FileName
        {
            get
            {
                if (FilePath == null)
                {
                    ResourceDictionary rd = Application.Current.Resources.MergedDictionaries[0];
                    return (String)rd["Doc_Hdr_Default"] + (IsDirty ? " *" : "");
                }

                return System.IO.Path.GetFileName(FilePath) + (IsDirty ? "*" : "");
            }
            set
            {
                RaisePropertyChanged("FileName");
            }
        }

        private string _filePath = null;
        [XmlAttribute]
        public string FilePath
        {
            get { return _filePath; }
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    RaisePropertyChanged("FilePath");
                    RaisePropertyChanged("FileName");
                    RaisePropertyChanged("Title");

                    if (File.Exists(_filePath))
                    {
                        ContentId = _filePath;
                    }
                }
            }
        }

        double _horizontalScrollPosition;
        [XmlIgnore]
        public double HorizontalScrollPosition
        {
            get
            {
                return _horizontalScrollPosition;
            }
            set
            {
                _horizontalScrollPosition = value; RaisePropertyChanged("HorizontalScrollPosition");
            }
        }

        Double _minUnitSize = 17.88;
        [XmlIgnore]
        public Double MinUnitSize
        {
            get { return _minUnitSize; }
            set
            {
                _minUnitSize = value;
                RaisePropertyChanged("MinUnitSize");
            }
        }

        Double _maxUnitSize = 140;
        [XmlIgnore]
        public Double MaxUnitSize
        {
            get { return _maxUnitSize; }
            set
            {
                _maxUnitSize = value;
                RaisePropertyChanged("MaxUnitSize");
            }
        }

        [XmlIgnore]
        public Double UnitSizeStep
        {
            get { return 0.1; }
        }

        [XmlAttribute]
        public Double UnitSize
        {
            get { return Settings.Default.UnitSize; }
            set
            {
                //if (value > MaxUnitSize) Settings.Default.UnitSize = MaxUnitSize;
                //else if (value < MinUnitSize) Settings.Default.UnitSize = MinUnitSize;
                //else 
                Settings.Default.UnitSize = Math.Round(value, 1);

                RaisePropertyChanged("UnitSize");
                RaisePropertyChanged("UnitSizeStep");
                RaisePropertyChanged("WaveFormLength");
                RaisePropertyChanged("PlaybackPointerPosition");
                RaisePropertyChanged("ProgressLinePosition");
            }
        }
        
        [XmlIgnore]
        public int ProjectSize
        {
            get
            {
                return 0;
                //List<byte> bin = GetBinaryPresentation();
                //int size = bin.Count;
                //return size;
            }
        }

        [XmlIgnore]
        public bool IsSyncAvailable
        {
            get
            {
                bool isAvailable = RootViewModel.This.DevicesModel.Illuminators.Count > 0;
                if (!isAvailable) IsSyncOn = false;
                return isAvailable;
            }
        }
        
        private bool _isSyncOn = false;
        [XmlIgnore]
        public bool IsSyncOn
        {
            get { return _isSyncOn; }
            set { _isSyncOn = value; RaisePropertyChanged("IsSyncOn"); }
        }

        //private bool _isWifiSyncOn = false;
        //[XmlIgnore]
        //public bool IsWifiSyncOn
        //{
        //    get { return _isWifiSyncOn; }
        //    set
        //    {
        //        _isWifiSyncOn = value;
        //        //RootViewModel.This.WifiPlayer.IsOnAir = value;
        //        RaisePropertyChanged("IsWifiSyncOn");
        //    }
        //}

        [XmlIgnore]
        public string RadioModuleMac
        {
            get
            {
                var devModel = RootViewModel.This.DevicesModel;
                if (devModel != null && devModel.Illuminators.Count > 0)
                {
                    var adr = devModel.Illuminators[0].Device.Mac;
                    string result = "";
                    for (int i = adr.Length - 1; i >= 0; i--)
                        result += adr[i].ToString("X2") + ":";

                    return result.TrimEnd(':');
                }
                return "";
            }
        }

        [XmlIgnore]
        public int MemoryListHeight
        {
            get
            {
                int n = 0;
                if (RootViewModel.This.DevicesModel != null
                    && RootViewModel.This.DevicesModel.Devices.Count != 0)
                {
                    n = DeviceHelper.Devices.Count(i => i.GetType() == typeof(PixelDevice));
                    if (n == 0) return 0;
                    return n * 16 + 20;
                }
                return n;
            }
        }
        
        [XmlIgnore]
        public override Uri IconSource
        {
            get
            {
                return new Uri("/PixelTuner;component/Images/document.png", UriKind.RelativeOrAbsolute);
            }
        }

        [XmlIgnore]
        public TimeLineViewModel SelectedTimeline
        {
            get
            {
                foreach (var t in TimeLines) if (t.IsSelected) return t;
                return null;
            }
        }

        [XmlIgnore]
        public double ProgressLinePosition
        {
            get
            {
                return SoundPlayer.ChannelPosition * Settings.Default.UnitSize + 26.1d;
            }
        }
        
        [XmlIgnore]
        public bool ViewLoaded
        {
            get;
            set;
        }

        [XmlIgnore]
        public Visibility ProgressLineVisibility
        {
            get
            {
                return SoundPlayer != null && SoundPlayer.ChannelLength > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #region SoundPlayer

        [XmlAttribute]
        public string AudioPath { get; set; }

        [XmlIgnore]
        public Double WaveFormLength
        {
            get
            {
                return SoundPlayer != null ? Settings.Default.UnitSize * SoundPlayer.ChannelLength : 0;
            }
            set
            {
            }
        }

        [XmlIgnore]
        public Double PlaybackPointerPosition
        {
            get
            {
                return SoundPlayer != null ? Settings.Default.UnitSize * SoundPlayer.ChannelPosition - 200 : 0;
            }
            set { }
        }

        [XmlIgnore]
        public InitializePlaybackDelegate InitializePlayback = null;

        IWaveformPlayer _soundPlayer;
        [XmlIgnore]
        public IWaveformPlayer SoundPlayer
        {
            get { return _soundPlayer; }
            set { _soundPlayer = value; RaisePropertyChanged("SoundPlayer"); RaisePropertyChanged("ProgressLineVisibility"); }
        }

        #endregion

        #endregion

        #region Undo/Redo
        public void PushUndo(bool keepRedoStack, TimeLineViewModel oldModel = null)
        {
            if (ViewLoaded)
            {
                var state = new ObservableCollection<TimeLineViewModel>();

                foreach (var l in _timeLines) state.Add((TimeLineViewModel)l.Clone());

                _undoStack.Push(state);
                _undoCanvasStack.Push((CanvasViewModel)Canvas.Clone());

                if (!keepRedoStack) _redoStack.Clear();
            }
        }

        private void PushRedo(TimeLineViewModel oldModel = null)
        {
            if (ViewLoaded)
            {
                var state = new ObservableCollection<TimeLineViewModel>();
                if (oldModel == null)
                {
                    foreach (var l in _timeLines) state.Add((TimeLineViewModel)l.Clone());
                }
                else
                {
                    state.Add((TimeLineViewModel)oldModel.Clone());
                }
                _redoStack.Push(state);
                _redoCanvasStack.Push((CanvasViewModel)Canvas.Clone());
            }
        }

        public void Undo()
        {
            if (_undoStack.Count > 0 && ViewLoaded)
            {
                _timeLines.CollectionChanged -= _timeLines_CollectionChanged;
                
                var state = _undoStack.Pop();
                var c = _undoCanvasStack.Pop();

                PushRedo();

                Canvas = c;
                SetState(state);
                RaiseFileChanged();
                _timeLines.CollectionChanged += _timeLines_CollectionChanged;
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0 && ViewLoaded)
            {
                _timeLines.CollectionChanged -= _timeLines_CollectionChanged;
                
                var state = _redoStack.Pop();
                var c = _redoCanvasStack.Pop();

                PushUndo(true);

                Canvas = c;
                SetState(state);
                RaiseFileChanged();
                _timeLines.CollectionChanged += _timeLines_CollectionChanged;
            }
        }

        private void SetState(ObservableCollection<TimeLineViewModel> state)
        {
            if (state.Count > 0)
            {
                _timeLines.Clear();
                foreach (var l in state) _timeLines.Add(l);
            }
        }

        Stack<ObservableCollection<TimeLineViewModel>> _undoStack = new Stack<ObservableCollection<TimeLineViewModel>>();
        Stack<ObservableCollection<TimeLineViewModel>> _redoStack = new Stack<ObservableCollection<TimeLineViewModel>>();
        Stack<CanvasViewModel> _undoCanvasStack = new Stack<CanvasViewModel>();
        Stack<CanvasViewModel> _redoCanvasStack = new Stack<CanvasViewModel>();

        #endregion

        #region Commands

        [XmlIgnore]
        public ICommand OnLoadedCommand { get; private set; }

        private void OnLoaded(object arg)
        {
            ViewLoaded = true;
        }

        #region SaveCommand
        RelayCommand _saveCommand = null;
        public ICommand SaveCommand
        {
            get
            {
                if (_saveCommand == null)
                {
                    _saveCommand = new RelayCommand((p) => OnSave(p), (p) => CanSave(p));
                }

                return _saveCommand;
            }
        }

        private bool CanSave(object parameter)
        {
            return IsDirty;
        }

        private void OnSave(object parameter)
        {
            RootViewModel.This.Save(this, false);
        }

        #endregion

        #region SaveAsCommand
        RelayCommand _saveAsCommand = null;
        public ICommand SaveAsCommand
        {
            get
            {
                if (_saveAsCommand == null)
                {
                    _saveAsCommand = new RelayCommand((p) => OnSaveAs(p), (p) => CanSaveAs(p));
                }

                return _saveAsCommand;
            }
        }

        private bool CanSaveAs(object parameter)
        {
            return IsDirty;
        }

        private void OnSaveAs(object parameter)
        {
            RootViewModel.This.Save(this, true);
        }

        #endregion

        #region CloseCommand
        RelayCommand _closeCommand = null;
        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                {
                    _closeCommand = new RelayCommand((p) => OnClose(), (p) => CanClose());
                }

                return _closeCommand;
            }
        }

        private bool CanClose()
        {
            return true;
        }

        private void OnClose()
        {
            Dispose();
            RootViewModel.This.Close(this);
        }
        #endregion

        #region AddPixelLineCommand
        RelayCommand _addPixelLineCommand = null;
        public ICommand AddPixelLine
        {
            get
            {
                if (_addPixelLineCommand == null)
                {
                    _addPixelLineCommand = new RelayCommand((p) => OnAddPixelLine(p), (p) => CanAddPixelLine(p));
                }

                return _addPixelLineCommand;
            }
        }

        private bool CanAddPixelLine(object parameter)
        {
            return TimeLines.OfType<PixelTimeLineViewModel>().Count() < 20;
        }

        private void OnAddPixelLine(object parameter)
        {
            PushUndo(false);
            _timeLines.Add(new PixelTimeLineViewModel());
        }

        #endregion

        #region AddEpicLineCommand
        RelayCommand _addEpicLineCommand = null;
        public ICommand AddEpicLine
        {
            get
            {
                if (_addEpicLineCommand == null)
                {
                    _addEpicLineCommand = new RelayCommand((p) => OnAddEpicLine(p), (p) => CanAddEpicLine(p));
                }

                return _addEpicLineCommand;
            }
        }

        private bool CanAddEpicLine(object parameter)
        {
            return TimeLines.OfType<EpicPoiTimeLineViewModel>().Count() < 20;
            //return TimeLines.Count<EpicPoiTimeLineViewModel>() < 254;
        }

        private void OnAddEpicLine(object parameter)
        {
            PushUndo(false);
            var c = TimeLines.OfType<EpicPoiTimeLineViewModel>().Count();
            var l = new EpicPoiTimeLineViewModel();
            l.LineColorTag = DefaultLineTagColors[c%DefaultLineTagColors.Count];
            _timeLines.Add(l);
        }

        #endregion

        #region AddLightLineCommand
        RelayCommand _addLightLineCommand = null;
        public ICommand AddLightLine
        {
            get
            {
                if (_addLightLineCommand == null)
                {
                    _addLightLineCommand = new RelayCommand((p) => OnAddLightLine(p), (p) => CanAddLightLine(p));
                }

                return _addLightLineCommand;
            }
        }

        private bool CanAddLightLine(object parameter)
        {
            return TimeLines.Count < 254;
        }

        private void OnAddLightLine(object parameter)
        {
            PushUndo(false);
            _timeLines.Add(new LightTimeLineViewModel());
        }

        #endregion

        #region AddPixelSuitLineCommand
        RelayCommand _addPixelSuitLineCommand = null;
        public ICommand AddPixelSuitLine
        {
            get
            {
                if (_addPixelSuitLineCommand == null)
                {
                    _addPixelSuitLineCommand = new RelayCommand((p) => OnAddPixelSuitLine(p), (p) => CanAddPixelSuitLine(p));
                }

                return _addPixelSuitLineCommand;
            }
        }

        private bool CanAddPixelSuitLine(object parameter)
        {
            return TimeLines.Count < 254;
        }

        private void OnAddPixelSuitLine(object parameter)
        {
            PushUndo(false);
            var c = TimeLines.OfType<VideoTimeLineViewModel>().Count();
            var l = new VideoTimeLineViewModel();
            l.LineColorTag = DefaultLineTagColors[c % DefaultLineTagColors.Count];
            _timeLines.Add(l);
        }

        #endregion

        #region OpenAudioCommand
        RelayCommand _openAudioCommand = null;
        public ICommand OpenAudio
        {
            get
            {
                if (_openAudioCommand == null)
                {
                    _openAudioCommand = new RelayCommand((p) => OnOpenAudio(p), (p) => CanOpenAudio(p));
                }

                return _openAudioCommand;
            }
        }

        private bool CanOpenAudio(object parameter)
        {
            return true;
        }

        private void OnOpenAudio(object parameter)
        {
            var dlg = new OpenFileDialog();
            dlg.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            dlg.Filter = "All Supported Files (*.wav;*.mp3)|*.wav;*.mp3|All Files (*.*)|*.*";
            string path = parameter as string;
            if (path == null || !File.Exists(path))
            {
                if (dlg.ShowDialog() == true && File.Exists(dlg.FileName))
                {
                    ((NAudioEngine)SoundPlayer).OpenFile(dlg.FileName);
                    IsDirty = true;
                    RaisePropertyChanged("WaveFormLength");
                    AudioPath = dlg.FileName;
                }
            }
            else
            {
                ((NAudioEngine)SoundPlayer).OpenFile(path);
                RaisePropertyChanged("WaveFormLength");
            }
        }

        #endregion

        #region ConnectCommand

        private Timer _stopTimer = null;

        private RelayCommand _connectCommand = null;
        public ICommand ConnectCommand
        {
            get
            {
                if (_connectCommand == null)
                {
                    _connectCommand = new RelayCommand((p) => OnConnect(p), (p) => CanConnect(p));
                }

                return _connectCommand;
            }
        }

        private bool CanConnect(object p)
        {
            return RootViewModel.This.UdpСlient == null;
        }

        private void OnConnect(object p)
        {
            try
            {
                RootViewModel.This.UdpСlient = new UdpClient();
                RootViewModel.This.UdpСlient.DontFragment = true;
                RootViewModel.This.UdpСlient.Connect(Settings.Default.MulticastIP, Settings.Default.WifiPort);
                //RootViewModel.This.UdpСlient.ExclusiveAddressUse = false;
                //{
                //    DontFragment = true
                //};
                var w = new WifiDevicesWindow(RootViewModel.This.UdpСlient);
                w.Owner = Application.Current.MainWindow;
                w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                w.ShowDialog();
                _stopTimer = new Timer(StopTimerCallback, null, 1000, 1000);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow, ex.Message, "Error");
                RootViewModel.This.UdpСlient = null;
            }
        }

        private void StopTimerCallback(object arg)
        {
            try
            {
                if (RootViewModel.This.UdpСlient != null && !SoundPlayer.IsPlaying)
                    RootViewModel.This.UdpСlient.Send(new byte[] {0x50, 0x05, 0x04, 0x00, 0x00, 0x00, 0x00, 0x0d}, 8);
            }
            catch
            {
            }
        }
        #endregion

        #region DisconnectCommand

        private RelayCommand _disconnectCommand = null;
        public ICommand DisconnectCommand
        {
            get
            {
                if (_disconnectCommand == null)
                {
                    _disconnectCommand = new RelayCommand((p) => OnDisconnect(p), (p) => CanDisconnect(p));
                }

                return _disconnectCommand;
            }
        }

        private bool CanDisconnect(object p)
        {
            return RootViewModel.This.UdpСlient != null;
        }

        private void OnDisconnect(object p)
        {
            _stopTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _stopTimer.Dispose();
            _stopTimer = null;
            RootViewModel.This.UdpСlient.Close();
            RootViewModel.This.UdpСlient = null;
        }
        #endregion

        #region PlayAudioCommand
        RelayCommand _playAudioCommand = null;
        public ICommand PlayAudio
        {
            get
            {
                if (_playAudioCommand == null)
                {
                    _playAudioCommand = new RelayCommand((p) => OnPlayAudio(p), (p) => CanPlayAudio(p));
                }

                return _playAudioCommand;
            }
        }

        private bool CanPlayAudio(object parameter)
        {
            return SoundPlayer != null && ((NAudioEngine)SoundPlayer).CanPlay;
        }

        private void OnPlayAudio(object parameter)
        {
            var lines = TimeLines.Where(item => item is LightTimeLineViewModel);
            _parsedProject = LightBinaryProject.Parse(GetLightBinaryPresentation(lines));

            var player = ((NAudioEngine)SoundPlayer);
            if (IsSyncAvailable && IsSyncOn && RootViewModel.This.DevicesModel.Illuminators.Count > 0)
            {
                RootViewModel.This.DevicesModel.Illuminators.First().Play(
                    player.CurrentTime, PixelTuner.Properties.Settings.Default.Discreteness);
            }
            //if (IsWifiSyncOn) RootViewModel.This.WifiPlayer.Play(player.CurrentTime, Settings.Default.Discreteness);
            if (RootViewModel.This.UdpСlient != null)
            {
                var timeBytes = BitConverter.GetBytes((UInt32) player.CurrentTime.TotalMilliseconds);
                var cmd = new byte[] {0x50, 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0x0d};
                timeBytes.CopyTo(cmd, 3);
                RootViewModel.This.UdpСlient.Send(cmd, 8);
                RootViewModel.This.UdpСlient.Send(cmd, 8);
                RootViewModel.This.UdpСlient.Send(cmd, 8);
            }

            player.Play();
        }

        private LightBinaryProject _parsedProject = null;

        #endregion

        #region PauseAudioCommand
        RelayCommand _pauseAudioCommand = null;
        public ICommand PauseAudio
        {
            get
            {
                if (_pauseAudioCommand == null)
                {
                    _pauseAudioCommand = new RelayCommand((p) => OnPauseAudio(p), (p) => CanPauseAudio(p));
                }

                return _pauseAudioCommand;
            }
        }

        private bool CanPauseAudio(object parameter)
        {
            return SoundPlayer != null && (((NAudioEngine)SoundPlayer).CanPause);
        }

        private void OnPauseAudio(object parameter)
        {
            var player = (NAudioEngine)SoundPlayer;
            if (IsSyncAvailable && IsSyncOn && RootViewModel.This.DevicesModel.Illuminators.Count > 0)
            {
                if (player.IsPlaying)
                {
                    RootViewModel.This.DevicesModel.Illuminators.First().Pause(
                        player.CurrentTime, Settings.Default.Discreteness);
                }
                else
                {
                    RootViewModel.This.DevicesModel.Illuminators.First().Play(
                        player.CurrentTime, Settings.Default.Discreteness);
                }
            }

            if (player.IsPlaying)
            {
                //if (IsWifiSyncOn)
                //    RootViewModel.This.WifiPlayer.Pause(player.CurrentTime, Settings.Default.Discreteness);
                if (RootViewModel.This.UdpСlient != null)
                {
                    var timeBytes = BitConverter.GetBytes((UInt32)player.CurrentTime.TotalMilliseconds);
                    var cmd = new byte[] {0x50, 0x05, 0x03, 0x00, 0x00, 0x00, 0x00, 0x0d};
                    timeBytes.CopyTo(cmd, 3);
                    RootViewModel.This.UdpСlient.Send(cmd, 8);
                }
            }
            else
            {
                //if (IsWifiSyncOn)
                //    RootViewModel.This.WifiPlayer.Play(player.CurrentTime, Settings.Default.Discreteness);
                if (RootViewModel.This.UdpСlient != null)
                {
                    var timeBytes = BitConverter.GetBytes((UInt32)player.CurrentTime.TotalMilliseconds);
                    var cmd = new byte[] { 0x50, 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0x0d };
                    timeBytes.CopyTo(cmd, 3);
                    RootViewModel.This.UdpСlient.Send(cmd, 8);
                }
            }
            player.Pause();
        }

        #endregion

        #region StopAudioCommand
        RelayCommand _stopAudioCommand = null;
        public ICommand StopAudio
        {
            get
            {
                if (_stopAudioCommand == null)
                {
                    _stopAudioCommand = new RelayCommand((p) => OnStopAudio(p), (p) => CanStopAudio(p));
                }

                return _stopAudioCommand;
            }
        }

        private bool CanStopAudio(object parameter)
        {
            return SoundPlayer != null && ((NAudioEngine)SoundPlayer).CanStop;
        }

        private void OnStopAudio(object parameter)
        {
            var player = (NAudioEngine)SoundPlayer;
            player.ChannelPosition = player.InitialChannelPosition;
            player.Stop();
            if (IsSyncAvailable && IsSyncOn && RootViewModel.This.DevicesModel.Illuminators.Count > 0)
            {
                RootViewModel.This.DevicesModel.Illuminators.First().Stop(
                    player.CurrentTime, Settings.Default.Discreteness);
            }
            //if (IsWifiSyncOn)
            //    RootViewModel.This.WifiPlayer.Stop(player.CurrentTime, Settings.Default.Discreteness);
            //RootViewModel.This.WifiPlayer.SendCommand(PlaybackState.stop, player.CurrentTime);
            if (RootViewModel.This.UdpСlient != null)
            {
                RootViewModel.This.UdpСlient.Send(new byte[] { 0x50, 0x05, 0x04, 0x00, 0x00, 0x00, 0x00, 0x0d }, 8);
            }
        }

        #endregion

        #region ScalingCommand
        RelayCommand _scalingCommand = null;
        public ICommand Scaling
        {
            get
            {
                if (_scalingCommand == null)
                {
                    _scalingCommand = new RelayCommand((p) => OnScaling(p), (p) => CanScaling(p));
                }

                return _scalingCommand;
            }
        }

        private bool CanScaling(object parameter)
        {
            return SoundPlayer != null && ((NAudioEngine)SoundPlayer).WaveformData != null;
        }

        private void OnScaling(object parameter)
        {
            var scaleDelta = GetScaleRate(((double)parameter) * 8, UnitSize);
            if (UnitSize + scaleDelta >= MinUnitSize && UnitSize + scaleDelta <= MaxUnitSize)
                UnitSize += scaleDelta;
        }

        #endregion

        #endregion

        #region NAudio Engine Events

        private Thread _updateThread = null;

        private Task _updateTask = null;

        private UInt32 _previousTimepoint = 0;

        private uint _syncInterval = 1000;

        private void NAudioEngine_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ChannelPosition":
                    //clockDisplay.Time = TimeSpan.FromSeconds(engine.ChannelPosition);
                    if (!SoundPlayer.IsPlaying)
                    {
                        var lines = TimeLines.Where(item => item is LightTimeLineViewModel);
                        _parsedProject = LightBinaryProject.Parse(GetLightBinaryPresentation(lines));
                    }

                    if (SoundPlayer.IsPlaying)
                    {
                        if (IsSyncAvailable && IsSyncOn && RootViewModel.This.DevicesModel.Illuminators.Count > 0)
                        {
                            RootViewModel.This.DevicesModel.Illuminators.First().Play(SoundPlayer.CurrentTime, Settings.Default.Discreteness);
                        }
                        if (RootViewModel.This.UdpСlient != null)
                        {
                            UInt32 timepoint = (UInt32)SoundPlayer.CurrentTime.TotalMilliseconds;
                            if (timepoint > 2000 && _syncInterval != 2000) _syncInterval = 2000;
                            if (timepoint - _previousTimepoint > _syncInterval)
                            {
                                //Console.WriteLine($"Timepoint: {SoundPlayer.CurrentTime}");
                                _previousTimepoint = timepoint;
                                List<byte> cmd = new List<byte>(new byte[] {0x50, 0x05, 0x02, 0x0d});
                                cmd.InsertRange(3, BitConverter.GetBytes(timepoint));
                                RootViewModel.This.UdpСlient.Send(cmd.ToArray(), 8);
                            }
                        }
                        //if (IsWifiSyncOn) RootViewModel.This.WifiPlayer.Play(SoundPlayer.CurrentTime, Settings.Default.Discreteness);
                    }
                    //if (_updateThread == null) _updateThread = new Thread(UpdatePreview);
                    //if (_updateThread != null && !_updateThread.IsAlive)
                    //{
                    //    _updateThread = new Thread(UpdatePreview);
                    //    _updateThread.Start(SoundPlayer.CurrentTime);
                    //}
                    if (!IsSyncOn && RootViewModel.This.UdpСlient == null)
                        UpdatePreview(SoundPlayer.CurrentTime);
                    
                    //if (_updateTask == null) _updateTask = new Task(new Action(() => UpdatePreview(SoundPlayer.CurrentTime)));
                    //if (_updateTask.Status != TaskStatus.Running) _updateTask.Start();

                    RaisePropertyChanged("ProgressLineVisibility");
                    RaisePropertyChanged("ProgressLinePosition");
                    break;
                default:
                    // Do Nothing
                    break;
            }
        }
        
        #endregion

        #region Project generation

        public int EvaluatePixelBinarySize(ushort targetHeight)
        {
            ColorDepth depth = ColorDepth.Bit24;
            try { depth = (ColorDepth)ColorDepth.Parse(typeof(ColorDepth), Settings.Default.ColorDepth); }
            catch { }

            var images = new List<ImagesCollectionItem>();
            Func<TimeLineViewModel, bool> filter = (i) =>
            {
                var line = i as PixelTimeLineViewModel;
                if (line != null) return line.LineHeight == targetHeight;

                return false;
            };

            PixelProjectFile project = CreatePixelProjectFile(_timeLines.Where(filter), images, targetHeight, depth);
            ImagesCollection c = new ImagesCollection(images);

            return project.GetBinarySize(c, targetHeight);
        }

        public List<byte> GetPixelBinaryPresentation(
            IEnumerable<TimeLineViewModel> lines, 
            ushort targetHeight)
        {
            ColorDepth depth = ColorDepth.Bit24;
            try { depth = (ColorDepth)ColorDepth.Parse(typeof(ColorDepth), Settings.Default.ColorDepth); }
            catch { }

            var images = new List<ImagesCollectionItem>();
            PixelProjectFile project = CreatePixelProjectFile(lines, images, targetHeight, depth);
            ImagesCollection c = new ImagesCollection(images);
            var buf = project.GetBinary(c, (ushort)targetHeight);
#if(DEBUG)
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".\\Logs");
            Directory.CreateDirectory(logFolder);
            string projectFileName = Path.Combine(logFolder, "pixel-project " + DateTime.Now.ToString("dd_MM_yyyy___HH_mm_ss_fff") + ".bin");
            File.WriteAllBytes(projectFileName, buf.ToArray());
#endif
            return buf;
        }

        private PixelProjectFile CreatePixelProjectFile(
            IEnumerable<TimeLineViewModel> lines,
            IList<ImagesCollectionItem> imagesCollection, 
            ushort targetHeight, 
            ColorDepth depth)
        {
            var project = new PixelProjectFile();
            foreach (TimeLineViewModel line in lines)
            {
                var pixelLine = line as PixelTimeLineViewModel;
                if (pixelLine == null || pixelLine.Items.Count == 0 || !pixelLine.IsSelectedToUpload) continue;
                var file = new PixelTimelineFile();
                file.TimelineColor = System.Drawing.Color.FromArgb(
                    pixelLine.LineColorTag.A, pixelLine.LineColorTag.R /10, pixelLine.LineColorTag.G/10, pixelLine.LineColorTag.B/10);
                
                var currentTime = pixelLine.Start.Value;

                List<ITimeLineDataItem> timepixelLineItems = FillGabs(pixelLine.Items, pixelLine.Start.Value, CreateBlankPixelItem, pixelLine.LineHeight);
                foreach (TimeLineItemViewModel item in timepixelLineItems)
                {
                    string path = item.FullName.ToLower();
                    var duration = (item.EndTime.Value - item.StartTime.Value).TotalMilliseconds;       // duration = end time - start time
                    double startMilliseconds = (item.StartTime.Value - pixelLine.Start.Value).TotalMilliseconds;    // start ms = start time - timepixelLine start time
                    UInt16 mask = item.LaserOn ? (ushort)0xffff : (ushort)0;
                    var imageFile = new PixelImageFile(path, (UInt16)item.Stretching, (UInt32)duration, (UInt32)startMilliseconds, mask);
                    var image = new ImagesCollectionItem(path, targetHeight, depth);
                    imageFile.Brightness = image.Brightness = item.Brightness;
                    imagesCollection.Add(image);

                    file.Images.Add(imageFile);
                    currentTime = item.EndTime.Value;
                }
                project.TimeLines.Add(file);
            }

            return project;
        }


        public List<byte> GetVideoBinaryPresentation(IEnumerable<TimeLineViewModel> lines)
        {
            ColorDepth depth = ColorDepth.Bit24;
            try { depth = (ColorDepth)ColorDepth.Parse(typeof(ColorDepth), Settings.Default.ColorDepth); }
            catch { }

            var images = new List<ImagesCollectionItem>();
            PixelProjectFile project = null;
            if (lines.Count() > 1)
            {
                project = CreateVideoProjectFile(lines.ElementAt(0), lines.ElementAt(1), images, depth);
            }
            else
            {
                project = CreateVideoProjectFile(lines.ElementAt(0), images, depth);
            }
            ImagesCollection c = new ImagesCollection(images);
            var buf = project.GetBinary(c);
#if(DEBUG)
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".\\Logs");
            Directory.CreateDirectory(logFolder);
            string projectFileName = Path.Combine(logFolder, "video-project " + DateTime.Now.ToString("dd_MM_yyyy___HH_mm_ss_fff") + ".bin");
            File.WriteAllBytes(projectFileName, buf.ToArray());
#endif
            return buf;
        }

        private struct DoubleTimeLineItem
        {
            public VideoThumbnailViewModel First;
            public VideoThumbnailViewModel Second;
        }

        private PixelProjectFile CreateVideoProjectFile(
            TimeLineViewModel channel1,
            TimeLineViewModel channel2,
            IList<ImagesCollectionItem> imagesCollection,
            ColorDepth depth)
        {
            var project = new PixelProjectFile();
            var videoLine1 = channel1 as VideoTimeLineViewModel;
            var videoLine2 = channel2 as VideoTimeLineViewModel;

            if (videoLine1?.MetaData == null
                || videoLine1.MetaData.OrderedLeds.Count == 0
                || videoLine1.Items.Count == 0
                || !videoLine1.IsSelectedToUpload)
                return project;

            if (videoLine2?.MetaData == null
                || videoLine2.MetaData.OrderedLeds.Count == 0
                || videoLine2.Items.Count == 0
                || !videoLine2.IsSelectedToUpload)
                return project;

            var file = new PixelTimelineFile()
            {
                TimeLineHeight = (ushort) (videoLine1.MetaData.OrderedLeds.Count + videoLine2.MetaData.OrderedLeds.Count)
            };

            List<ITimeLineDataItem> timelineItems1 = FillGabs(videoLine1.Items, videoLine1.Start.Value, CreateBlankVideoItem, videoLine1.LineHeight);
            List<ITimeLineDataItem> timelineItems2 = FillGabs(videoLine2.Items, videoLine2.Start.Value, CreateBlankVideoItem, videoLine2.LineHeight);

            List<DateTime?> intervals = GetSyncedIntervals(timelineItems1, timelineItems2);
            
            timelineItems1 = SplitItems(timelineItems1, intervals);
            timelineItems2 = SplitItems(timelineItems2, intervals);

            List<DoubleTimeLineItem> doubleItems = new List<DoubleTimeLineItem>();
            for (int i = 0; i < intervals.Count; i++) doubleItems.Add(new DoubleTimeLineItem()
            {
                First = (VideoThumbnailViewModel)timelineItems1[i],
                Second = (VideoThumbnailViewModel)timelineItems2[i]
            });
            
            foreach (DoubleTimeLineItem dItem in doubleItems)
            {
                string path1 = dItem.First.FullName.ToLower();
                string path2 = dItem.Second.FullName.ToLower();

                var duration = (dItem.First.EndTime.Value - dItem.First.StartTime.Value).TotalMilliseconds;       // duration = end time - start time
                double startMilliseconds = (dItem.First.StartTime.Value - videoLine1.Start.Value).TotalMilliseconds;           // start ms = start time - timeline start time
                UInt16 mask = dItem.First.LaserOn ? (ushort)1 : (ushort)0;

                var imageFile = new VideoImageFile(path1, 50, (UInt32)duration, (UInt32)startMilliseconds, mask);
                var image = new ImagesCollectionVideoDoubleItem(path1, path2, 
                    videoLine1.MetaData.OrderedLeds, videoLine2.MetaData.OrderedLeds, 
                    dItem.First.XOffset, dItem.First.YOffset,
                    dItem.Second.XOffset, dItem.Second.YOffset,
                    depth, dItem.First.Brightness, dItem.Second.Brightness );

                imageFile.Brightness = image.Brightness = dItem.First.Brightness;
                imagesCollection.Add(image);

                file.Images.Add(imageFile);
            }
            project.TimeLines.Add(file);
            return project;
        }

        public List<byte> GetFanBinaryPresentation(TimeLineViewModel line)
        {
            ColorDepth depth = ColorDepth.Bit24;
            ushort height = (ushort)((PixelTimeLineViewModel) line).LineHeight;
            try { depth = (ColorDepth)ColorDepth.Parse(typeof(ColorDepth), Settings.Default.ColorDepth); }
            catch { }

            var images = new List<ImagesCollectionItem>();
            PixelProjectFile project = null;
            project = CreateFanProjectFile(line, images, height, depth);
            ImagesCollection c = new ImagesCollection(images);
            var buf = project.GetBinary(c, height);
#if(DEBUG)
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".\\Logs");
            Directory.CreateDirectory(logFolder);
            string projectFileName = Path.Combine(logFolder, "video-project " + DateTime.Now.ToString("dd_MM_yyyy___HH_mm_ss_fff") + ".bin");
            File.WriteAllBytes(projectFileName, buf.ToArray());
#endif
            return buf;
        }

        private PixelProjectFile CreateFanProjectFile(TimeLineViewModel line,
            IList<ImagesCollectionItem> imagesCollection,
            ushort targetHeight,
            ColorDepth depth)
        {
            var project = new PixelProjectFile();
            var file = new PixelTimelineFile();
            var pixelLine = line as PixelTimeLineViewModel;

            var currentTime = line.Start.Value;

            List<ITimeLineDataItem> timelineItems = FillGabs(line.Items, line.Start.Value, CreateBlankPixelItem, pixelLine.LineHeight * 6);
            foreach (TimeLineItemViewModel item in timelineItems)
            {
                string path = item.FullName.ToLower();
                var duration = (item.EndTime.Value - item.StartTime.Value).TotalMilliseconds;       // duration = end time - start time
                double startMilliseconds = (item.StartTime.Value - line.Start.Value).TotalMilliseconds;    // start ms = start time - timeline start time
                UInt16 mask = item.LaserOn ? (ushort)0xffff : (ushort)0;
                var imageFile = new PixelFanImageFile(path, (UInt16)item.Stretching, (UInt32)duration, (UInt32)startMilliseconds, mask);
                var image = new ImagesCollectionItem(path, targetHeight, depth);
                imageFile.Brightness = image.Brightness = item.Brightness;
                imagesCollection.Add(image);

                file.Images.Add(imageFile);
                currentTime = item.EndTime.Value;
            }
            project.TimeLines.Add(file);

            return project;
        }

        private List<ITimeLineDataItem> SplitItems(List<ITimeLineDataItem> timelineItems1, List<DateTime?> intervals)
        {
            List<ITimeLineDataItem> result = new List<ITimeLineDataItem>();
            for (int i = 0; i < timelineItems1.Count; i++)
            {
                foreach (var endPoint in intervals)
                {
                    if (endPoint > timelineItems1[i].StartTime && endPoint <= timelineItems1[i].EndTime)
                        result.Add((ITimeLineDataItem)timelineItems1[i].Clone());

                    if (endPoint > timelineItems1[i].EndTime) break;
                }
            }

            for (int i = 0; i < intervals.Count - 1; i++)
                result[i + 1].StartTime = result[i].EndTime = intervals[i];

            return result;
        }

        private List<DateTime?> GetSyncedIntervals(List<ITimeLineDataItem> timelineItems1, List<ITimeLineDataItem> timelineItems2)
        {
            var result = new SortedList<DateTime, DateTime?>();
            foreach (var item in timelineItems1) result.Add(item.EndTime.Value, item.EndTime);
            foreach (var item in timelineItems2)
                if (!result.ContainsKey(item.EndTime.Value))
                    result.Add(item.EndTime.Value, item.EndTime);

            return new List<DateTime?>(result.Values);
        }

        private PixelProjectFile CreateVideoProjectFile(
            TimeLineViewModel line,
            IList<ImagesCollectionItem> imagesCollection,
            ColorDepth depth)
        {
            var project = new PixelProjectFile();
            var videoLine = line as VideoTimeLineViewModel;

            if (videoLine?.MetaData == null
                || videoLine.MetaData.OrderedLeds.Count == 0 
                || line.Items.Count == 0 
                || !line.IsSelectedToUpload)
                return project;
    
            var file = new PixelTimelineFile() { TimeLineHeight = (ushort)videoLine.MetaData.OrderedLeds.Count};
                
            List<ITimeLineDataItem> timelineItems = FillGabs(line.Items, line.Start.Value, CreateBlankVideoItem, videoLine.LineHeight);
            foreach (VideoThumbnailViewModel item in timelineItems)
            {
                string path = item.FullName.ToLower();
                var duration = (item.EndTime.Value - item.StartTime.Value).TotalMilliseconds;       // duration = end time - start time
                double startMilliseconds = (item.StartTime.Value - line.Start.Value).TotalMilliseconds;    // start ms = start time - timeline start time
                UInt16 mask = item.LaserOn ? (ushort)1 : (ushort)0;
                var imageFile = new VideoImageFile(path, 50, (UInt32)duration, (UInt32)startMilliseconds, mask);
                var image = new ImagesCollectionVideoItem(path, videoLine.MetaData.OrderedLeds, depth, item.XOffset, item.YOffset, item.Brightness);
                imageFile.Brightness = image.Brightness = item.Brightness;
                imagesCollection.Add(image);

                file.Images.Add(imageFile);
            }
            project.TimeLines.Add(file);
            
            return project;
        }

        public List<byte> GetEpicBinaryPresentation(IEnumerable<TimeLineViewModel> lines)
        {
            ColorDepth depth = ColorDepth.Bit24;
            try { depth = (ColorDepth)ColorDepth.Parse(typeof(ColorDepth), Settings.Default.ColorDepth); }
            catch { }

            var gradients = new List<GradientsCollectionItem>();
            GradientProjectFile project = CreateEpicProjectFile(lines, gradients, depth);
            var c = new GradientsCollection(gradients);
            var buf = project.GetBinary(c);
#if(DEBUG)
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".\\Logs");
            Directory.CreateDirectory(logFolder);
            string projectFileName = Path.Combine(logFolder, DateTime.Now.ToString("simple-project-dd_MM_yyyy___HH_mm_ss_fff") + ".bin");
            File.WriteAllBytes(projectFileName, buf.ToArray());
#endif
            return buf;
        }

        private GradientProjectFile CreateEpicProjectFile(
            IEnumerable<TimeLineViewModel> lines,
            IList<GradientsCollectionItem> imagesCollection, 
            ColorDepth depth)
        {
            var project = new GradientProjectFile();
            foreach (TimeLineViewModel line in lines)
            {
                var epicLine = line as EpicPoiTimeLineViewModel;
                if (epicLine == null || line.Items.Count == 0 || !line.IsSelectedToUpload) continue;

                var file = new GradientTimeLineFile();
                file.TimelineColor = System.Drawing.Color.FromArgb(255, epicLine.LineColorTag.R, epicLine.LineColorTag.G,
                    epicLine.LineColorTag.B);

                var currentTime = line.Start.Value;
                List<ITimeLineDataItem> timelineItems = FillGabs(line.Items, line.Start.Value, CreateBlankGradientItem);
                foreach (TimeLineItemViewModel item in timelineItems)
                {
                    string path = item.FullName.ToLower();
                    double startMilliseconds = 0;

                    var gapDuration = (item.StartTime.Value - currentTime).TotalMilliseconds;
                    if (gapDuration > 0)    // if start time of the next image doesn't match end time of the current one
                    {
                        startMilliseconds = (item.StartTime.Value - line.Start.Value).TotalMilliseconds;
                        var blankImageFile = new GradientImageFile(null, (UInt16)item.Stretching, (UInt32)gapDuration, (UInt32)startMilliseconds, 0);
                        file.Images.Add(blankImageFile);
                        imagesCollection.Add(new GradientsCollectionItem(null, 1, depth));
                    }

                    var duration = (item.EndTime.Value - item.StartTime.Value).TotalMilliseconds;       // duration = end time - start time
                    startMilliseconds = (item.StartTime.Value - line.Start.Value).TotalMilliseconds;    // start ms = start time - timeline start time
                    UInt16 mask = item.LaserOn ? (ushort)1 : (ushort)0;
                    var imageFile = new GradientImageFile(path, (UInt16)item.Stretching, (UInt32)duration, (UInt32)startMilliseconds, mask);
                    var image = new GradientsCollectionItem(path, 1, depth);
                    imageFile.Brightness = image.Brightness = item.Brightness;
                    imagesCollection.Add(image);

                    file.Images.Add(imageFile);
                    currentTime = item.EndTime.Value;
                }
                project.TimeLines.Add(file);
            }

            return project;
        }

        public List<byte> GetLightBinaryPresentation(IEnumerable<TimeLineViewModel> lines)
        {
            LightProjectFile project = new LightProjectFile();
            byte slaveAddress = 1;
            foreach (TimeLineViewModel line in lines)
            {
                if (!(line is LightTimeLineViewModel) || line.Items.Count == 0 || !line.IsSelectedToUpload) continue;
                var binaryLine = new LightTimelineFile();
                binaryLine.IsMonochrome = (line as LightTimeLineViewModel).IsMonochrome;
                binaryLine.SubmoduleAddress = slaveAddress;

                List<ITimeLineDataItem> timelineItems = FillGabs(line.Items, line.Start.Value, CreateBlankLightItem);
                foreach (TimeLineItemViewModel item in timelineItems)
                {
                    if (item is SolidColorThumbnailViewModel)
                    {
                        var file = new LightColorFile(item.ContentID, item.StartTime.Value, item.EndTime.Value);
                        var c = (item as SolidColorThumbnailViewModel).BackgroundColor;
                        file.Color = System.Drawing.Color.FromArgb(255, c.R, c.G, c.B);
                        binaryLine.Images.Add(file);
                    }
                    else if (item is EffectThumbnailViewModel)
                    {
                        var file = new LightEffectFile(item.ContentID, item.StartTime.Value, item.EndTime.Value);
                        var vm = (EffectThumbnailViewModel)item;
                        file.ColorFirst = System.Drawing.Color.FromArgb(255, vm.ColorFirst.R, vm.ColorFirst.G, vm.ColorFirst.B);
                        file.ColorLast = System.Drawing.Color.FromArgb(255, vm.ColorLast.R, vm.ColorLast.G, vm.ColorLast.B);
                        file.T1 = vm.T1.HasValue ? vm.T1.Value : 0;
                        file.T2 = vm.T2.HasValue ? vm.T2.Value : 0;
                        binaryLine.Images.Add(file);
                    }
                }
                project.TimeLines.Add(binaryLine);
                slaveAddress++;
            }

            var buf = project.GetBinary(Settings.Default.Discreteness);
#if(DEBUG)
            string projectFileName = "light-project " + DateTime.Now.ToString("dd_MM_yyyy___HH_mm_ss_fff") + ".bin";
            File.WriteAllBytes(projectFileName, buf.ToArray());
#endif
            return buf;
        }

        delegate ITimeLineDataItem CreateBlankItemDelegate(DateTime start, DateTime end, int height = 0);

        private List<ITimeLineDataItem> FillGabs(ObservableCollection<ITimeLineDataItem> items, DateTime startTime, CreateBlankItemDelegate CreateBlankItem, int targetHeight = 0)
        {
            var result = new List<ITimeLineDataItem>();
            
            var firstItem = items.First();
            int height = targetHeight != 0 ? targetHeight : (firstItem as TimeLineItemViewModel).ActualHeight;

            if (firstItem != null)
            {
                if (startTime != firstItem.StartTime)
                    result.Add(CreateBlankItem(startTime, firstItem.StartTime.Value, height));

                for (int i = 0; i < items.Count - 1; i++)
                {
                    result.Add(items[i]);
                    if (items[i].EndTime != items[i + 1].StartTime)
                    {
                        result.Add(CreateBlankItem(items[i].EndTime.Value, items[i + 1].StartTime.Value, height));
                    }
                }
                result.Add(items.Last());
            }
            return result;
        }

        private SolidColorThumbnailViewModel CreateBlankLightItem(DateTime begin, DateTime end, int height = 0)
        {
            var item = new SolidColorThumbnailViewModel(null, "Solid Color");
            item.StartTime = begin;
            item.EndTime = end;
            return item;
        }

        private PixelThumbnailViewModel CreateBlankPixelItem(DateTime begin, DateTime end, int height = 0)
        {
            var item = new PixelThumbnailViewModel(new FileInfo("black.jpg"));
            item.StartTime = begin;
            item.EndTime = end;
            item.ReplaceWithBlank(height);
            return item;
        }
        
        private GradientThumbnailViewModel CreateBlankGradientItem(DateTime begin, DateTime end, int height = 0)
        {
            var item = new GradientThumbnailViewModel();
            item.StartTime = begin;
            item.EndTime = end;
            return item;
        }

        private VideoThumbnailViewModel CreateBlankVideoItem(DateTime begin, DateTime end, int height = 0)
        {
            var item = new VideoThumbnailViewModel();
            item.ReplaceWithBlank(height);
            item.StartTime = begin;
            item.EndTime = end;
            return item;
        }

        #endregion

        [XmlIgnore]
        public static List<Type> ExtraTypes
        {
            get
            {
                var result = new List<Type>()
                {
                    typeof(CanvasViewModel),
                    typeof(CanvasElementViewModel),
                    typeof(LedTile),
                    typeof(TimeLineViewModel),
                    typeof(EpicPoiTimeLineViewModel),
                    typeof(LightTimeLineViewModel),
                    typeof(PixelTimeLineViewModel),
                    typeof(VideoTimeLineViewModel)
                };
                result.AddRange(TimeLineViewModel.ExtraTypes);
                return result;
            }
        }

        public override string ToString()
        {
            return String.Format("{0} < {1} >", this.FileName, this.FilePath);
        }

        public static double GetScaleRate(double delta, double unitSize)
        {
            var result = delta / 50;
            if (unitSize > 100) result = delta / 10;
            if (unitSize > 500) result = delta / 2;
            if (unitSize > 1000) result = delta;
            return result;
        }

        #region Selection

        List<TimeLineItemViewModel> _selectedItems = new List<TimeLineItemViewModel>();

        internal void SetSelection(object dataContext, bool ctrlHold, bool shiftHold, bool altHold)
        {
            if (!ctrlHold && !shiftHold) ClearSelection();

            TimeLineViewModel selectedLine = null;
            foreach (var line in TimeLines)
            {
                selectedLine = line.IsSelected ? line : null;
                if (selectedLine != null) break;
            }

            if (dataContext is TimeLineViewModel)
            {
                var sender = (dataContext as TimeLineViewModel);
                sender.IsSelected = true;
                selectedLine = sender;
            }
            else if (dataContext is TimeLineItemViewModel)
            {
                var item = (dataContext as TimeLineItemViewModel);
                if (_selectedItems.Count > 0)
                {
                    if (selectedLine != null)
                    {
                        if (item.IsSelected && ctrlHold)
                        {
                            _selectedItems.Remove(item);
                            item.IsSelected = false;
                        }
                        else if (shiftHold)
                        {
                            List<TimeLineItemViewModel> selectionRange = GetSelectionRange(item);

                            ClearSelection();

                            selectedLine.IsSelected = true;
                            _selectedItems.AddRange(selectionRange);
                            selectionRange.ForEach(x => x.IsSelected = true);
                        }
                        else
                        {
                            _selectedItems.Add(item);
                            item.IsSelected = true;
                        }
                    }
                }
                else
                {
                    _selectedItems.Add(item);
                    item.IsSelected = true;
                    TimeLines.First(x => x.Items.Contains(item)).IsSelected = true;
                }
                if (item.IsSelected)
                {
                    RootViewModel.This.ProjectPlayerModel.DisplayModel = item;
                }
            }
        }

        private List<TimeLineItemViewModel> GetSelectionRange(TimeLineItemViewModel item)
        {
            List<TimeLineItemViewModel> result = new List<TimeLineItemViewModel>();
            if (_selectedItems.Count < 1)
            {
                result.Add(item);
            }
            else
            {
                TimeLineViewModel line = TimeLines.First(x => x.Items.Contains(item));
                int idx1 = line.Items.IndexOf(item);
                int idx2 = line.Items.IndexOf(_selectedItems.First());
                // to keep first selected item at the beginning of list:
                if (idx1 < idx2)
                {
                    for (int i = idx2; i >= idx1; i--) result.Add((TimeLineItemViewModel)line.Items[i]);
                }
                else
                {
                    for (int i = idx2; i <= idx1; i++) result.Add((TimeLineItemViewModel)line.Items[i]);
                }
            }
            return result;
        }

        internal void CopySelectedItems()
        {
            RootViewModel.This.CopiedItems.Clear();
            RootViewModel.This.CopiedTimeline = null;
            _selectedItems = _selectedItems.OrderBy(i => i.StartTime).ToList();
            foreach (var i in _selectedItems) RootViewModel.This.CopiedItems.Add((TimeLineItemViewModel)i.Clone());
            if (RootViewModel.This.CopiedItems.Count == 0)
            {
                TimeLineViewModel tl = null;
                try { tl = TimeLines.First((line) => { return line.IsSelected == true; }); }
                catch { }
                if (tl != null) RootViewModel.This.CopiedTimeline = tl;
            }
            ClearSelection();
        }

        internal void PasteSelectedItems()
        {
            if (RootViewModel.This.CopiedTimeline != null)
            {
                var tl = (TimeLineViewModel)RootViewModel.This.CopiedTimeline.Clone();
                PushUndo(false);
                TimeLines.Add(tl);
            }

            TimeLineViewModel selectedLine = SelectedTimeline;
            if (selectedLine != null)
            {
                ITimeLineDataItem selected = null;
                foreach (ITimeLineDataItem i in selectedLine.Items)
                    if (i.IsSelected)
                    {
                        selected = i;
                        break;
                    }

                int insertIndex = selectedLine.Items.Count;
                if (selected != null) insertIndex = selectedLine.Items.IndexOf(selected);

                foreach (var item in RootViewModel.This.CopiedItems)
                {
                    var pixelLine = selectedLine as PixelTimeLineViewModel;
                    if (pixelLine != null && (item.ActualHeight != pixelLine.LineHeight && pixelLine.LineHeight != 0))
                    {
                        MessageBox.Show(((String)Application.Current.TryFindResource("Global_ImagesHeightMismatch")).Replace("\"", ""));
                        return;
                    }
                }
                selectedLine.InsertItems(RootViewModel.This.CopiedItems, insertIndex);
            }
        }

        internal void DeleteSelectedItems()
        {
            PushUndo(false);
            _selectedItems.ForEach(i => {
                i.IsSelected = false;
                foreach(var l in TimeLines)
                {
                    l.Items.Remove(i);
                }
            });
            _selectedItems.Clear();
        }

        private void ClearSelection()
        {
            foreach (var t in TimeLines) t.IsSelected = false;
            _selectedItems.ForEach(i => { i.IsSelected = false; });
            _selectedItems.Clear();
        }

        #endregion

        protected void OnFileChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (FileChanged != null)
                FileChanged(sender, e);
        }

        public void Dispose()
        {
            if (SoundPlayer is NAudioEngine)
                (SoundPlayer as NAudioEngine).Dispose();
        }

        public event EventHandler FileChanged;

        public void RaiseFileChanged()
        {
            OnFileChanged(this, null);
        }
    }
}
