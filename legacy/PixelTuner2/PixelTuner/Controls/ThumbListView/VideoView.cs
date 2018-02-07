using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PixelTuner.ViewModel;
using System.Text;
using PixelTuner.Properties;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using Common.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace PixelTuner.Controls.ThumbListView
{
    public partial class ThumbnailVideoListView : ResourceDictionary
    {
        static string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        string _sourcePath = Path.Combine(picturesPath, "PixelTuner 2\\Clips");

        private void boxThumbnails_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                BackgroundWorker bgw = new BackgroundWorker();
                bgw.DoWork += new DoWorkEventHandler(DropFiles);
                bgw.RunWorkerAsync(new object[] { sender, droppedFilePaths });
            }
        }

        private void DropFiles(object sender, DoWorkEventArgs ea)
        {
            object[] args = (object[])ea.Argument;

            ListView videosView = (ListView)args[0];
            string[] fileNames = (string[])args[1];

            VideosViewModel videosModel = null;
            videosView.Dispatcher.Invoke(new Action(() => videosModel = videosView.DataContext as VideosViewModel ));
            
            StringBuilder sb = new StringBuilder();
            
            foreach (var p in fileNames)
            {
                try
                {
                    File.Copy(p, Path.Combine(_sourcePath, Path.GetFileName(p)));
                }
                catch
                {
                    sb.AppendLine(String.Format("Error copying image. File: {0}", p));
                }
            }
            
            if (sb.Length > 0) MessageBox.Show(sb.ToString());
            videosModel.RefreshVideosCollection();
            videosView.Dispatcher.Invoke(new Action(() => videosView.Focus()));
        }
        
        private void boxThumbnails_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                var control = sender as ListView;
                if (control != null)
                {
                    var itemToRemove = control.SelectedItem as TimeLineItemViewModel;
                    string msg = String.Format((String)Application.Current.TryFindResource("Global_ReallyRemove"), itemToRemove.FullName);
                    var answer = MessageBox.Show(msg, Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (itemToRemove != null && answer == MessageBoxResult.Yes)
                    {
                        RootViewModel.This.DeleteItem(itemToRemove);
                    }
                }
            }
        }

        private void SearchHandler(object sender, RoutedEventArgs e)
        {
            var s = sender as TextBox;
            RootViewModel.This.VideosModel.RefreshVideosCollection();
        }

        private void ListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DependencyObject obj = (DependencyObject)e.OriginalSource;

            while (obj != null && obj != sender)
            {
                var item = obj as ListViewItem;
                if (item != null)
                {
                    var fm = RootViewModel.This.ActiveDocument;
                    if (fm != null && fm.SelectedTimeline != null)
                    {
                        var src = item.DataContext as TimeLineItemViewModel;
                        if (src != null)
                        {
                            var newItem = (TimeLineItemViewModel)src.Clone();
                            int insertPosition = fm.SelectedTimeline.Items.Count;
                            foreach (TimeLineItemViewModel i in fm.SelectedTimeline.Items) {
                                if (i.IsSelected)
                                {
                                    insertPosition = fm.SelectedTimeline.Items.IndexOf(i); break;
                                }
                            }
                            var itemCollection = new ObservableCollection<TimeLineItemViewModel>(
                                new System.Collections.Generic.List<TimeLineItemViewModel> { newItem });
                            newItem.EndTime = newItem.StartTime.Value.AddSeconds(Settings.Default.DefaultDuration);

                            if (fm.SelectedTimeline.DragManager != null)
                                if (fm.SelectedTimeline.DragManager.CanAddToTimeLine(newItem, newItem.StartTime.Value))
                                {
                                    fm.SelectedTimeline.InsertItems(itemCollection, insertPosition);
                                }
                        }
                    }
                    break;
                }
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            }
        }

        private void ListView_SelectionChanged(object sender, System.EventArgs e)
        {
            //var item = (sender as ListView).SelectedItem as TimeLineItemViewModel;
            //if (item != null)
            //{
            //    RootViewModel.This.ProjectPlayerModel.DisplayModel = new TimeLineItemViewModel(new FileInfo(item.FullName));
            //}
        }

        #region Picture editor

        private void Delete_Button_Click(object sender, RoutedEventArgs e)
        {
            var itemToRemove = RootViewModel.This.VideosModel.SelectedItem;
            if (itemToRemove != null)
            {
                string msg = String.Format((String)Application.Current.TryFindResource("Global_ReallyRemove"), itemToRemove.FullName);
                var answer = MessageBox.Show(msg, Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Yes)
                {
                    RootViewModel.This.DeleteItem(itemToRemove);
                }
            }
        }

        private void Resfresh_Button_Click(object sender, RoutedEventArgs e)
        {
            RootViewModel.This.VideosModel.RefreshVideosCollection();
        }

        #region Bmp import

        private byte[] GifAnimation = { 33, 255, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 };
        private byte[] Delay = { 0, 0 };
        public void WriteGifImg(byte[] B, BinaryWriter BW)
        {
            B[785] = Delay[0]; //5 secs delay
            B[786] = Delay[1];
            B[798] = (byte)(B[798] | 0X87);
            BW.Write(B, 781, 18);
            BW.Write(B, 13, 768);
            BW.Write(B, 799, B.Length - 800);
        }

        private void BmpImport_Button_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = Environment.SpecialFolder.MyPictures.ToString();
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.InitialDirectory = Path.Combine(Environment.SpecialFolder.MyVideos.ToString(), "PixelTuner 2\\Clips");
                        sfd.Filter = "Gif files (*.gif)|*.gif";
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            string GifFile = sfd.FileName;

                            string[] Files = Directory.GetFiles(fbd.SelectedPath, "*.bmp");
                            MemoryStream MS = new MemoryStream();
                            BinaryReader BR = new BinaryReader(MS);
                            BinaryWriter BW = new BinaryWriter(new FileStream(GifFile, FileMode.Create));
                            System.Drawing.Image.FromFile(Files[0]).Save(MS, System.Drawing.Imaging.ImageFormat.Gif);
                            byte[] B = MS.ToArray();
                            B[10] = (byte)(B[10] & 0X78); //No global color table.
                            BW.Write(B, 0, 13);
                            BW.Write(GifAnimation);
                            WriteGifImg(B, BW);
                            for (int I = 1; I < Files.Length; I++)
                            {
                                MS.SetLength(0);
                                System.Drawing.Image.FromFile(Files[I]).Save(MS, System.Drawing.Imaging.ImageFormat.Gif);
                                B = MS.ToArray();
                                WriteGifImg(B, BW);
                            }
                            BW.Write(B[B.Length - 1]);
                            BW.Close();
                            MS.Dispose();
                        }
                    }
                }
            }
            RootViewModel.This.VideosModel.RefreshVideosCollection();
        }

        private void VideoImport_Button_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "video files (*.avi)|*.avi;*.mp4;*.mov;*.mpg;*.mpeg";
            if (ofd.ShowDialog() == true)
            {
                // SourcePath = ofd.FileName;
                var importWindow = new VideoImportWindow();
                importWindow.SourcePath = ofd.FileName;
                importWindow.UpdateInfo();
                importWindow.Owner = Application.Current.MainWindow;
                if (importWindow.ShowDialog() == true)
                {
                    importWindow.SaveGif(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), 
                        "PixelTuner 2\\Clips\\" + Path.GetFileNameWithoutExtension(ofd.FileName) + ".gif"));
                }
            }
        }

        #endregion

        #endregion
    }
    /// <summary>
    /// ImageView displays image files using themselves as their icons.
    /// In order to write our own visual tree of a view, we should override its
    /// DefaultStyleKey and ItemContainerDefaultKey. DefaultStyleKey specifies
    /// the style key of ListView; ItemContainerDefaultKey specifies the style
    /// key of ListViewItem.
    /// </summary>
    public partial class VideoView : ViewBase
    {
        #region DefaultStyleKey

        protected override object DefaultStyleKey
        {
            get { return new ComponentResourceKey(GetType(), "VideoView"); }
        }

        #endregion

        #region ItemContainerDefaultStyleKey

        protected override object ItemContainerDefaultStyleKey
        {
            get { return new ComponentResourceKey(GetType(), "VideoListViewItem"); }
        }

        #endregion
    }
}