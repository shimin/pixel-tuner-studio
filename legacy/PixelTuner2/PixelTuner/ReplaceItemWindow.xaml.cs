using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PixelTuner.ViewModel;
using PixelTuner.Properties;
using Common.ViewModel;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for ReplaceItemWindow.xaml
    /// </summary>
    public partial class ReplaceItemWindow : Window
    {
        public ReplaceItemWindow()
            : this(new ImagesViewModel("ReplaceItemWindow"))
        {   
        }

        public ReplaceItemWindow(ViewModelBase model)
        {
            InitializeComponent();
            DataContext = model;
        }

        private void boxThumbnails_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                this.Close();
            }
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                this.Close();
            }
        }

        public object SelectedItem
        {
            get;
            set;
        }

        private void SearchHandler(object sender, RoutedEventArgs e)
        {
            var s = sender as TextBox;
            (this.DataContext as ImagesViewModel).RefreshImagesCollection(Settings.Default.LineHeight);
        }

        private void ListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (boxThumbnails.SelectedItem != null)
            {
                DialogResult = true;
                SelectedItem = boxThumbnails.SelectedItem;
                this.Close();
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = boxThumbnails.SelectedItem;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void boxThumbnails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = boxThumbnails.SelectedItem;
        }

    }
}
