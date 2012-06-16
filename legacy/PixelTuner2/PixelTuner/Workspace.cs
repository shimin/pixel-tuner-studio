using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using AvalonDock.Layout;
using System.Xml.Serialization;
using System.Xml;

namespace PixelTuner
{
    class Workspace : ViewModelBase
    {
        protected Workspace()
        { 
        
        }

        XmlSerializer _projectSerializer = new XmlSerializer(typeof(FileViewModel), FileViewModel.ExtraTypes.ToArray());

        static Workspace _this = new Workspace();

        public static Workspace This
        {
            get { return _this; }
        }

        ObservableCollection<FileViewModel> _files = new ObservableCollection<FileViewModel>();
        ReadOnlyObservableCollection<FileViewModel> _readonyFiles = null;
        public ReadOnlyObservableCollection<FileViewModel> Files
        {
            get
            {
                if (_readonyFiles == null)
                    _readonyFiles = new ReadOnlyObservableCollection<FileViewModel>(_files);

                return _readonyFiles;
            }
        }

        ToolViewModel[] _tools = null;

        public IEnumerable<ToolViewModel> Tools
        {
            get
            {
                if (_tools == null)
                    _tools = new ToolViewModel[] { FileStats };
                return _tools;
            }
        }

        FileStatsViewModel _fileStats = null;
        public FileStatsViewModel FileStats
        {
            get
            {
                if (_fileStats == null)
                    _fileStats = new FileStatsViewModel();

                return _fileStats;
            }
        }

        #region OpenCommand
        RelayCommand _openCommand = null;
        public ICommand OpenCommand
        {
            get
            {
                if (_openCommand == null)
                {
                    _openCommand = new RelayCommand((p) => OnOpen(p), (p) => CanOpen(p));
                }

                return _openCommand;
            }
        }

        private bool CanOpen(object parameter)
        {
            return true;
        }

        private void OnOpen(object parameter)
        {
            Open((string)parameter);
        }

        //public FileViewModel Open(string filepath)
        //{
        //    var fileViewModel = _files.FirstOrDefault(fm => fm.FilePath == filepath);
        //    if (fileViewModel != null)
        //        return fileViewModel;

        //    fileViewModel = new FileViewModel(filepath);
        //    _files.Add(fileViewModel);
        //    return fileViewModel;
        //}

        #endregion 

        #region NewCommand
        RelayCommand _newCommand = null;
        public ICommand NewCommand
        {
            get
            {
                if (_newCommand == null)
                {
                    _newCommand = new RelayCommand((p) => OnNew(p), (p) => CanNew(p));
                }

                return _newCommand;
            }
        }

        private bool CanNew(object parameter)
        {
            return true;
        }

        private void OnNew(object parameter)
        {
            var model = new FileViewModel();
            model.TimeLines.Add(new TimeLineViewModel());
            _files.Add(model);
            ActiveDocument = _files.Last();
        }

        #endregion 

        #region ActiveDocument

        private FileViewModel _activeDocument = null;
        public FileViewModel ActiveDocument
        {
            get { return _activeDocument; }
            set
            {
                if (_activeDocument != value)
                {
                    _activeDocument = value;
                    RaisePropertyChanged("ActiveDocument");
                    if (ActiveDocumentChanged != null)
                        ActiveDocumentChanged(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler ActiveDocumentChanged;

        #endregion

        #region SaveCommand
        RelayCommand _saveCommand = null;
        public ICommand SaveCommand
        {
            get
            {
                if (_saveCommand == null)
                {
                    _saveCommand = new RelayCommand((p) => OnSave(p), (p) => CanSave(p));
                }

                return _saveCommand;
            }
        }

        private bool CanSave(object parameter)
        {
            return ActiveDocument != null;
        }

        private void OnSave(object parameter)
        {
            Save(ActiveDocument);
        }

        #endregion 

        internal void Close(FileViewModel fileToClose)
        {
            if (fileToClose.IsDirty)
            {
                var res = MessageBox.Show(string.Format("Save changes for file '{0}'?", fileToClose.FileName), "AvalonDock Test App", MessageBoxButton.YesNoCancel);
                if (res == MessageBoxResult.Cancel)
                    return;
                if (res == MessageBoxResult.Yes)
                {
                    Save(fileToClose);
                }
            }

            _activeDocument = null;
            _files.Remove(fileToClose);
        }

        internal void Save(FileViewModel fileToSave, bool saveAsFlag = false)
        {
            if (fileToSave.FilePath == null || saveAsFlag)
            {
                var dlg = new SaveFileDialog();
                dlg.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                dlg.Filter = "PixelTuner project file|*.ptp";
                if (dlg.ShowDialog().GetValueOrDefault())
                    fileToSave.FilePath = dlg.SafeFileName;
            }

            if (fileToSave.FilePath != null)
            {
                using (XmlTextWriter tw = new XmlTextWriter(fileToSave.FilePath, Encoding.UTF8))
                {
                    tw.Formatting = Formatting.Indented;
                    _projectSerializer.Serialize(tw, fileToSave);
                    ///////////////////////////////
                    // исправить нотификацию об изменении имени файла.
                    _files.Remove(fileToSave);
                    _files.Add(fileToSave);
                    //RaisePropertyChanged("Files");
                }
                ActiveDocument.IsDirty = false;
            }
        }

        internal void Open(string path = null)
        {
            if (String.IsNullOrEmpty(path))
            {
                var dlg = new OpenFileDialog();
                dlg.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                dlg.Filter = "PixelTuner project file|*.ptp";
                if (dlg.ShowDialog().GetValueOrDefault())
                    path = dlg.FileName;
            }

            if (path != null)
            {
                try
                {
                    using (XmlTextReader tr = new XmlTextReader(path))
                    {
                        try
                        {
                            var file = (FileViewModel)_projectSerializer.Deserialize(tr);
                            file.FilePath = path;
                            this._files.Add(file);
                            ActiveDocument = file;
                        }
                        catch (Exception ex)
                        {
                            StringBuilder sb = new StringBuilder();
                            WriteException(sb, ex);
                            sb.AppendLine(ex.StackTrace);
                            try
                            {
                                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LastException.txt"), sb.ToString());
                            }
                            catch { }

                            ResourceDictionary rd = Application.Current.Resources.MergedDictionaries[0];
                            string msg = (String)rd["Doc_Open_DeserializeError"];
                            MessageBox.Show(msg, Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch 
                {
                    ResourceDictionary rd = Application.Current.Resources.MergedDictionaries[0];
                    string msg = (String)rd["Doc_Open_AccessError"];
                    MessageBox.Show(msg, Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                ActiveDocument.IsDirty = false;
            }
        }

        private void WriteException(StringBuilder sb, Exception ex)
        {
            sb.AppendLine(ex.Message);
            sb.AppendLine(ex.Source);
            if (ex.InnerException != null)
                WriteException(sb, ex.InnerException);
        }
    }
}
