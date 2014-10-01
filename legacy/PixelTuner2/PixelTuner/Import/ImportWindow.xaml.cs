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
using Common.Imaging;

namespace PixelTuner.Import
{
    /// <summary>
    /// Interaction logic for ImportWindow.xaml
    /// </summary>
    public partial class ImportWindow : Window
    {
        private Stack<UserControl> pagesNext = new Stack<UserControl>();
        private Stack<UserControl> pagesPrev = new Stack<UserControl>();

        private BitmapImage _resultImage = null;
        public BitmapImage ResultImage
        {
            get { return _resultImage; }
        }

        public ImportWindow(ImageSource imageToCrop)
        {
            DataContext = this;
            //pagesNext.Push(new TransformPage());
            pagesNext.Push(new ColorAdjustPage());
            pagesNext.Push(new CropPage(imageToCrop));
            InitializeComponent();
            pageTransitionControl.ShowPage(pagesNext.Peek());
        }

        #region BackCommand
        
        RelayCommand _backCommand = null;
        public ICommand BackCommand
        {
            get
            {
                if (_backCommand == null)
                {
                    _backCommand = new RelayCommand((p) => OnBack(p), (p) => CanBack(p));
                }

                return _backCommand;
            }
        }

        private bool CanBack(object parameter)
        {
            return pagesPrev.Count > 0;
        }

        private void OnBack(object parameter)
        {
            pagesNext.Push(pagesPrev.Pop());
            pageTransitionControl.ShowPage(pagesNext.Peek());
        }

        #endregion 
        
        #region NextCommand

        RelayCommand _nextCommand = null;
        public ICommand NextCommand
        {
            get
            {
                if (_nextCommand == null)
                {
                    _nextCommand = new RelayCommand((p) => OnNext(p), (p) => CanNext(p));
                }

                return _nextCommand;
            }
        }

        private bool CanNext(object parameter)
        {
            return pagesNext.Count > 1;
        }

        private void OnNext(object parameter)
        {
            var current = (IPicturePage)pagesNext.Peek();
            current.ApplyChanges();
            pagesPrev.Push(pagesNext.Pop());

            var next = (IPicturePage)pagesNext.Peek();
            next.SetSource(current.Result);
            pageTransitionControl.ShowPage(pagesNext.Peek());
        }

        #endregion 

        private void boxOK_Click(object sender, RoutedEventArgs e)
        {
            IPicturePage page = (IPicturePage)pagesNext.Peek();
            page.ApplyChanges();
            _resultImage = (BitmapImage)page.Result;
            this.DialogResult = true;
            Close();
        }

        private void boxCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var img = ImageHelper.ImageFromClipboard();
                if (img != null)
                {
                    pagesNext.Clear();
                    pagesPrev.Clear();
                    pagesNext.Push(new ColorAdjustPage());
                    pagesNext.Push(new CropPage(img));
                    pageTransitionControl.ShowPage(pagesNext.Peek());
                }
            }
        }
    }
}
