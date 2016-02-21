using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

using PixelTuner.Properties;
using Common.Controls;
using PixelTuner.ViewModel.Serialization;
using System.Windows.Media;
using Common.Imaging;

namespace PixelTuner.ViewModel
{
    public class TimeLineItemViewModel : FileSystemInfo, ITimeLineDataItem, INotifyPropertyChanged, IConvertible
    {
        FileInfo _fi = null;

        public TimeLineItemViewModel()
            : this(null)
        {
        }

        public TimeLineItemViewModel(FileInfo fi)
        {
            RootViewModel.ApplicationSettingsChanged += new EventHandler(RootViewModel_ApplicationSettingsChanged);
            _fi = fi;
        }

        void RootViewModel_ApplicationSettingsChanged(object sender, EventArgs e)
        {
            RaisePropertyChanged("StartIcon");
            RaisePropertyChanged("EndIcon");
            RaisePropertyChanged("Start");
            RaisePropertyChanged("End");
        }
        
        #region Properties

        public object StartIcon
        {
            get
            {
                BitmapImage image = ImageHelper.CreateImage(new Uri("pack://application:,,,/Images/timeStart.png", UriKind.Absolute));
                if (image == null) return DependencyProperty.UnsetValue;
                return image;
            }
        }

        public object EndIcon
        {
            get
            {
                string path = "pack://application:,,,/Images/timeEnd.png";
                if (Settings.Default.TimeSpanMode) path = "pack://application:,,,/Images/timeLength.png";
                BitmapImage image = ImageHelper.CreateImage(new Uri(path, UriKind.Absolute));
                if (image == null) return DependencyProperty.UnsetValue;
                return image;
            }
        }

        DateTime? _startTime = DateTime.MinValue;
        public DateTime? StartTime
        {
            get { return _startTime; }
            set
            {
                if (value != _startTime)
                {
                    var tl = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
                    if (tl != null) tl.IsChanged = true;
                }
                _startTime = value;
                RaisePropertyChanged("StartTime");
                RaisePropertyChanged("Start");
            }
        }

        public string Start
        {
            get
            {
                return _startTime.HasValue ? String.Format("{0}:{1}:{2}.{3}", _startTime.Value.Hour.ToString("D2"),
                    _startTime.Value.Minute.ToString("D2"), _startTime.Value.Second.ToString("D2"),
                    _startTime.Value.Millisecond.ToString("D3"))
                    : "";
            }
        }

        DateTime? _endTime = null;
        public DateTime? EndTime
        {
            get
            {
                return _endTime;
            }
            set
            {
                if (value != _endTime)
                {
                    var tl = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
                    if (tl != null) tl.IsChanged = true;
                }
                _endTime = value;
                RaisePropertyChanged("EndTime");
                RaisePropertyChanged("End");
            }
        }

        public string End
        {
            get
            {
                DateTime? end = _endTime ?? DateTime.MinValue;
                if (Settings.Default.TimeSpanMode)
                {
                    var start = _startTime ?? DateTime.MinValue;
                    end = new DateTime((end.Value - start).Ticks);
                }
                return end.HasValue ? String.Format("{0}:{1}:{2}.{3}", end.Value.Hour.ToString("D2"),
                    end.Value.Minute.ToString("D2"), end.Value.Second.ToString("D2"),
                    end.Value.Millisecond.ToString("D3"))
                    : "";
            }
        }

