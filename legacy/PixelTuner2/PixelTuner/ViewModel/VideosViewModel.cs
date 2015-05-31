using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.IO;

namespace PixelTuner.ViewModel
{
    public class VideosViewModel : ToolViewModel
    {
        public VideosViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
        }

        public const string ToolContentId = "VideosTool";
        private string _lastFilter = "";

        private void MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        ObservableCollection<TimeLineItemViewModel> _videos = new ObservableCollection<TimeLineItemViewModel>();
        ReadOnlyObservableCollection<TimeLineItemViewModel> _readonyVideosCollection = null;
        public ReadOnlyObservableCollection<TimeLineItemViewModel> VideosCollection
        {
            get
            {
                if (_readonyVideosCollection == null)
                    _readonyVideosCollection = new ReadOnlyObservableCollection<TimeLineItemViewModel>(_videos);

                return _readonyVideosCollection;
            }
        }

        TimeLineItemViewModel _selectedItem = null;
        public TimeLineItemViewModel SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                _selectedItem = value;
                RaisePropertyChanged("IsSomethingSelected");
            }
        }

        public bool IsSomethingSelected
        {
            get
            {
                return _selectedItem != null;
            }
        }

        public void ApplyFilter(string text)
        {
            _lastFilter = text;
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }
            DirectoryInfo info = new DirectoryInfo(sourcePath);
            List<FileSystemInfo> allfiles = new List<FileSystemInfo>(info.GetFileSystemInfos("*.avi"));
            allfiles.AddRange(info.GetFileSystemInfos("*.wmv"));
            allfiles.AddRange(info.GetFileSystemInfos("*.mp4"));
            allfiles.AddRange(info.GetFileSystemInfos("*.mpg"));
            List<FileSystemInfo> suitableFiles = new List<FileSystemInfo>();
            foreach (var f in allfiles)
            {
                if (string.IsNullOrEmpty(text) || Path.GetFileNameWithoutExtension(f.Name.ToLower()).Contains(text.ToLower()))
                {
                    using (System.Drawing.Image img = System.Drawing.Image.FromFile(f.FullName))
                    {
                        if (img.Height == PixelTuner.Properties.Settings.Default.LineHeight)
                            suitableFiles.Add(f);
                    }
                }
            }
            allfiles = suitableFiles;
            _videos.Clear();

            foreach (FileInfo fi in allfiles)
            {
                // exlude hidden files 
                TimeLineItemViewModel tfi = new TimeLineItemViewModel(fi);
                if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    _videos.Add(tfi);
            }
        }

        public void RefreshVideosCollection(int height = 0, string itemToRemove = "")
        {
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }
            DirectoryInfo info = new DirectoryInfo(sourcePath);
            List<FileSystemInfo> allfiles = new List<FileSystemInfo>(info.GetFileSystemInfos("*.avi"));
            allfiles.AddRange(info.GetFileSystemInfos("*.wmv"));
            allfiles.AddRange(info.GetFileSystemInfos("*.mp4"));
            allfiles.AddRange(info.GetFileSystemInfos("*.mpg"));

            foreach (FileInfo fi in allfiles)
            {
                // exclude hidden files 
                TimeLineItemViewModel tfi = new TimeLineItemViewModel(fi);
                if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    _videos.Add(tfi);
            }
            ApplyFilter(_lastFilter);
        }


    }
}
