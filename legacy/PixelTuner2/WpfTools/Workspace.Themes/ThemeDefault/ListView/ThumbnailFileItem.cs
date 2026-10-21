using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using TimeLineTool;
using System.IO;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Xml.Serialization;
using PixelTuner.Serialization;
using Workspace.Commands;

namespace Workspace.Themes.Default.ListView
{
    public class ThumbnailFileItem: FileSystemInfo, ITimeLineDataItemConvertable<TimeLineItem>, INotifyPropertyChanged
    {
        FileInfo _fi = null;
        public ThumbnailFileItem()
        {
        }

        public ThumbnailFileItem(FileInfo fi)
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
                    _startTime.Value.Millisecond.ToString("D4")) 
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
                    _endTime.Value.Millisecond.ToString("D4")) 
                    : "";
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

        object ICloneable.Clone()
        {
            ThumbnailFileItem result = new ThumbnailFileItem(this._fi);
            result.StartTime = this.StartTime;
            result.EndTime = this.EndTime;
            result.TimelineViewExpanded = this.TimelineViewExpanded;
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

        public static implicit operator TimeLineItem(ThumbnailFileItem item)
        {
            var result = new TimeLineItem();
            result.StartTime = item.StartTime;
            result.EndTime = item.EndTime;
            result.FullName = item.FullName;
            return result;
        }

        #region ITimeLineDataItemConvertable<TimeLineItem> Members

        TimeLineItem ITimeLineDataItemConvertable<TimeLineItem>.ConvertTo(ITimeLineDataItem item)
        {
            throw new NotImplementedException();
        }

        ITimeLineDataItem ITimeLineDataItemConvertable<TimeLineItem>.ConvertFrom(TimeLineItem item)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
