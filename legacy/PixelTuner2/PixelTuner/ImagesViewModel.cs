using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PixelTuner.ListViewStyle;
using System.Collections.ObjectModel;

namespace PixelTuner
{
    public class ImagesViewModel : ToolViewModel
    {

        public ImagesViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
            RefreshImagesCollection();
        }

        public const string ToolContentId = "ImagesTool";

        private void MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        ObservableCollection<ThumbnailFileItem> _images = new ObservableCollection<ThumbnailFileItem>();
        ReadOnlyObservableCollection<ThumbnailFileItem> _readonyImagesCollection = null;
        public ReadOnlyObservableCollection<ThumbnailFileItem> ImagesCollection
        {
            get
            {
                if (_readonyImagesCollection == null)
                    _readonyImagesCollection = new ReadOnlyObservableCollection<ThumbnailFileItem>(_images);

                return _readonyImagesCollection;
            }
        }

        ThumbnailFileItem _selectedItem = null;
        public ThumbnailFileItem SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                _selectedItem = value;
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
                    System.Drawing.Image img = System.Drawing.Image.FromFile(f.FullName);
                    if (img.Height == height)
                        suitableFiles.Add(f);
                }
                allfiles = suitableFiles;
            }
            _images.Clear();

            FileSystemInfo ftr = null;
            foreach (var f in allfiles)
                if (f.FullName == itemToRemove)
                {
                    ftr = f; break;
                }

            if (ftr != null)
            {
                allfiles.Remove(ftr);
                ftr.Delete();
            }

            foreach (FileInfo fi in allfiles)
            {
                // exlude hidden files 
                ThumbnailFileItem tfi = new ThumbnailFileItem(fi);
                if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    _images.Add(tfi);
            }
        }

    }
}
