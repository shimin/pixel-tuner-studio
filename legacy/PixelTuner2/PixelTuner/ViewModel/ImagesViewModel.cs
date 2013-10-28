using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PixelTuner.Controls.ThumbListView;
using System.Collections.ObjectModel;

namespace PixelTuner.ViewModel
{
    public class ImagesViewModel : ToolViewModel
    {
        public ImagesViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
            RefreshImagesCollection(PixelTuner.Properties.Settings.Default.LineHeight);
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

        public bool IsSomethingSelected
        {
            get
            {
                return _selectedItem != null;
            }
        }


        public void ApplyFilter(string text)
        {
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }
            DirectoryInfo info = new DirectoryInfo(sourcePath);
            List<FileSystemInfo> allfiles = new List<FileSystemInfo>(info.GetFileSystemInfos("*.png"));
            allfiles.AddRange(info.GetFileSystemInfos("*.jpg"));
            allfiles.AddRange(info.GetFileSystemInfos("*.bmp"));
            List<FileSystemInfo> suitableFiles = new List<FileSystemInfo>();
            foreach (var f in allfiles)
            {
                if (string.IsNullOrEmpty(text) || Path.GetFileNameWithoutExtension(f.Name).Contains(text))
                {
                    using (System.Drawing.Image img = System.Drawing.Image.FromFile(f.FullName))
                    {
                        if (img.Height == PixelTuner.Properties.Settings.Default.LineHeight)
                            suitableFiles.Add(f);
                    }
                }
            }
            allfiles = suitableFiles;
            _images.Clear();

            foreach (FileInfo fi in allfiles)
            {
                // exlude hidden files 
                TimeLineItemViewModel tfi = new TimeLineItemViewModel(fi);
                if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    _images.Add(tfi);
            }
        }

        public void RefreshImagesCollection(int height = 0, string itemToRemove = "")
        {
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }
            DirectoryInfo info = new DirectoryInfo(sourcePath);
            List<FileSystemInfo> allfiles = new List<FileSystemInfo>(info.GetFileSystemInfos("*.png"));
            allfiles.AddRange(info.GetFileSystemInfos("*.jpg"));
            allfiles.AddRange(info.GetFileSystemInfos("*.bmp"));
            if (height > 0)
            {
                List<FileSystemInfo> suitableFiles = new List<FileSystemInfo>();
                foreach (var f in allfiles)
                {
                    using (System.Drawing.Image img = System.Drawing.Image.FromFile(f.FullName))
                    {
                        if (img.Height == height && f.FullName != itemToRemove)
                            suitableFiles.Add(f);
                    }
                }
                allfiles = suitableFiles;
            }
            _images.Clear();
            if (!string.IsNullOrEmpty(itemToRemove))
            {
                File.Delete(itemToRemove);
            }
            
            foreach (FileInfo fi in allfiles)
            {
                // exlude hidden files 
                TimeLineItemViewModel tfi = new TimeLineItemViewModel(fi);
                if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    _images.Add(tfi);
            }
        }

    }
}
