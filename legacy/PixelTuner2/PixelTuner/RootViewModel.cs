using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using Workspace.Layout;
using System.Xml.Serialization;
using System.Xml;
using PixelTuner.Properties;
using PixelTuner.ListViewStyle;

namespace PixelTuner
{
    public class RootViewModel : ViewModelBase
    {
        public RootViewModel()
        {
            RefreshImagesCollection();
            this.Id = Guid.NewGuid();
            //_files.Add(new FileViewModel("qwe.t"));
            this.ActiveDocumentChanged +=new DocumentChangedEventHandler(Workspace_ActiveDocumentChanged);
        }

        void Workspace_ActiveDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            if (_activeDocument == null) RefreshImagesCollection();
            else
            {
                RefreshImagesCollection(_activeDocument.LineHeight);
                e.NewDocument.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(NewDocument_PropertyChanged);
                if (e.OldDocument != null)
                    e.OldDocument.PropertyChanged -= NewDocument_PropertyChanged;
            }
        }

        void NewDocument_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "LineHeight") 
                if (sender is FileViewModel)
                    RefreshImagesCollection((sender as FileViewModel).LineHeight);
        }

        XmlSerializer _projectSerializer = new XmlSerializer(typeof(FileViewModel), FileViewModel.ExtraTypes.ToArray());

        static RootViewModel _this = null;

        public static RootViewModel This
        {
            get { return _this; }
            set { _this = value; }
        }

        ObservableCollection<FileViewModel> _files = new ObservableCollection<FileViewModel>();
        ReadOnlyObservableCollection<FileViewModel> _readonlyFiles = null;
        public ReadOnlyObservableCollection<FileViewModel> Files
        {
            get
            {
                if (_readonlyFiles == null)
                    _readonlyFiles = new ReadOnlyObservableCollection<FileViewModel>(_files);

                return _readonlyFiles;
            }
        }

        ToolViewModel[] _tools = null;

        public IEnumerable<ToolViewModel> Tools
        {
            get
            {
                if (_tools == null)
                    _tools = new ToolViewModel[] { FileStats, DevicesModel, ProjectsModel };
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

        DevicesViewModel _devicesModel = null;
        public DevicesViewModel DevicesModel
        {
            get
            {
                if (_devicesModel == null)
                    _devicesModel = new DevicesViewModel(
                        (String)Application.Current.TryFindResource("Menu_Window_Devices"));

                return _devicesModel;
            }
        }

        ProjectsViewModel _projectsModel = null;
        public ProjectsViewModel ProjectsModel
        {
            get
            {
                if (_projectsModel == null)
                {
                    _projectsModel = new ProjectsViewModel(
                        (String)Application.Current.TryFindResource("Menu_Window_Projects"));
                    _projectsModel.InitializeFilesList(_files);
                }

                return _projectsModel;
            }
        }

        private Visibility _updatesAvailable = Visibility.Collapsed;
        public Visibility UpdatesAvailable
        {
            get { return _updatesAvailable; }
            set { _updatesAvailable = value; RaisePropertyChanged("UpdatesAvailable"); }
        }

        public string AssemblyVersion
        {
            get
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version.ToString();
            }
        }

        #region UpdateCommand
        RelayCommand _updateCommand = null;
        public ICommand UpdateCommand
        {
            get
            {
                if (_updateCommand == null)
                {
                    _updateCommand = new RelayCommand((p) => OnUpdate(p), (p) => CanUpdate(p));
                }

                return _updateCommand;
            }
        }

        private bool CanUpdate(object parameter)
        {
            return true;
        }

        private void OnUpdate(object parameter)
        {
            CheckForUpdates();
        }

        private void CheckForUpdates()
        {
            #region check and download new version program
            bool bHasError = false;
            PixelTunerAutoupdater.IAutoUpdater autoUpdater = new PixelTunerAutoupdater.AutoUpdater(Application.Current.MainWindow);
            try
            {
                autoUpdater.Update();
            }
            catch (System.Net.WebException exp)
            {
                MessageBox.Show("Can not find the specified resource");
                bHasError = true;
            }
            catch (System.Xml.XmlException exp)
            {
                bHasError = true;
                MessageBox.Show("Download the upgrade file error");
            }
            catch (NotSupportedException exp)
            {
                bHasError = true;
                MessageBox.Show("Upgrade address configuration error");
            }
            catch (ArgumentException exp)
            {
                bHasError = true;
                MessageBox.Show("Download the upgrade file error");
            }
            catch (Exception exp)
            {
                bHasError = true;
                MessageBox.Show("An error occurred during the upgrade process");
            }
            finally
            {
                if (bHasError == true)
                {
                    try
                    {
                        autoUpdater.RollBack();
                    }
                    catch (Exception)
                    {
                        //Log the message to your file or database
                    }
                }
            }
            #endregion
        }
        #endregion 

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

        private Guid _id = Guid.Empty;

        public Guid Id
        {
            get { return _id; }
            set { _id = value; }
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
                    FileViewModel oldDoc = _activeDocument;     // сохраняю ссылку на предыдущий документ, чтобы иметь к нему доступ в обработчике события
                    _activeDocument = value;
                    RaisePropertyChanged("ActiveDocument");
                    if (ActiveDocumentChanged != null)
                        ActiveDocumentChanged(this, new DocumentChangedEventArgs(oldDoc, _activeDocument));
                }
            }
        }
        public class DocumentChangedEventArgs : EventArgs
        {
            FileViewModel _oldDoc;
            public FileViewModel OldDocument
            {
                get { return _oldDoc; }
            }
            FileViewModel _newDoc;
            public FileViewModel NewDocument
            {
                get { return _newDoc; }
            }

            public DocumentChangedEventArgs(FileViewModel oldDoc, FileViewModel newDoc)
            {
                _oldDoc = oldDoc;
                _newDoc = newDoc;
            }
        }
        public delegate void DocumentChangedEventHandler(object sender, DocumentChangedEventArgs e);
        public event DocumentChangedEventHandler ActiveDocumentChanged;

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

        #region SaveAsCommand
        RelayCommand _saveAsCommand = null;
        public ICommand SaveAsCommand
        {
            get
            {
                if (_saveAsCommand == null)
                {
                    _saveAsCommand = new RelayCommand((p) => OnSaveAs(p), (p) => CanSaveAs(p));
                }

                return _saveAsCommand;
            }
        }

        private bool CanSaveAs(object parameter)
        {
            return ActiveDocument != null;
        }

        private void OnSaveAs(object parameter)
        {
            Save(ActiveDocument, true);
        }

        #endregion 

        #region CloseCommand
        RelayCommand _closeCommand = null;
        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                {
                    _closeCommand = new RelayCommand((p) => OnClose(p), (p) => CanClose(p));
                }

                return _closeCommand;
            }
        }

        private bool CanClose(object parameter)
        {
            return ActiveDocument != null;
        }

        private void OnClose(object parameter)
        {
            Close(ActiveDocument);
        }

        #endregion 

        internal void Close(FileViewModel fileToClose)
        {
            if (fileToClose.IsDirty)
            {
                var res = MessageBox.Show(string.Format((String)Application.Current.TryFindResource("Global_SaveChanges"), fileToClose.FileName), 
                    Application.Current.MainWindow.Title, MessageBoxButton.YesNoCancel);
                if (res == MessageBoxResult.Cancel)
                    return;
                if (res == MessageBoxResult.Yes)
                {
                    Save(fileToClose);
                }
            }

            FileViewModel oldDoc = _activeDocument;
            _files.Remove(fileToClose);
            _activeDocument = _files.Count > 0 ? _files.Last() : null;
            RaisePropertyChanged("ActiveDocument");
            if (ActiveDocumentChanged != null)
                ActiveDocumentChanged(this, new DocumentChangedEventArgs(oldDoc, _activeDocument));
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
                    RaisePropertyChanged("ActiveDocument");
                    if (ActiveDocumentChanged != null)
                        ActiveDocumentChanged(this, new DocumentChangedEventArgs(_activeDocument, _activeDocument));
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
                            ActiveDocument.IsDirty = false;
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
            }
        }

        private void WriteException(StringBuilder sb, Exception ex)
        {
            sb.AppendLine(ex.Message);
            sb.AppendLine(ex.Source);
            if (ex.InnerException != null)
                WriteException(sb, ex.InnerException);
        }

        ObservableCollection<ThumbnailFileItem> _images = new ObservableCollection<ThumbnailFileItem>();
        ReadOnlyObservableCollection<ThumbnailFileItem> _readonyImagesCollection = null;
        public ReadOnlyObservableCollection<ThumbnailFileItem> ImagesCollection
        {
            get
            {
                if (_readonyImagesCollection == null)
                    _readonyImagesCollection = new ReadOnlyObservableCollection<ThumbnailFileItem>(_images);

                return _readonyImagesCollection;
            }
        }

        private void RefreshImagesCollection(int height = 0)
        {
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }
            DirectoryInfo info = new DirectoryInfo(sourcePath);
            List<FileSystemInfo> allfiles = new List<FileSystemInfo>( info.GetFileSystemInfos("*.png") );
            allfiles.AddRange(info.GetFileSystemInfos("*.jpg"));
            allfiles.AddRange(info.GetFileSystemInfos("*.bmp"));
            if (height > 0)
            {
                List<FileSystemInfo> suitableFiles = new List<FileSystemInfo>();
                foreach (var f in allfiles)
                {
                    System.Drawing.Image img = System.Drawing.Image.FromFile(f.FullName);
                    if (img.Height == height)
                        suitableFiles.Add(f);
                }
                allfiles = suitableFiles;
            }
            _images.Clear();
            foreach (FileInfo fi in allfiles)
            {
                // exlude hidden files 
                ThumbnailFileItem tfi = new ThumbnailFileItem(fi);
                if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    _images.Add(tfi);
            }
        }
    }
}
