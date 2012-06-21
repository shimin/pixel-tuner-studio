using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PixelTuner
{
    class FileStatsViewModel : ToolViewModel
    {
        public FileStatsViewModel()
            :base("File Stats")
        {
            RootViewModel.This.ActiveDocumentChanged += new PixelTuner.RootViewModel.DocumentChangedEventHandler(OnActiveDocumentChanged);
            ContentId = ToolContentId;
        }

        public const string ToolContentId = "FileStatsTool";

        void OnActiveDocumentChanged(object sender, PixelTuner.RootViewModel.DocumentChangedEventArgs e)
        {
            if (RootViewModel.This.ActiveDocument != null &&
                RootViewModel.This.ActiveDocument.FilePath != null &&
                File.Exists(RootViewModel.This.ActiveDocument.FilePath))
            {
                var fi = new FileInfo(RootViewModel.This.ActiveDocument.FilePath);
                FileSize = fi.Length;
                LastModified = fi.LastWriteTime;
            }
            else
            {
                FileSize = 0;
                LastModified = DateTime.MinValue;
            }
        }

        #region FileSize

        private long _fileSize;
        public long FileSize
        {
            get { return _fileSize; }
            set
            {
                if (_fileSize != value)
                {
                    _fileSize = value;
                    RaisePropertyChanged("FileSize");
                }
            }
        }

        #endregion

        #region LastModified

        private DateTime _lastModified;
        public DateTime LastModified
        {
            get { return _lastModified; }
            set
            {
                if (_lastModified != value)
                {
                    _lastModified = value;
                    RaisePropertyChanged("LastModified");
                }
            }
        }

        #endregion



        
    }
}
