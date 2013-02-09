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
using PixelTuner.Properties;
using System.ComponentModel;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for DefaultsWindow.xaml
    /// </summary>
    public partial class DefaultsWindow : Window
    {
        public DefaultsWindow()
        {
            this.DataContext = this;
            InitializeComponent();
        }

        int _stretching = Settings.Default.DefaultStretching;
        public int Stretching
        {
            get { return _stretching; }
            set { _stretching = value; }
        }

        int _defaultDuration = (int)Settings.Default.DefaultDuration;
        public int DefaultDuration
        {
            get { return _defaultDuration; }
            set { _defaultDuration = value; }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.DefaultDuration = DefaultDuration;
            PixelTuner.ListViewStyle.ThumbnailFileItem.SetDefaultStretching(Stretching);
            Settings.Default.DefaultStretching = Stretching;
            Settings.Default.Save();
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void numeric_KeyDown(object sender, KeyEventArgs e)
        {

        }

    }
}
