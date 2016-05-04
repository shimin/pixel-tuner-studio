using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Threading;

namespace PixelTuner.ViewModel
{
    public class VideosViewModel : ToolViewModel
    {
        static string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        string _sourcePath = Path.Combine(picturesPath, "PixelTuner 2\\Clips");

        public VideosViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
            RefreshVideosCollection();
        }

        private string _searchPattern;
        public string SearchPattern
        {
            get { return _searchPattern; }
            set { _searchPattern = value; RaisePropertyChanged("SearchPattern"); }
        }

        public const string ToolContentId = "VideosTool";

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

        /// <summary>
        /// Indicates wether something selected or not to enable/disable edit buttons 
        /// </summary>
        public bool IsSomethingSelected
        {
            get
            {
                return _selectedItem != null;
            }
        }

        Thread t = null;
        public void RefreshVideosCollection(int height = 0, string itemToRemove = "")
        {
            if (t != null && t.IsAlive)
            {
                t.Abort();
                t.Join();
            }
            t = new Thread((parameter) =>
            {
                try
                {
                    var arg = parameter as RefreshParameters;
                    if (arg != null)
                    {
                        AsyncRefreshCollection(arg.Height, arg.ItemToRemove);
                    }
                    else AsyncRefreshCollection();
                }
                catch { }
            });
            t.Start(new RefreshParameters(height, itemToRemove));
        }

        private void AsyncRefreshCollection(int height = 0, string itemToRemove = "")
        {
            if (!Directory.Exists(_sourcePath))
                Directory.CreateDirectory(_sourcePath);

            DirectoryInfo info = new DirectoryInfo(_sourcePath);
            List<FileSystemInfo> allfiles = new List<FileSystemInfo>(info.GetFileSystemInfos("*.avi"));
            allfiles.AddRange(info.GetFileSystemInfos("*.wmv"));
            allfiles.AddRange(info.GetFileSystemInfos("*.mp4"));
            allfiles.AddRange(info.GetFileSystemInfos("*.mpg"));

            if (Application.Current == null) return;
            Application.Current.Dispatcher.Invoke(new Action(() => { _videos.Clear(); }));

            foreach (FileInfo fi in allfiles)
            {
                if (string.IsNullOrEmpty(SearchPattern)
                        || Path.GetFileNameWithoutExtension(fi.Name.ToLower()).Contains(SearchPattern.ToLower()))
                {
                    var tfi = new VideoThumbnailViewModel(fi);
                    if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    {
                        if (Application.Current == null) return;
                        Application.Current.Dispatcher.Invoke(new Action(() => { _videos.Add(tfi); }));
                    }
                }
            }
        }
    }
}
