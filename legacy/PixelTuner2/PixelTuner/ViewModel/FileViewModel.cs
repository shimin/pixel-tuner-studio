using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

using System.Windows.Media;
using System.Xml.Serialization;

using Communication.Pixelate;
using Communication.Service;
using PixelTuner.Properties;
using Common.Collections;

namespace PixelTuner.ViewModel
{
    public class FileViewModel : PaneViewModel
    {
        public FileViewModel()
            : this(Path.GetTempFileName())
        {
            IsDirty = true;
        }

        public FileViewModel(string filePath)
        {
            FilePath = filePath;
            Title = FileName;
            _timeLines.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(
                _timeLines_CollectionChanged);

            Devices = new SafeObservable<PixelDeviceViewModel>();
            FileChanged += (x, e) => { IsDirty = true; };
            DeviceHelper.Devices.ForEach((d) => { DeviceHelper_DeviceFound(new DeviceEventArgs(d)); });
            DeviceHelper.DeviceFound += new DeviceHelper.DeviceChangedDelegate(DeviceHelper_DeviceFound);
            DeviceHelper.DeviceLost += new DeviceHelper.DeviceChangedDelegate(DeviceHelper_DeviceLost);
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

        void _timeLines_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ClearSelection();

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                ((TimeLineViewModel)e.NewItems[e.NewItems.Count - 1]).IsSelected = true;

            for (int i = 0; i < _timeLines.Count; i++)
            {
                if (i % 2 == 0) _timeLines[i].Background =  _lightLineColor;
                else _timeLines[i].Background = _darkLineColor;
                _timeLines[i].Parent = this;
                _timeLines[i].Items.CollectionChanged -= TimeLineItems_CollectionChanged;
                _timeLines[i].Items.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(TimeLineItems_CollectionChanged);
            }
            OnFileChanged(sender, e);
        }

        void TimeLineItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged("LineHeightChangingAvailable");
            OnFileChanged(sender, e);
        }

        #region Properties

