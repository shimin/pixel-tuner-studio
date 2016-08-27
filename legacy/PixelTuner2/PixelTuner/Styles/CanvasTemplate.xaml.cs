using System.Windows;
using System.Windows.Input;

using PixelTuner.ViewModel;
using System.Windows.Threading;
using System;

namespace PixelTuner.Styles
{
    internal partial class CanvasTemplate : ResourceDictionary
    {
        public CanvasTemplate()
        {
            InitializeComponent();
        }

        System.Windows.Controls.Canvas canvas = null;

        private void Path_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var s = sender as FrameworkElement;
            if (s != null)
            {
                string path = (s.DataContext as CanvasElementViewModel).Path;
            }
        }
        
        private void Viewbox_Loaded(object sender, RoutedEventArgs e)
        {
            // заменить на команду
            var s = sender as System.Windows.Controls.Viewbox;
            if (s != null && s.DataContext != null) 
            {
                (s.DataContext as CanvasViewModel).LoadContent();
            }
        }
    }
}
