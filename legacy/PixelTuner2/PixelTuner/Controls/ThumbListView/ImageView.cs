using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PixelTuner.ViewModel;
using System.Text;
using PixelTuner.Properties;
using Common.Imaging.UnsharpMask;

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
                        var testFileName = GenerateFileName(sourcePath, p);
                        if (img.Height != Settings.Default.LineHeight)
                        {
                            var cropWindow = new CroppingWindow(new Uri(p));
                            if (cropWindow.ShowDialog() == true)
                            {
                                System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(cropWindow.ResizedImageGDI);
                                if (cropWindow.ApplySharpening) bmp = bmp.UnsharpGaussian3x3(1.0f);
                                Common.Imaging.ImageUtilities.SaveBmp(testFileName, bmp.UnsharpGaussian3x3(1.0f));
                            }
                        }
                        else
                        {
                            File.Copy(p, testFileName);
                        }
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
        }

        private void SearchHandler(object sender, RoutedEventArgs e)
        {
            var s = sender as TextBox;
            RootViewModel.This.ImagesModel.ApplyFilter(s.Text);
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
                    if (fm.SelectedTimeline != null)
                    {
                        var src = item.DataContext as TimeLineItemViewModel;
                        if (src != null)
                        {
                            var newItem = (TimeLineItemViewModel)src.Clone();
                            int insertPosition = fm.SelectedTimeline.Items.Count;
                            foreach (TimeLineItemViewModel i in fm.SelectedTimeline.Items)
                                if (i.IsSelected)
                                {
                                    insertPosition = fm.SelectedTimeline.Items.IndexOf(i); break;
                                }
                            if (insertPosition == 0)
                            {
                                newItem.StartTime = fm.SelectedTimeline.Start;
                            }
                            else
                            {
                                newItem.StartTime = fm.SelectedTimeline.Items[insertPosition - 1].EndTime;
                            }
                            newItem.EndTime = newItem.StartTime.Value.AddSeconds(Settings.Default.DefaultDuration);
                            fm.SelectedTimeline.Items.Insert(insertPosition, newItem);
                        }
                    }
                    break;
                }
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
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