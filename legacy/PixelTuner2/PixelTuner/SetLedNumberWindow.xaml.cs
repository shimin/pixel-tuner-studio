using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Interaction logic for SetLedNumberWindow.xaml
    /// </summary>
    public partial class SetLedNumberWindow : Window
    {
        public LedTile Tile { get; set; }

        public SetLedNumberWindow()
        {
            DataContext = this;
            InitializeComponent();
            Loaded += SetLedNumberWindow_Loaded;
        }

        private void SetLedNumberWindow_Loaded(object sender, RoutedEventArgs e)
        {
            boxValue.Value = Tile.LedNumber;
        }

        public SetLedNumberWindow(LedTile tile)
            : this()
        {
            Tile = tile;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            if (DialogResult == true)
            {
                Tile.Selected = true;
                Tile.LedNumber = (int) boxValue.Value;
            }
        }

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
