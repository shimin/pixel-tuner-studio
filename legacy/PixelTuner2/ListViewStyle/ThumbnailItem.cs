using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using TimeLineTool;
using System.IO;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.ComponentModel;

namespace PixelTuner.ListViewStyle
{
    public class ThumbnailFileItem : FileSystemInfo, ITimeLineDataItem, INotifyPropertyChanged
    {
        FileInfo _fi = null;
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
            }
        }

        DateTime? _endTime = DateTime.MinValue.AddDays(2);
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

        public FileAttributes Attributes { get { return _fi.Attributes; } set { _fi.Attributes = value; } }
        public DateTime CreationTime { get { return _fi.CreationTime; } set { _fi.CreationTime = value; } }
        public DateTime CreationTimeUtc { get { return _fi.CreationTimeUtc; } set { _fi.CreationTimeUtc = value; } }
        public string Extension { get { return _fi.Extension; } }
        public virtual string FullName { get { return _fi.FullName; } }
        public DateTime LastAccessTime { get { return _fi.LastAccessTime; } set { _fi.LastAccessTime = value; } }
        public DateTime LastAccessTimeUtc { get { return _fi.LastAccessTimeUtc; } set { _fi.LastAccessTimeUtc = value; } }
        public DateTime LastWriteTime { get { return _fi.LastWriteTime; } set { _fi.LastWriteTime = value; } }
        public DateTime LastWriteTimeUtc { get { return _fi.LastWriteTimeUtc; } set { _fi.LastWriteTimeUtc = value; } }
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            _fi.GetObjectData(info, context);
        }
        public void Refresh()
        {
            _fi.Refresh();
        }

        #region INotifyPropertyChanged Members

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