        public System.Windows.Media.Brush NormalBackground
        {
            get
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(10, 10, 200));
            }
            set
            {

            }
        }

        public object ImageSource
        {
            get
            {
                BitmapImage image = ImageHelper.CreateImage(new Uri(FullName, UriKind.Absolute));
                if (image == null) return DependencyProperty.UnsetValue;
                return image;
            }
        }

        private bool _isSelected = false;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; RaisePropertyChanged("IsSelected"); }
        }

        bool _viewExpanded = false;
        public bool TimelineViewExpanded
        {
            get
            {
                return _viewExpanded;
            }
            set
            {
                _viewExpanded = value;
                RaisePropertyChanged("TimelineViewExpanded");
            }
        }

        int _stretching = Settings.Default.DefaultStretching;
        public int Stretching
        {
            get { return _stretching; }
            set
            {
                if (value != _stretching)
                {
                    var tl = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
                    if (tl != null) tl.IsChanged = true;
                }
                _stretching = value;
                RaisePropertyChanged("Stretching");
            }
        }

        byte _brightness = 100;
        public byte Brightness
        {
            get { return _brightness; }
            set { _brightness = value; RaisePropertyChanged("Brightness"); }
        }

        private bool _laserOn;
        public bool LaserOn
        {
            get { return _laserOn; }
            set { _laserOn = value; RaisePropertyChanged("LaserOnButtonColor"); }
        }

        public Brush LaserOnButtonColor
        {
            get
            {
                if (LaserOn) return new SolidColorBrush(Color.FromRgb(200, 140, 100));
                else return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            }
        }

        [XmlIgnore]
        public int ActualHeight
        {
            get { return ImageSource == null ? 0 : (int)((BitmapImage)ImageSource).PixelHeight; }
        }

        #endregion

        public object Clone()
        {
            TimeLineItemViewModel result = new TimeLineItemViewModel(this._fi);
            result.StartTime = this.StartTime;
            result.EndTime = this.EndTime;
            result.TimelineViewExpanded = this.TimelineViewExpanded;
            result.Stretching = this.Stretching;
            result.Brightness = this.Brightness;
            result.LaserOn = this.LaserOn;
            return result;
        }
        
        #region ReplaceCommand
        RelayCommand _replaceCommand = null;
        public ICommand ReplaceCommand
        {
            get
            {
                if (_replaceCommand == null)
                {
                    _replaceCommand = new RelayCommand((p) => OnReplace(p), (p) => CanReplace(p));
                }

                return _replaceCommand;
            }
        }

        private bool CanReplace(object parameter)
        {
            return true;
        }

        private void OnReplace(object parameter)
        {
            var w = new ReplaceItemWindow();
            w.Owner = Application.Current.MainWindow;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (w.ShowDialog() == true)
            {
                TimeLineItemViewModel newItem = w.SelectedItem as TimeLineItemViewModel;
                if (newItem != null)
                {
                    var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
                    if (p != null)
                    {
                        int idx = p.Items.IndexOf(this);
                        newItem.StartTime = this.StartTime;
                        newItem.EndTime = this.EndTime;
                        newItem.Stretching = this.Stretching;
                        p.Items.Remove(this);
                        p.Items.Insert(idx, newItem);
                        p.IsChanged = true;
                    }
                }
            }
        }

        #endregion
        
        #region SplitCommand
        RelayCommand _splitCommand = null;
        public ICommand SplitCommand
        {
            get
            {
                if (_splitCommand == null)
                {
                    _splitCommand = new RelayCommand((p) => OnSplit(p), (p) => CanSplit(p));
                }

                return _splitCommand;
            }
        }

        private bool CanSplit(object parameter)
        {
            return (this.EndTime.Value - this.StartTime.Value).TotalMilliseconds >= 100;
        }

        private void OnSplit(object parameter)
        {
            SplitItemWindow w = new SplitItemWindow(this);
            SetWindowPosition(w);

            if (w.ShowDialog() == true)
            {
                if (w.Duration < (this.EndTime - this.StartTime) && w.Duration.TotalMilliseconds > 50)
                    Split(w.Duration);
            }
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }

        private void Split(TimeSpan timeSpan)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null)
            {
                int idxToPaste = p.Items.IndexOf(this);
                var splittedPart = (TimeLineItemViewModel)this.Clone();
                this.StartTime = this.StartTime + timeSpan;
                splittedPart.EndTime = splittedPart.StartTime + timeSpan;
                p.Items.Insert(idxToPaste, splittedPart);
            }
        }

        #endregion

        #region RepeatCommand
        RelayCommand _repeatCommand = null;
        public ICommand RepeatCommand
        {
            get
            {
                if (_repeatCommand == null)
                {
                    _repeatCommand = new RelayCommand((p) => OnRepeat(p), (p) => CanRepeat(p));
                }

                return _repeatCommand;
            }
        }

        private bool CanRepeat(object parameter)
        {
            return true;
        }

        private void OnRepeat(object parameter)
        {
            RepeatItemWindow w = new RepeatItemWindow(this);
            SetWindowPosition(w);

            if (w.ShowDialog() == true)
            {
                this.Stretching = w.Stretching;
            }
            RootViewModel.NotifySettingsChanged();
        }

        #endregion

        #region DisposeMe
        RelayCommand _disposeMeCommand = null;
        public ICommand DisposeMe
        {
            get
            {
                if (_disposeMeCommand == null)
                {
                    _disposeMeCommand = new RelayCommand((p) => OnDisposeMe(p), (p) => CanDisposeMe(p));
                }

                return _disposeMeCommand;
            }
        }

        private bool CanDisposeMe(object parameter)
        {
            return true;
        }

        private void OnDisposeMe(object parameter)
        {
            ObservableCollection<ITimeLineDataItem> parentCollection = null;
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null)
            {
                p.IsChanged = true;
                System.Reflection.PropertyInfo pi = p.GetType().GetProperty("Items");
                if (pi != null) parentCollection = 
                    (ObservableCollection<ITimeLineDataItem>)pi.GetValue(p, null);
            }
            if (parentCollection != null)
            {
                parentCollection.Remove(this);
            }
        }

        #endregion 

        #region EditMe
        RelayCommand _editMeCommand = null;
        public ICommand EditMe
        {
            get
            {
                if (_editMeCommand == null)
                {
                    _editMeCommand = new RelayCommand((p) => OnEditMe(p), (p) => CanEditMe(p));
                }

                return _editMeCommand;
            }
        }

        private bool CanEditMe(object parameter)
        {
            return true;
        }

        private void OnEditMe(object parameter)
        {
            ItemWindow w = new ItemWindow(this);
            var location = Application.Current.MainWindow.PointToScreen( Mouse.GetPosition(Application.Current.MainWindow));
            w.Left = location.X - w.Width / 2;
            w.Top = location.Y - w.Height / 2;
            if (w.ShowDialog() == true)
            {
                ApplyChanges(w.Start, w.Duration, w.Stretching, w.Brightness, w.LaserOn, w.ForAll, w.DurationChanged, w.StretchingChanged, w.BrightnessChanged, w.LaserOnChanged);
            }
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }

        private void ApplyChanges(DateTime start, TimeSpan duration, 
            int stretching, byte brightness, bool laserOn, bool forAll, bool durationChanged,
            bool stretchChanged, bool brightnessChanged, bool laserOnChanged)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null)
            {
                int idx = p.Items.IndexOf(this) + 1;

                this.StartTime = start > p.Start.Value ? start : p.Start.Value;
                this.EndTime = this.StartTime + duration;
                this.Stretching = stretching;
                this.Brightness = brightness;
                this.LaserOn = laserOn;

                var thisIdx = p.Items.IndexOf(this);
                if (thisIdx < p.Items.Count - 1 && !forAll && this.EndTime.Value > p.Items[thisIdx + 1].StartTime.Value)
                    if (PixelTuner.Properties.Settings.Default.CutNextItems)
                    {
                        p.CutItemsWithinInterval(p.Items[thisIdx + 1].StartTime, this.EndTime);
                    }
                    else
                    {
                        TimeSpan moveInterval = EndTime.Value - p.Items[thisIdx + 1].StartTime.Value;
                        p.MoveItemsAfterIndex(p.Items[thisIdx + 1].StartTime, moveInterval);
                    }

                DateTime lastUsed = this.EndTime.Value;
                while (forAll && idx < p.Items.Count)
                {
                    var itm = (p.Items[idx] as TimeLineItemViewModel);
                    if (durationChanged)
                    {
                        p.Items[idx].StartTime = lastUsed;
                        p.Items[idx].EndTime = lastUsed.Add(duration);
                    }
                    if (stretchChanged) itm.Stretching = stretching;
                    if (brightnessChanged) itm.Brightness = brightness;
                    if (laserOnChanged) itm.LaserOn = laserOn;
                    lastUsed = itm.EndTime.Value;
                    idx++;
                }
            }
        }

        #endregion 
        
        #region SwitchLaserOn
        RelayCommand _switchLaserOnCommand = null;
        public ICommand SwitchLaserOn
        {
            get
            {
                if (_switchLaserOnCommand == null)
                {
                    _switchLaserOnCommand = new RelayCommand((p) => OnSwitchLaserOn(p), (p) => CanSwitchLaserOn(p));
                }

                return _switchLaserOnCommand;
            }
        }

        private bool CanSwitchLaserOn(object parameter)
        {
            return true;
        }

        private void OnSwitchLaserOn(object parameter)
        {
            LaserOn = !LaserOn;
            RaisePropertyChanged("LaserOn");
            RaisePropertyChanged("LaserOnButtonColor");
        }

        #endregion 

        #region INotifyPropertyChanged Members

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Compatibility
        public DirectoryInfo Directory { get { return _fi.Directory; } }
        public string DirectoryName { get { return _fi.DirectoryName; } }
        public override bool Exists { get { return _fi.Exists; } }
        public bool IsReadOnly { get { return _fi.IsReadOnly; } set { _fi.IsReadOnly = value; } }
        public long Length { get { return _fi.Length; } }
        public override string Name { get { return _fi.Name; } }

        public StreamWriter AppendText() { return _fi.AppendText(); }
        public FileInfo CopyTo(string destFileName) { return _fi.CopyTo(destFileName); }
        public FileInfo CopyTo(string destFileName, bool overwrite) { return _fi.CopyTo(destFileName, overwrite); }
        public FileStream Create() { return _fi.Create(); }
        public StreamWriter CreateText() { return _fi.CreateText(); }
        public void Decrypt() { _fi.Decrypt(); }
        public override void Delete() { _fi.Delete(); }
        public void Encrypt() { _fi.Encrypt(); }
        public FileSecurity GetAccessControl() { return _fi.GetAccessControl(); }
        public FileSecurity GetAccessControl(AccessControlSections includeSections) { return _fi.GetAccessControl(includeSections); }
        public void MoveTo(string destFileName) { _fi.MoveTo(destFileName); }
        public FileStream Open(FileMode mode) { return _fi.Open(mode); }
        public FileStream Open(FileMode mode, FileAccess access) { return _fi.Open(mode, access); }
        public FileStream Open(FileMode mode, FileAccess access, FileShare share) { return _fi.Open(mode, access, share); }
        public FileStream OpenRead() { return _fi.OpenRead(); }
        public StreamReader OpenText() { return _fi.OpenText(); }
        public FileStream OpenWrite() { return OpenWrite(); }
        public FileInfo Replace(string destinationFileName, string destinationBackupFileName) { return _fi.Replace(destinationFileName, destinationBackupFileName); }
        public FileInfo Replace(string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors) { return _fi.Replace(destinationFileName, destinationBackupFileName, ignoreMetadataErrors); }
        public void SetAccessControl(FileSecurity fileSecurity) { _fi.SetAccessControl(fileSecurity); }
        public override string ToString() { return _fi.ToString(); }

        public new FileAttributes Attributes { get { return _fi.Attributes; } set { _fi.Attributes = value; } }
        public new DateTime CreationTime { get { return _fi.CreationTime; } set { _fi.CreationTime = value; } }
        public new DateTime CreationTimeUtc { get { return _fi.CreationTimeUtc; } set { _fi.CreationTimeUtc = value; } }
        public new string Extension { get { return _fi.Extension; } }
        public override string FullName { get { return _fi.FullName; } }
        public new DateTime LastAccessTime { get { return _fi.LastAccessTime; } set { _fi.LastAccessTime = value; } }
        public new DateTime LastAccessTimeUtc { get { return _fi.LastAccessTimeUtc; } set { _fi.LastAccessTimeUtc = value; } }
        public new DateTime LastWriteTime { get { return _fi.LastWriteTime; } set { _fi.LastWriteTime = value; } }
        public new DateTime LastWriteTimeUtc { get { return _fi.LastWriteTimeUtc; } set { _fi.LastWriteTimeUtc = value; } }
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            _fi.GetObjectData(info, context);
        }
        public new void Refresh()
        {
            _fi.Refresh();
        } 
        #endregion

        #region Type Conversion

        public static implicit operator TimeLineItem(TimeLineItemViewModel item)
        {
            var result = new TimeLineItem();
            result.StartTime = item.StartTime;
            result.EndTime = item.EndTime;
            result.FullName = item.FullName;
            result.Stretching = item.Stretching;
            result.Brightness = item.Brightness;
            result.LaserOn = item.LaserOn;
            return result;
        }

        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public byte ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public char ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public double ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public short ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public float ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public string ToString(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(TimeLineItem))
            {
                return (TimeLineItem)this;
            }
            throw new NotImplementedException();
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Misc

        private static void SetWindowPosition(Window w)
        {
            var location = Application.Current.MainWindow.PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));

            w.Left = location.X - w.Width / 2;
            w.Top = location.Y - w.Height / 2;

            if (w.Left + w.Width > Application.Current.MainWindow.Left + Application.Current.MainWindow.Width)
                w.Left = location.X - w.Width;

            if (w.Top + w.Height > Application.Current.MainWindow.Top + Application.Current.MainWindow.Height)
                w.Top = location.Y - w.Height;
        }

        #endregion
    }
}
