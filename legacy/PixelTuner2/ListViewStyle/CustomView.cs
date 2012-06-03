using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PixelTuner.ListViewStyle
{
    /// <summary>
    /// ImageView displays image files using themselves as their icons.
    /// In order to write our own visual tree of a view, we should override its
    /// DefaultStyleKey and ItemContainerDefaultKey. DefaultStyleKey specifies
    /// the style key of ListView; ItemContainerDefaultKey specifies the style
    /// key of ListViewItem.
    /// </summary>
    public class ImageView : ViewBase
    {
        #region DefaultStyleKey

        protected override object DefaultStyleKey
        {
            get { return new ComponentResourceKey(GetType(), "ImageView"); }
        }

        #endregion

        #region ItemContainerDefaultStyleKey

        protected override object ItemContainerDefaultStyleKey
        {
            get { return new ComponentResourceKey(GetType(), "ListViewItem"); }
        }

        #endregion
    }

    /// <summary>
    /// FileCollection is used as ItemsSource of ListView. ListView will show files inside
    /// the collection
    /// </summary>
    public sealed class FileCollection : ObservableCollection<ThumbnailFileItem>
    {
        public FileCollection()
        {
            // put files in %windir%\web\wallpaper into the collection

            // get %windir%
            //string windir = Environment.GetEnvironmentVariable("WINDIR");
            //if (string.IsNullOrEmpty(windir))
            //    return;

            // put files into collection

            //string wallpaperPath = string.Format(@"{0}\Web\Wallpaper", windir);
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }
            DirectoryInfo info = new DirectoryInfo(sourcePath);
            FileSystemInfo[] files = info.GetFileSystemInfos();

            foreach (FileInfo fi in files)
            {
                // exlude hidden files 
                ThumbnailFileItem tfi = new ThumbnailFileItem(fi);
                if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    Add(tfi);
            }
        }
    }
}