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

using Core;
using PixelTuner.Properties;
using PixelTuner.Controls;
using PixelTuner.ViewModel.Serialization;

namespace PixelTuner.ViewModel
{
    public class TimeLineItemViewModel : FileSystemInfo, ITimeLineDataItem, INotifyPropertyChanged, IConvertible
    {
        FileInfo _fi = null;

        public TimeLineItemViewModel()
        {
        }

        public TimeLineItemViewModel(FileInfo fi)
        {
            _fi = fi;
        }

        DateTime? _startTime = DateTime.MinValue;
        public DateTime? StartTime
        {
            get { return _startTime; }
            set
            {
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
                _endTime = value;
                RaisePropertyChanged("EndTime");
                RaisePropertyChanged("End");
            }
        }

        public string End
        {
            get
            {
                return _endTime.HasValue ? String.Format("{0}:{1}:{2}.{3}", _endTime.Value.Hour.ToString("D2"),
                    _endTime.Value.Minute.ToString("D2"), _endTime.Value.Second.ToString("D2"),
                    _endTime.Value.Millisecond.ToString("D3")) 
                    : "";
            }
        }

        public object ImageSource
        {
            get
            {
                BitmapImage image = new BitmapImage();

                try
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    image.UriSource = new Uri(FullName, UriKind.Absolute);
                    image.EndInit();
                }
                catch
                {
                    return DependencyProperty.UnsetValue;
                }

                return image;
            }
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
                _stretching = value;
                RaisePropertyChanged("Stretching");
            }
        }


        System.Windows.Media.Brush _backGround = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255,255,255));
        [XmlIgnore]
        public System.Windows.Media.Brush NormalBackground
        {
            get { return _backGround; }
            set { _backGround = value; RaisePropertyChanged("NormalBackground"); }
        }

        object ICloneable.Clone()
        {
            TimeLineItemViewModel result = new TimeLineItemViewModel(this._fi);
            result.StartTime = this.StartTime;
            result.EndTime = this.EndTime;
            result.TimelineViewExpanded = this.TimelineViewExpanded;
            result.Stretching = this.Stretching;
            result.NormalBackground = this.NormalBackground;
            return result;
        }

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
            if (_parent != null)
            {
                System.Reflection.PropertyInfo pi = _parent.GetType().GetProperty("Items");
                if (pi != null) parentCollection = 
                    (ObservableCollection<ITimeLineDataItem>)pi.GetValue(_parent, null);
            }
            if (parentCollection != null)
            {
                parentCollection.Remove(this);
            }
        }

        INotifyPropertyChanged _parent;
        [XmlIgnore]
        public INotifyPropertyChanged Parent
        {
            get { return _parent; }
            set { _parent = value; }
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

        public static implicit operator TimeLineItem(TimeLineItemViewModel item)
        {
            var result = new TimeLineItem();
            result.StartTime = item.StartTime;
            result.EndTime = item.EndTime;
            result.FullName = item.FullName;
            result.Stretching = item.Stretching;
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
    }
}
