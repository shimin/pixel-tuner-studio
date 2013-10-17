using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PixelTuner.ViewModel;
using System.Text;
using PixelTuner.Properties;

namespace PixelTuner.Controls.ThumbListView
{
    public partial class ThumbnailListView : ResourceDictionary
    {
        private void boxThumbnails_Drop(object sender, DragEventArgs e)
        {
            var imagesModel = ((ListView)sender).DataContext as ImagesViewModel;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                StringBuilder sb = new StringBuilder();
                string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
                foreach (var p in droppedFilePaths)
                {
                    try
                    {
                        System.Drawing.Image img = System.Drawing.Image.FromFile(p);
                        if (img.Height > 150 || img.Width > 2000)
                        {
                            sb.AppendLine(String.Format("Wrong picture size. File: {0}", p));
                            continue;
                        }
                        string destFileName = "";
                        var shortName = Path.GetFileName(p);
                        var testFileName = destFileName = Path.Combine(sourcePath, shortName);

                        int idx = 0;
                        while (File.Exists(testFileName))
                        {
                            idx++;
                            testFileName = Path.Combine(sourcePath, Path.GetFileNameWithoutExtension(destFileName) +
                                "_" + idx.ToString() + Path.GetExtension(destFileName));
                        }
                        File.Copy(p, testFileName);
                    }
                    catch
                    {
                        sb.AppendLine(String.Format("Error copying image. File: {0}", p));
                    }
                }
                imagesModel.RefreshImagesCollection(PixelTuner.Properties.Settings.Default.LineHeight);
                if (sender is UIElement) (sender as UIElement).Focus();
            }
        }

        private void boxThumbnails_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                var control = sender as ListView;
                if (control != null)
                {
                    var itemToRemove = control.SelectedItem as ThumbnailFileItem;
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
            RootViewModel.This.ImagesModel.ApplyFilter(s.Text);
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
            saveDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath + "\\Content";
            saveDialog.Filter = "Bmp files|*.bmp";
            if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                img.Save(saveDialog.FileName);
            }
            RootViewModel.This.ImagesModel.RefreshImagesCollection(Settings.Default.LineHeight);
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
            RootViewModel.This.ImagesModel.RefreshImagesCollection();
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
                    saveDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath + "\\Content";
                    saveDialog.Filter = "Bmp files|*.bmp";
                    if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        img.Save(saveDialog.FileName);
                    }
                    RootViewModel.This.ImagesModel.RefreshImagesCollection();
                }
            }
        }

        #endregion
    }
    /// <summary>
    /// ImageView displays image files using themselves as their icons.
    /// In order to write our own visual tree of a view, we should override its
    /// DefaultStyleKey and ItemContainerDefaultKey. DefaultStyleKey specifies
    /// the style key of ListView; ItemContainerDefaultKey specifies the style
    /// key of ListViewItem.
    /// </summary>
    public partial class ImageView : ViewBase
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
}