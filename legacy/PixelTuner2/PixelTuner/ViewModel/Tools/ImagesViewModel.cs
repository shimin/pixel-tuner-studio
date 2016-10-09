using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PixelTuner.Controls.ThumbListView;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Threading;

namespace PixelTuner.ViewModel
{
    public class ImagesViewModel : ToolViewModel
    {
        static string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        string _sourcePath = Path.Combine(picturesPath, "PixelTuner 2\\Content");

        public ImagesViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
            RefreshImagesCollection(PixelTuner.Properties.Settings.Default.LineHeight);
        }

        private string _searchPattern;
        public string SearchPattern
        {
            get { return _searchPattern; }
            set { _searchPattern = value; RaisePropertyChanged("SearchPattern"); }
        }

        private int _lineHeight = Properties.Settings.Default.LineHeight;
        public int LineHeight
        {
            get { return _lineHeight; }
            set { _lineHeight = value; RaisePropertyChanged("LineHeight"); RefreshImagesCollection(value); }
        }

        public const string ToolContentId = "ImagesTool";

        private void MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        ObservableCollection<TimeLineItemViewModel> _images = new ObservableCollection<TimeLineItemViewModel>();
        ReadOnlyObservableCollection<TimeLineItemViewModel> _readonyImagesCollection = null;
        public ReadOnlyObservableCollection<TimeLineItemViewModel> ImagesCollection
        {
            get
            {
                if (_readonyImagesCollection == null)
                    _readonyImagesCollection = new ReadOnlyObservableCollection<TimeLineItemViewModel>(_images);

                return _readonyImagesCollection;
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
        public void RefreshImagesCollection(int height = 0, string itemToRemove = "")
        {
            if (t != null && t.IsAlive)
            {
                t.Abort();
                t.Join();
            }
            t = new Thread((parameter) => {
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
            {
                Directory.CreateDirectory(_sourcePath);
            }
            DirectoryInfo info = new DirectoryInfo(_sourcePath);
            List<FileSystemInfo> allfiles = new List<FileSystemInfo>(info.GetFileSystemInfos("*.png"));
            allfiles.AddRange(info.GetFileSystemInfos("*.jpg"));
            allfiles.AddRange(info.GetFileSystemInfos("*.bmp"));
            if (height > 0)
            {
                List<FileSystemInfo> suitableFiles = new List<FileSystemInfo>();
                foreach (var f in allfiles)
                {
                    if (string.IsNullOrEmpty(SearchPattern)
                        || Path.GetFileNameWithoutExtension(f.Name.ToLower()).Contains(SearchPattern.ToLower()))
                        using (System.Drawing.Image img = System.Drawing.Image.FromFile(f.FullName))
                        {
                            if (img.Height == height && f.FullName != itemToRemove)
                                suitableFiles.Add(f);
                        }
                }
                allfiles = suitableFiles;
            }

            if (Application.Current == null) return;
            Application.Current.Dispatcher.Invoke(new Action(() => { _images.Clear(); }));

            if (!string.IsNullOrEmpty(itemToRemove))
                File.Delete(itemToRemove);

            foreach (FileInfo fi in allfiles)
            {
                var tfi = new PixelThumbnailViewModel(fi);
                if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    if (string.IsNullOrEmpty(SearchPattern) 
                        || Path.GetFileNameWithoutExtension(fi.Name.ToLower()).Contains(SearchPattern.ToLower()))
                    {
                        if (Application.Current == null) return;
                        Application.Current.Dispatcher.Invoke(new Action(() => { _images.Add(tfi); }));
                    }
            }
        }
    }
}