        [XmlIgnore]
        public SafeObservable<PixelDeviceViewModel> Devices
        {
            get;
            private set;
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

        Brush _darkLineColor = new SolidColorBrush(Color.FromArgb(255, 64, 64, 64));

        Brush _lightLineColor = new SolidColorBrush(Color.FromArgb(255, 70, 70, 70));

        Double _minUnitSize = 1.0;
        [XmlAttribute]
        public Double MinUnitSize
        {
            get { return _minUnitSize; }
            set
            {
                _minUnitSize = value;
                RaisePropertyChanged("MinUnitSize");
            }
        }

        Double _maxUnitSize = 160.0;
        [XmlAttribute]
        public Double MaxUnitSize
        {
            get { return _maxUnitSize; }
            set
            {
                _maxUnitSize = value;
                RaisePropertyChanged("MaxUnitSize");
            }
        }

        [XmlAttribute]
        public Double UnitSize
        {
            get { return Settings.Default.UnitSize; }
            set
            {
                Settings.Default.UnitSize = value;
                RaisePropertyChanged("UnitSize");
            }
        }

        int _lineHeight = Settings.Default.LineHeight;
        [XmlAttribute]
        public int LineHeight
        {
            get { return _lineHeight; }
            set
            {
                _lineHeight = value;
                Settings.Default.LineHeight = value;
                RaisePropertyChanged("LineHeight");
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
        public bool LineHeightChangingAvailable
        {
            get
            {
                foreach (var line in TimeLines)
                    if (line.Items.Count > 0)
                        return false;
                return true;
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

        public TimeLineViewModel SelectedTimeline
        {
            get
            {
                foreach (var t in TimeLines) if (t.IsSelected) return t;
                return null;
            }
        }

        #endregion

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
            RootViewModel.This.Close(this);
        }
        #endregion

        #region AddLineCommand
        RelayCommand _addLineCommand = null;
        public ICommand AddLine
        {
            get
            {
                if (_addLineCommand == null)
                {
                    _addLineCommand = new RelayCommand((p) => OnAddLine(p), (p) => CanAddLine(p));
                }

                return _addLineCommand;
            }
        }

        private bool CanAddLine(object parameter)
        {
            return TimeLines.Count < 254;
        }

        private void OnAddLine(object parameter)
        {
            _timeLines.Add(new TimeLineViewModel());
        }

        #endregion 
        
        public int EvaluateBinarySize(byte targetHeight)
        {
            var knownPaths = new List<string>();

            ColorDepth depth;
            var descriptors = new List<ImagesCollectionItem>();
            PixelProjectFile project = CreateProjectFile(descriptors, (byte)targetHeight, out depth, Settings.Default.CollapseGaps);
            ImagesCollection c = new ImagesCollection(descriptors);
            return project.GetBinarySize(c, targetHeight);
        }

        public List<byte> GetBinaryPresentation(int targetHeight)
        {
            var knownPaths = new List<string>();
            byte projectHeight = (byte)targetHeight;

            ColorDepth depth;
            var descriptors = new List<ImagesCollectionItem>();
            PixelProjectFile project = CreateProjectFile(descriptors, (byte)targetHeight, out depth, Settings.Default.CollapseGaps);
            ImagesCollection c = new ImagesCollection(descriptors);
            var buf = project.GetBinary(c, projectHeight);
#if(DEBUG)
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".\\Logs");
            Directory.CreateDirectory(logFolder);
            string projectFileName = Path.Combine(logFolder, DateTime.Now.ToString("dd_MM_yyyy___HH_mm_ss_fff") + ".bin");
            File.WriteAllBytes(projectFileName, buf.ToArray());
#endif
            return buf;
        }

        private PixelProjectFile CreateProjectFile(List<ImagesCollectionItem> descriptors, byte targetHeight, out ColorDepth depth, bool collapseGaps = true)
        {
            var project = new PixelProjectFile();
            
            depth = ColorDepth.Bit24;
            try { depth = (ColorDepth)ColorDepth.Parse(typeof(ColorDepth), Settings.Default.ColorDepth); }
            catch { }

            foreach (TimeLineViewModel tl in _timeLines)
            {
                if (tl.Items.Count == 0) continue;
                var tlf = new PixelTimelineFile();

                var start = tl.Start.Value;
                foreach (PixelTuner.ViewModel.TimeLineItemViewModel item in tl.Items)
                {
                    string path = item.FullName.ToLower();
                    if (!collapseGaps && (item.StartTime.Value - start).TotalMilliseconds > Settings.Default.GapMinimumSize)
                    {
                        UInt16 gapDuration = (UInt16)((item.StartTime.Value - start).TotalMilliseconds / 100);
                        var blankImage = new PixelImageFile(null, (UInt16)item.Stretching, gapDuration);
                        tlf.Images.Add(blankImage);
                        descriptors.Add(new ImagesCollectionItem(null, targetHeight, depth));
                    }
                    DateTime startTimeStamp = collapseGaps ? start : item.StartTime.Value;
                    UInt16 duration = (UInt16)(((TimeSpan)(item.EndTime - startTimeStamp)).TotalMilliseconds / 100);
                    var imgf = new PixelImageFile(path, (UInt16)item.Stretching, duration);
                    descriptors.Add(new ImagesCollectionItem(path, targetHeight, depth));
                    imgf.Brightness = descriptors[descriptors.Count - 1].Brightness = item.Brightness;
                    descriptors[descriptors.Count - 1].LaserOn = item.LaserOn;
                    tlf.Images.Add(imgf);
                    start = item.EndTime.Value;
                }
                project.TimeLines.Add(tlf);
            }

            return project;
        }

        //private PixelProjectFile CreateProjectFile(List<string> knownPaths, out ColorDepth depth, bool collapseGaps = true)
        //{
        //    var project = new PixelProjectFile();
        //    foreach (TimeLineViewModel tl in _timeLines)
        //    {
        //        if (tl.Items.Count == 0) continue;
        //        var tlf = new PixelTimelineFile();
                
        //        var start = tl.Start.Value;
        //        foreach (PixelTuner.ViewModel.TimeLineItemViewModel item in tl.Items)
        //        {
        //            string path = item.FullName.ToLower();
        //            if (!collapseGaps && (item.StartTime.Value - start).TotalMilliseconds > Settings.Default.GapMinimumSize)
        //            {
        //                UInt16 gapDuration = (UInt16)((item.StartTime.Value - start).TotalMilliseconds / 100);
        //                var blankImage = new PixelImageFile(null, (UInt16)item.Stretching, gapDuration);
        //                knownPaths.Add(null);
        //                tlf.Images.Add(blankImage);
        //            }
        //            DateTime startTimeStamp = collapseGaps ? start : item.StartTime.Value;
        //            UInt16 duration = (UInt16)(((TimeSpan)(item.EndTime - startTimeStamp)).TotalMilliseconds / 100);
        //            var imgf = new PixelImageFile(path, (UInt16)item.Stretching, duration);
        //            knownPaths.Add(path);
        //            tlf.Images.Add(imgf);
        //            start = item.EndTime.Value;
        //        }
        //        project.TimeLines.Add(tlf);
        //    }
        //    depth = ColorDepth.Bit24;

        //    try { depth = (ColorDepth)ColorDepth.Parse(typeof(ColorDepth), Settings.Default.ColorDepth); }
        //    catch { }

        //    return project;
        //}

        [XmlIgnore]
        public static List<Type> ExtraTypes
        {
            get
            {
                var result = new List<Type>()
                {
                    typeof(TimeLineViewModel)
                };
                result.AddRange(TimeLineViewModel.ExtraTypes);
                return result;
            }
        }

        public int MemoryListHeight
        {
            get
            {
                return DeviceHelper.Devices.Count != 0 ? DeviceHelper.Devices.Count * 15 + 20 : 0;
            }
        }

        public override string ToString()
        {
            return String.Format("{0} < {1} >", this.FileName, this.FilePath);
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
                    if (selectedLine != null && item.Parent == selectedLine)
                    {
                        if (item.IsSelected && ctrlHold)
                        {
                            _selectedItems.Remove(item);
                            item.IsSelected = false;
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
                    ((TimeLineViewModel)item.Parent).IsSelected = true;
                }
            }
        }

        internal void CopySelectedItems()
        {
            RootViewModel.This.CopiedItems.Clear();
            _selectedItems = _selectedItems.OrderByDescending(i => i.StartTime).ToList();
            foreach (var i in _selectedItems) RootViewModel.This.CopiedItems.Add((TimeLineItemViewModel)i.Clone());
            ClearSelection();
        }

        internal void PasteSelectedItems()
        {
            TimeLineViewModel selectedLine = null;
            foreach (var line in TimeLines)
            {
                selectedLine = line.IsSelected ? line : null;
                if (selectedLine != null) break;
            }
            if (selectedLine != null)
            {
                TimeLineItemViewModel selected = null;
                foreach (TimeLineItemViewModel i in selectedLine.Items)
                    if (i.IsSelected)
                    {
                        selected = i;
                        break;
                    }

                int insertIndex = selectedLine.Items.Count;
                if (selected != null) insertIndex = selectedLine.Items.IndexOf(selected);

                foreach (var i in RootViewModel.This.CopiedItems)
                {
                    var clone = (TimeLineItemViewModel)i.Clone();
                    selectedLine.Items.Insert(insertIndex, clone);
                }
                selectedLine.TrimTimeSpaces();
            }
        }

        internal void DeleteSelectedItems()
        {
            _selectedItems.ForEach(i => { ((TimeLineViewModel)i.Parent).Items.Remove(i); });
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
        public event EventHandler FileChanged;

    }
}
