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
        protected FileInfo _fi = null;

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

        private bool _useFullName = true;
        [XmlIgnore]
        public bool UseFullName
        {
            get
            {
                return _useFullName;
            }

            set
            {
                _useFullName = value;
            }
        }
        
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

        protected BitmapImage _image = null;
        public virtual BitmapSource ImageSource
        {
            get
            {
                if (_image == null)
                {
                    if (string.IsNullOrEmpty(FullName)) _image = new BitmapImage();
                    else _image = ImageHelper.CreateImage(new Uri(FullName, UriKind.Absolute));
                }
                return _image;
                //if (_image == null) return DependencyProperty.UnsetValue;
                //return _image;
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

        [XmlIgnore]
        public virtual double ColorLayerOpacity
        {
            get { return 0.6; }
        }

        [XmlIgnore]
        public virtual bool UseColorLayer
        {
            get { return false; }
        }

        [XmlIgnore]
        public bool CanSwitchLaserOnProperty
        {
            get { return CanSwitchLaserOn(null); }
        }

        [XmlIgnore]
        public bool CanThumbnailProperty
        {
            get { return CanThumbnail(null); }
        }

        [XmlIgnore]
        public bool CanRepeatProperty
        {
            get { return CanRepeat(null); }
        }

        [XmlIgnore]
        public bool CanReplaceProperty
        {
            get { return CanReplace(null); }
        }

        [XmlIgnore]
        public bool CanSplitProperty
        {
            get { return CanSplit(null); }
        }

        public Brush LaserOnButtonColor
        {
            get
            {
                if (LaserOn) return new SolidColorBrush(Color.FromRgb(200, 140, 100));
                else return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            }
        }

        private int _actualHeight = 0;
        [XmlAttribute]
        public int ActualHeight
        {
            get
            {
                try
                {
                    _actualHeight = ImageSource?.PixelHeight ?? 0;
                }
                catch { }
                return _actualHeight;
            }
            set { _actualHeight = value; }
        }
        
        public virtual string ContentID
        {
            get
            {
                return "GenericElement";
            }
        }

        #endregion

        public virtual void Replace(string path)
        {
            if (File.Exists(path))
            {
                _fi = new FileInfo(path);
            }
        }

        public virtual void ReplaceWithBlank(int projectHeight = 0)
        {
            var b = new System.Drawing.Bitmap(10, ActualHeight != 0 ? ActualHeight : projectHeight);
            var p = Path.GetTempFileName().Replace(".tmp", ".bmp");
            b.Save(p, System.Drawing.Imaging.ImageFormat.Bmp);
            _fi = new FileInfo(p);
        }

        public virtual object Clone(FileInfo fi = null)
        {
            TimeLineItemViewModel result = (TimeLineItemViewModel)Activator.CreateInstance(GetType());
            result._fi = fi ?? this._fi;
            result.StartTime = this.StartTime;
            result.EndTime = this.EndTime;
            result.TimelineViewExpanded = this.TimelineViewExpanded;
            result.NormalBackground = NormalBackground;
            result.Stretching = this.Stretching;
            result.Brightness = this.Brightness;
            result.LaserOn = this.LaserOn;
            return result;
        }

        #region ThumbnailCommand
        RelayCommand _thumbnailCommand = null;
        public ICommand ThumbnailCommand
        {
            get
            {
                if (_thumbnailCommand == null)
                {
                    _thumbnailCommand = new RelayCommand((p) => OnThumbnail(p), (p) => CanThumbnail(p));
                }

                return _thumbnailCommand;
            }
        }

        public virtual bool CanThumbnail(object parameter)
        {
            return false;
        }

        protected virtual void OnThumbnail(object parameter)
        {

        }

        #endregion

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

        public virtual bool CanReplace(object parameter)
        {
            return true;
        }

        protected virtual void OnReplace(object parameter)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            ReplaceItemWindow w = OpenReplaceWindow();
            try
            {
                w.Height = 0;
                w.Height = (int) parameter;
            }
            catch {}

            if (w.Height == 0) w.Height = p.TimelineHeightPx;

            w.Owner = Application.Current.MainWindow;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (w.ShowDialog() == true)
            {
                TimeLineItemViewModel newItem = w.SelectedItem as TimeLineItemViewModel;
                if (newItem != null)
                {
                    if (p != null)
                    {
                        RootViewModel.This.ActiveDocument.PushUndo(false);
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

        public virtual ReplaceItemWindow OpenReplaceWindow()
        {
            return new ReplaceItemWindow();
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

        public virtual bool CanSplit(object parameter)
        {
            return (this.EndTime.Value - this.StartTime.Value).TotalMilliseconds >= 50;
        }

        private void OnSplit(object parameter)
        {
            SplitItemWindow w = new SplitItemWindow(this);
            SetWindowPosition(w);

            if (w.ShowDialog() == true)
            {
                if (w.Duration < (this.EndTime - this.StartTime) && w.Duration.TotalMilliseconds > 20)
                    Split(w.Duration);
            }
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }

        private void Split(TimeSpan timeSpan)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null)
            {
                RootViewModel.This.ActiveDocument.PushUndo(false);
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

        public virtual bool CanRepeat(object parameter)
        {
            return false;
        }

        private void OnRepeat(object parameter)
        {
            RepeatItemWindow w = new RepeatItemWindow(this);
            SetWindowPosition(w);

            if (w.ShowDialog() == true)
            {
                var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
                if (p != null) { RootViewModel.This.ActiveDocument.PushUndo(false); }

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

        protected virtual bool CanDisposeMe(object parameter)
        {
            return true;
        }

        protected virtual void OnDisposeMe(object parameter)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null)
            {
                RootViewModel.This.ActiveDocument.PushUndo(false);
                p.IsChanged = true;
                p.Items.Remove(this);
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

        protected virtual bool CanEditMe(object parameter)
        {
            return true;
        }

        protected virtual void OnEditMe(object parameter)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null) { RootViewModel.This.ActiveDocument.PushUndo(false, p); }

            if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftAlt)
                || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt))
            {
                ProprietaryEdit(parameter);
            }
            else
            {
                GenericEdit(parameter);
            }
        }

        protected virtual void GenericEdit(object parameter)
        {
            BasicItemWindow w = new BasicItemWindow(this);
            SetWindowPosition(w);

            if (w.ShowDialog() == true)
            {
                ApplyChanges(w.Start, w.Duration, w.Brightness, w.ForAll, w.DurationChanged, w.BrightnessChanged);
            }
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }

        private void ApplyChanges(DateTime start, TimeSpan duration, byte brightness, bool forAll, bool durationChanged, bool brightnessChanged)
        {
            var p = TimelineHelper.FindParentTimeline(RootViewModel.This.ActiveDocument, this);
            if (p != null)
            {
                int idx = p.Items.IndexOf(this) + 1;

                this.StartTime = start > p.Start.Value ? start : p.Start.Value;
                this.EndTime = this.StartTime + duration;
                this.Brightness = brightness;

                List<ITimeLineDataItem> toRemove = new List<ITimeLineDataItem>();
                for (int i = idx - 2; i >= 0; i--)
                {
                    if (p.Items[i].StartTime.Value >= this.StartTime.Value) toRemove.Add(p.Items[i]);
                    else if (p.Items[i].EndTime.Value > this.StartTime.Value) p.Items[i].EndTime = this.StartTime;
                }

                for (int i = idx; i < p.Items.Count && !forAll; i++)
                {
                    if (p.Items[i].EndTime.Value <= this.EndTime.Value) toRemove.Add(p.Items[i]);
                    else if (p.Items[i].StartTime.Value < this.EndTime.Value) { p.Items[i].StartTime = this.EndTime; p.Items[i].EndTime = p.Items[i].EndTime; }
                }
                toRemove.ForEach((killedItem) => { p.Items.Remove(killedItem); });

                DateTime lastUsed = this.EndTime.Value;
                while (forAll && idx < p.Items.Count)
                {
                    var itm = (p.Items[idx] as TimeLineItemViewModel);
                    if (durationChanged)
                    {
                        if (p.Items[idx].StartTime.Value < lastUsed) p.Items[idx].StartTime = lastUsed;
                        p.Items[idx].EndTime = p.Items[idx].StartTime.Value.Add(duration);
                    }
                    if (brightnessChanged) itm.Brightness = brightness;
                    lastUsed = itm.EndTime.Value;
                    idx++;
                }
            }
        }

        protected virtual void ProprietaryEdit(object parameter)
        {

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

        public virtual bool CanSwitchLaserOn(object parameter)
        {
            return false;
        }

        protected virtual void OnSwitchLaserOn(object parameter)
        {
            
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
        private string _fullName = null;
        public override string FullName { get { return _fi?.FullName ?? ""; } }
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
            result.FullName = item.UseFullName ? item.FullName : Path.Combine("./", item.Name);
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

        public virtual object ToType(Type conversionType, IFormatProvider provider)
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
