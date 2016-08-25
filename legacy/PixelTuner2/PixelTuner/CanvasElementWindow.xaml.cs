using PixelTuner.ViewModel;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for CanvasElementWindow.xaml
    /// </summary>
    public partial class CanvasElementWindow : Window, INotifyPropertyChanged
    {
        private CanvasElementViewModel _canvasElement;
        public CanvasElementViewModel CanvasElement
        {
            get
            {
                return _canvasElement;
            }

            set
            {
                DataContext = _canvasElement = value; RaisePropertyChanged("CanvasElement");
            }
        }

        public CanvasElementWindow()
        {
            InitializeComponent();
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void boxCloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Button b = sender as Button;
            Console.WriteLine(e.LeftButton);
            if (e.LeftButton == MouseButtonState.Pressed ||
                e.MouseDevice.LeftButton == MouseButtonState.Pressed)
            {
                var ledModel = b.DataContext as LedTile;
                ledModel.TurnOnCommand.Execute(null);
            }
        }

        private void Button_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
        }
    }
}
