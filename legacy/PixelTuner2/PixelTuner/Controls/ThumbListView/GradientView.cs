using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Common.Imaging;
using System.Windows.Media.Imaging;
using PixelTuner.ViewModel;
using PixelTuner.Properties;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.ComponentModel;

namespace PixelTuner.Controls.ThumbListView
{
    public partial class ThumbnailGradientListView : ResourceDictionary
    {
        static string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        string _sourcePath = Path.Combine(picturesPath, "PixelTuner 2\\Gradients");

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

            ListView imagesView = (ListView)args[0];
            string[] fileNames = (string[])args[1];

            GradientsViewModel imagesModel = null;
            imagesView.Dispatcher.Invoke(new Action(() => imagesModel = imagesView.DataContext as GradientsViewModel));
            
            StringBuilder sb = new StringBuilder();

            foreach (var p in fileNames)
            {
                try
                {
                    var testFileName = GenerateFileName(_sourcePath, p);
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        var importWindow = new Import.ImportWindow(new BitmapImage(new Uri(p)));
                        if (importWindow.ShowDialog() == true)
                        {
                            System.Drawing.Bitmap bmp = ImageHelper.BitmapImageToBitmap<BmpBitmapEncoder>(importWindow.ResultImage);
                            Common.Imaging.ImageHelper.SaveBmp(testFileName, bmp);
                        }
                    }));
                }
                catch
                {
                    sb.AppendLine(String.Format("Error copying image. File: {0}", p));
                }
            }

            if (sb.Length > 0) MessageBox.Show(sb.ToString());
            imagesModel.RefreshImagesCollection();
            imagesView.Dispatcher.Invoke(new Action(() => imagesView.Focus()));
        }

        private static string GenerateFileName(string baseDirectory, string dropFileName)
        {
            string destFileName = "";
            var shortName = Path.GetFileNameWithoutExtension(dropFileName);
            shortName += ".bmp";
            var testFileName = destFileName = Path.Combine(baseDirectory, shortName);

            int idx = 0;
            while (File.Exists(testFileName))
            {
                idx++;
                testFileName = Path.Combine(baseDirectory, Path.GetFileNameWithoutExtension(destFileName) +
                    "_" + idx.ToString() + Path.GetExtension(destFileName));
            }
            return testFileName;
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
            else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var img = ImageHelper.ImageFromClipboard();
                if (img != null)
                {
                    var importWindow = new Import.ImportWindow(img);
                    if (importWindow.ShowDialog() == true)
                    {
                        System.Drawing.Bitmap bmp = ImageHelper.BitmapImageToBitmap<BmpBitmapEncoder>(importWindow.ResultImage);
                        var saveDialog = new System.Windows.Forms.SaveFileDialog();
                        saveDialog.InitialDirectory = _sourcePath;
                        saveDialog.Filter = "Bmp files|*.bmp";
                        if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            Common.Imaging.ImageHelper.SaveBmp(saveDialog.FileName, bmp);
                        }
                    }
                }
            }
        }

        private void SearchHandler(object sender, RoutedEventArgs e)
        {
            var s = sender as TextBox;
            RootViewModel.This.GradientsModel.RefreshImagesCollection();
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
                            foreach (TimeLineItemViewModel i in fm.SelectedTimeline.Items)
                            {
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
            var item = (sender as ListView).SelectedItem as TimeLineItemViewModel;
            if (item != null)
            {
                if (RootViewModel.This.ProjectPlayerModel.IsVisible)
                    RootViewModel.This.ProjectPlayerModel.DisplayModel = new TimeLineItemViewModel(new FileInfo(item.FullName));
            }
        }

        #region Picture editor

        private void Create_Button_Click(object sender, RoutedEventArgs e)
        {
            var c = new PixelTuner.Drawing.ConfigurationItem(Settings.Default.LineHeight, 84, 8);
            PictureEditor pe = new PictureEditor(c);
            pe.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            pe.Owner = Application.Current.MainWindow;
            pe.ShowDialog();
            var img = pe.Config.GetScaledImage();
            var saveDialog = new System.Windows.Forms.SaveFileDialog();
            saveDialog.InitialDirectory = _sourcePath;
            saveDialog.Filter = "Bmp files|*.bmp";
            if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                img.Save(saveDialog.FileName);
            }
            RootViewModel.This.GradientsModel.RefreshImagesCollection();
        }

        private void Delete_Button_Click(object sender, RoutedEventArgs e)
        {
            var itemToRemove = RootViewModel.This.ImagesModel.SelectedItem;
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
            RootViewModel.This.GradientsModel.RefreshImagesCollection();
        }

        private void Change_Button_Click(object sender, RoutedEventArgs e)
        {
            var thumbItem = RootViewModel.This.ImagesModel.SelectedItem;

            if (thumbItem != null)
            {
                var image = System.Drawing.Image.FromFile(thumbItem.FullName);
                if (image != null)
                {
                    var c = new PixelTuner.Drawing.ConfigurationItem(Settings.Default.LineHeight, 84, 8);
                    c.SetScaledImage(image, true);

                    PictureEditor pe = new PictureEditor(c);
                    pe.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                    pe.Owner = Application.Current.MainWindow;
                    pe.ShowDialog();
                    var img = pe.Config.GetScaledImage();
                    var saveDialog = new System.Windows.Forms.SaveFileDialog();
                    saveDialog.FileName = thumbItem.Name;
                    saveDialog.InitialDirectory = _sourcePath;
                    saveDialog.Filter = "Bmp files|*.bmp";
                    if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        img.Save(saveDialog.FileName);
                    }
                    RootViewModel.This.GradientsModel.RefreshImagesCollection();
                }
            }
        }

        #endregion
    }

    public partial class GradientView : ViewBase
    {
        #region DefaultStyleKey

        protected override object DefaultStyleKey
        {
            get { return new ComponentResourceKey(GetType(), "GradientView"); }
        }

        #endregion

        #region ItemContainerDefaultStyleKey

        protected override object ItemContainerDefaultStyleKey
        {
            get { return new ComponentResourceKey(GetType(), "GradientListViewItem"); }
        }

        #endregion
    }
}
