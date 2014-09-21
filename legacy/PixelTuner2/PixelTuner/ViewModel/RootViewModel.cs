using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;

using Common.ViewModel;
using PixelTuner.Properties;
using PixelTuner.Controls.ThumbListView;
using Workspace.Layout;
using Common.Log;

namespace PixelTuner.ViewModel
{
    public class RootViewModel : ViewModelBase
    {
#if DEBUG
        public static Logger Logger = null;
#endif

        public RootViewModel()
        {
            this.Id = Guid.NewGuid();
            _anchorStepCollection = new Dictionary<int, string>(){
                {20, "20 ms"},
                {50, "50 ms"},
                {100, "100 ms"},
                {250, "250 ms"},
                {500, "500 ms"},
                {1000, "1 s"}};
#if DEBUG
            Logger = new Logger(new FileTarget("log.txt"));
#endif
            this.ActiveDocumentChanged +=new DocumentChangedEventHandler(Workspace_ActiveDocumentChanged);
        }

        void Workspace_ActiveDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            if (_activeDocument == null) ImagesModel.RefreshImagesCollection(Settings.Default.LineHeight);
            else
            {
                Settings.Default.LineHeight = _activeDocument.LineHeight;
                ImagesModel.RefreshImagesCollection(Settings.Default.LineHeight);
                e.NewDocument.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(NewDocument_PropertyChanged);
                if (e.OldDocument != null)
                    e.OldDocument.PropertyChanged -= NewDocument_PropertyChanged;
            }
        }

