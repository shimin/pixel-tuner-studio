using System;
using System.ComponentModel;
using System.Windows;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for FixMissingWindow.xaml
    /// </summary>
    public partial class FixMissingWindow : Window, INotifyPropertyChanged
    {
        private string _missingPath = "";
        public string MissingPath
        {
            get { return _missingPath; }
            set { _missingPath = value; RaisePropertyChanged("MissingPath"); }
        }

        private Action SetBaseDirectory { get; set; }

        private Action SetOtherImageFromCollection { get; set; }

        private Action SetOtherImageFromDrive { get; set; }

        private Action SetFileToIgnore { get; set; }

        private Action SetAllFilesToIgnore { get; set; }

        public Action SelectedAction { get; private set; }

        public FixMissingWindow(
            Action setBaseDirectory,
            Action setOtherImageFromCollection,
            Action setOtherImageFromDrive,
            Action setFileToIgnore,
            Action setAllFilesToIgnore)
        {
            SetBaseDirectory = setBaseDirectory;
            SetOtherImageFromCollection = setOtherImageFromCollection;
            SetOtherImageFromDrive = setOtherImageFromDrive;
            SetFileToIgnore = setFileToIgnore;
            SetAllFilesToIgnore = setAllFilesToIgnore;

            InitializeComponent();
            DataContext = this;
            AdjustControls();
        }

        private void AdjustControls()
        {
            if (SetBaseDirectory == null)
            {
                boxSetBaseDirectory.IsChecked = false;
                boxSetBaseDirectory.IsEnabled = false;
            }
            else
            {
                boxSetBaseDirectory.IsChecked = true;
            }

            if (SetOtherImageFromCollection == null)
            {
                boxSetOtherImageFromCollection.IsChecked = false;
                boxSetOtherImageFromCollection.IsEnabled = false;
            }
            else
            {
                if (boxSetBaseDirectory.IsEnabled != true)
                    boxSetOtherImageFromCollection.IsChecked = true;
            }

            if (SetOtherImageFromDrive == null)
            {
                boxSetOtherImageFromDrive.IsChecked = false;
                boxSetOtherImageFromDrive.IsEnabled = false;
            }
            else
            {
                if (boxSetOtherImageFromCollection.IsEnabled != true)
                    boxSetOtherImageFromDrive.IsChecked = true;
            }

            if (SetFileToIgnore == null)
            {
                boxSetFileToIgnore.IsChecked = false;
                boxSetFileToIgnore.IsEnabled = false;
            }
            else
            {
                if (boxSetOtherImageFromDrive.IsEnabled != true)
                    boxSetFileToIgnore.IsChecked = true;
            }

            if (SetAllFilesToIgnore == null)
            {
                boxSetAllFilesToIgnore.IsChecked = false;
                boxSetAllFilesToIgnore.IsEnabled = false;
            }
            else
            {
                if (boxSetFileToIgnore.IsEnabled != true)
                    boxSetAllFilesToIgnore.IsChecked = true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void boxOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            SelectedAction = SetSelectedAction();
            Close();
        }

        private Action SetSelectedAction()
        {
            if (SetBaseDirectory != null && boxSetBaseDirectory.IsChecked == true) return SetBaseDirectory;
            if (SetOtherImageFromCollection != null && boxSetOtherImageFromCollection.IsChecked == true) return SetOtherImageFromCollection;
            if (SetOtherImageFromDrive != null && boxSetOtherImageFromDrive.IsChecked == true) return SetOtherImageFromDrive;
            if (SetFileToIgnore != null && boxSetFileToIgnore.IsChecked == true) return SetFileToIgnore;
            if (SetAllFilesToIgnore != null && boxSetAllFilesToIgnore.IsChecked == true) return SetAllFilesToIgnore;
            return null;
        }

        private void boxCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            SelectedAction = null;
            Close();
        }
    }
}
