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

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for EffectTransitionWindow.xaml
    /// </summary>
    public partial class EffectTransitionWindow : Window
    {
        public EffectTransitionWindow(EffectTransitionThumbnailViewModel model)
        {
            InitializeComponent();
            this.DataContext = model;
        }
        
        public bool NeedRefresh { get; set; }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape: e.Handled = true; this.Close(); break;
                case Key.Enter: e.Handled = true; this.DialogResult = true; this.Close(); break;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            boxC1.Focus();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