        void NewDocument_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "LineHeight")
                if (sender is FileViewModel)
                {
                    Settings.Default.LineHeight = (sender as FileViewModel).LineHeight;
                    ImagesModel.RefreshImagesCollection(Settings.Default.LineHeight);
                }
        }

        public static void NotifySettingsChanged()
        {
            RaiseApplicationSettingsChanged();
        }

        #region Properties

        static RootViewModel _this = null;
        public static RootViewModel This
        {
            get { return _this; }
            set { _this = value; }
        }

        public string AssemblyVersion
        {
            get
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version.ToString();
            }
        }

        private bool _updatesAvailable = false;
        public bool UpdatesAvailable
        {
            get { return _updatesAvailable; }
            set { _updatesAvailable = value; RaisePropertyChanged("UpdatesAvailable"); }
        }

        private Guid _id = Guid.Empty;
        public Guid Id
        {
            get { return _id; }
            set { _id = value; }
        }

        private ObservableCollection<TimeLineItemViewModel> _copiedItems = new ObservableCollection<TimeLineItemViewModel>();
        public ObservableCollection<TimeLineItemViewModel> CopiedItems
        {
            get { return _copiedItems; }
            set { _copiedItems = value; RaisePropertyChanged("CopiedItems"); }
        }

        private TimeLineViewModel _copiedTimeline = null;
        public TimeLineViewModel CopiedTimeline
        {
            get { return _copiedTimeline; }
            set { _copiedTimeline = value; RaisePropertyChanged("CopiedTimeline"); }
        }

        XmlSerializer _projectSerializer = new XmlSerializer(typeof(FileViewModel), FileViewModel.ExtraTypes.ToArray());

        /// <summary>
        /// Step of rulers on timelines to link with items borders
        /// </summary>
        public int AnchorStep
        {
            get { return Settings.Default.AnchorStep; }
            set { Settings.Default.AnchorStep = value; RaisePropertyChanged("AnchorStep"); }
        }

        private Dictionary<int, string> _anchorStepCollection;
        public Dictionary<int, string> AnchorStepCollection
        {
            get { return _anchorStepCollection; }
        }

        /// <summary>
        /// Switches insertion mode: false if inserted items moves all the rest timeline items
        /// if true inserted time interval replaces the rest items
        /// </summary>
        public bool CutNextItems
        {
            get { return Settings.Default.CutNextItems; }
            set { Settings.Default.CutNextItems = value; RaisePropertyChanged("CutNextItems"); }
        }

        #endregion

        #region View models

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
                    _tools = new ToolViewModel[] { //FileStats,
                        DevicesModel, ProjectsModel, ImagesModel, ProjectPlayerModel
                    };
                return _tools;
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

        ProjectPlayerViewModel _projectPlayerModel = null;
        public ProjectPlayerViewModel ProjectPlayerModel
        {
            get
            {
                if (_projectPlayerModel == null)
                {
                    _projectPlayerModel = new ProjectPlayerViewModel(
                        (String)Application.Current.TryFindResource("Menu_Window_ProjectPlayer"));
                }

                return _projectPlayerModel;
            }
        }

        #region Images model

        ImagesViewModel _imagesModel = null;
        public ImagesViewModel ImagesModel
        {
            get
            {
                if (_imagesModel == null)
                {
                    _imagesModel = new ImagesViewModel(
                        (String)Application.Current.TryFindResource("Menu_Window_Images"));
                }

                return _imagesModel;
            }
        }

        public void DeleteItem(TimeLineItemViewModel itemToRemove)
        {
            if (itemToRemove != null)
            {
                try
                {
                    ImagesModel.RefreshImagesCollection(Settings.Default.LineHeight, itemToRemove.FullName);
                    RemoveBrokenReferences();
                }
                catch (Exception ex)
                {
                    ImagesModel.RefreshImagesCollection(Settings.Default.LineHeight);
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void RemoveBrokenReferences()
        {
            foreach (var fileView in _files)
            {
                foreach (var timeline in fileView.TimeLines)
                {
                    List<TimeLineItemViewModel> toRemove = new List<TimeLineItemViewModel>();
                    foreach (var item in timeline.Items)
                    {
                        if (!File.Exists((item as TimeLineItemViewModel).FullName))
                            toRemove.Add(item as TimeLineItemViewModel);
                    }
                    foreach (var item in toRemove)
                        timeline.Items.Remove(item);
                }
            }
        }

        #endregion

        #endregion

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
            catch (System.Net.WebException)
            {
                MessageBox.Show("Can not find the specified resource");
                bHasError = true;
            }
            catch (System.Xml.XmlException)
            {
                bHasError = true;
                MessageBox.Show("Download the upgrade file error");
            }
            catch (NotSupportedException)
            {
                bHasError = true;
                MessageBox.Show("Upgrade address configuration error");
            }
            catch (ArgumentException)
            {
                bHasError = true;
                MessageBox.Show("Download the upgrade file error");
            }
            catch (Exception exp)
            {
                bHasError = true;
                MessageBox.Show("An error occurred during the upgrade process" + exp.Message);
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
            RemoveBrokenReferences();
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
                            if (!string.IsNullOrEmpty(file.AudioPath))
                                file.OpenAudio.Execute(file.AudioPath);
                            ActiveDocument = file;
                            ActiveDocument.IsDirty = false;
                        }
                        catch (Exception ex)
                        {
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

        internal void Save(FileViewModel fileToSave, bool saveAsFlag = false)
        {
            if (fileToSave.FilePath == null || saveAsFlag || Path.GetExtension(fileToSave.FilePath) == ".tmp")
            {
                var dlg = new SaveFileDialog();
                dlg.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                dlg.Filter = "PixelTuner project file|*.ptp";
                if (dlg.ShowDialog() == true)
                    fileToSave.FilePath = dlg.FileName;
                else
                {
                    return;
                }
            }

            if (fileToSave.FilePath != null)
            {
                using (XmlTextWriter tw = new XmlTextWriter(fileToSave.FilePath, Encoding.UTF8))
                {
                    tw.Formatting = Formatting.Indented;
                    _projectSerializer.Serialize(tw, fileToSave);

                    if (ActiveDocumentChanged != null)
                        ActiveDocumentChanged(this, new DocumentChangedEventArgs(_activeDocument, _activeDocument));
                }
                foreach (var t in fileToSave.TimeLines) { t.IsChanged = false; }
                fileToSave.IsDirty = false;
                fileToSave.FileName = "";
            }
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

        internal bool Close(FileViewModel fileToClose)
        {
            if (fileToClose.IsDirty)
            {
                var res = MessageBox.Show(string.Format((String)Application.Current.TryFindResource("Global_SaveChanges"), fileToClose.FileName),
                    Application.Current.MainWindow.Title, MessageBoxButton.YesNoCancel);
                if (res == MessageBoxResult.Cancel)
                    return false;
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

            return true;
        }

        #endregion 

        #region AdjustDefaultsCommand
        RelayCommand _adjustDefaultsCommand = null;
        public ICommand AdjustDefaultsCommand
        {
            get
            {
                if (_adjustDefaultsCommand == null)
                {
                    _adjustDefaultsCommand = new RelayCommand((p) => OnAdjustDefaults(p), (p) => CanAdjustDefaults(p));
                }

                return _adjustDefaultsCommand;
            }
        }

        private bool CanAdjustDefaults(object parameter)
        {
            return true;
        }

        private void OnAdjustDefaults(object parameter)
        {
            DefaultsWindow dw = new DefaultsWindow();
            dw.Owner = Application.Current.MainWindow;
            dw.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dw.ShowDialog();

            ImagesModel.RefreshImagesCollection(Settings.Default.LineHeight);

            foreach (var f in Files)
            {
                foreach (var t in f.TimeLines)
                {
                    t.DefaultTimeInterval = Settings.Default.DefaultDuration;
                }
            }
        }

        #endregion 

        #region Events

        protected static void RaiseApplicationSettingsChanged()
        {
            if (ApplicationSettingsChanged != null)
                ApplicationSettingsChanged(null, new EventArgs());
        }
        public static event EventHandler ApplicationSettingsChanged;

        #endregion
    }
}
