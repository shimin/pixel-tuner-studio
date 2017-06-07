using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;

using Common.ViewModel;
using PixelTuner.Properties;
using Common.Log;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PixelTuner.ViewModel
{
    public static class ExtensionMethods
    {

        private static Action EmptyDelegate = delegate () { };


        public static void Refresh(this UIElement uiElement)
        {
            uiElement.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, EmptyDelegate);
        }
    }

    public class RootViewModel : ViewModelBase
    {
#if DEBUG
        public static Logger Logger = null;
#endif
        internal delegate void LastUsedFileChangedDelegate(string path);

        private UdpClient _udpСlient = null;
        public UdpClient UdpСlient
        {
            get { return _udpСlient; }
            set { _udpСlient = value; }
        }

        private LastUsedFileChangedDelegate _lastUsedFileHandler;
        internal LastUsedFileChangedDelegate LastUsedFileHandler
        {
            get { return _lastUsedFileHandler; }
            set { _lastUsedFileHandler = value; }
        }

        public RootViewModel()
        {
            this.Id = Guid.NewGuid();

            GenerateColors();

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

        private void GenerateColors()
        {
            byte step = 50;
            for (byte i = 250; i > 0; i -= 5)
            {
                _colorsMono.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(i, i, i), string.Format("{0}, {0}, {0}", i)));
            }

            for (byte i = 250; i > 0; i -= 50)
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(i, i, i), string.Format("{0}, {0}, {0}", i)));
            }
            
            for (byte r = 250; r > 0; r -= step) // red
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(r, 0, 0), string.Format("{0}, {1}, {2}", r, 0, 0)));
            }

            for (byte c = 250; c > 0; c -= step) // orange
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(c, (byte)(c / 2), 0), string.Format("{0}, {1}, {2}", c, c / 2, 0)));
            }

            for (byte c = 250; c > 0; c -= step) // yellow
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(c, c, 0), string.Format("{0}, {1}, {2}", c, c, 0)));
            }

            for (byte c = 250; c > 0; c -= step) // yellow
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb((byte)(c / 2), c, 0), string.Format("{0}, {1}, {2}", c / 2, c, 0)));
            }

            for (byte g = 250; g > 0; g -= step) // green
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, g, 0), string.Format("{0}, {1}, {2}", 0, g, 0)));
            }

            for (byte c = 250; c > 0; c -= step) // cyan
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, c, (byte)(c / 2)), string.Format("{0}, {1}, {2}", 0, c, c / 2)));
            }

            for (byte c = 250; c > 0; c -= step) // cyan
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, c, c), string.Format("{0}, {1}, {2}", 0, c, c)));
            }

            for (byte c = 250; c > 0; c -= step) // cyan
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, (byte)(c / 2), c), string.Format("{0}, {1}, {2}", 0, c / 2, c)));
            }

            for (byte b = 250; b > 0; b -= step) // blue
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, 0, b), string.Format("{0}, {1}, {2}", 0, 0, b)));
            }

            for (byte c = 250; c > 0; c -= step) // cyan
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb((byte)(c / 2), 0, c), string.Format("{0}, {1}, {2}", c / 2, 0, c)));
            }

            for (byte c = 250; c > 0; c -= step) // purple
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(c, 0, c), string.Format("{0}, {1}, {2}", c, 0, c)));
            }
            
            for (byte c = 250; c > 0; c -= step) // cyan
            {
                _colorsRGB.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(c, 0, (byte)(c / 2)), string.Format("{0}, {1}, {2}", c, 0, c / 2)));
            }

            for (byte c = 0; c < 250; c += 25) 
                _colorsMonoShort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(c, c, c), string.Format("{0}, {0}, {0}", c)));
            _colorsMonoShort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(255, 255, 255), "White"));

            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, 0, 0), "Black"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(125, 125, 125), "Gray"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(255, 255, 255), "White"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(255, 0, 0), "Red"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(255, 125, 0), "Orange"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(255, 255, 0), "Yellow"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, 255, 0), "Green"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, 255, 255), "Cyan"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(0, 0, 255), "Blue"));
            _colorsRGBshort.Add(new Toolkit.ColorItem(System.Windows.Media.Color.FromRgb(255, 0, 255), "Magenta"));
        }

        void Workspace_ActiveDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            if (_activeDocument == null)
            {
                ImagesModel.RefreshImagesCollection(ImagesModel.LineHeight);
                //VideosModel.RefreshVideosCollection(Settings.Default.LineHeight);
                CanvasModel = null;
                GradientsModel.RefreshImagesCollection();
            }
            else
            {
                ImagesModel.RefreshImagesCollection(ImagesModel.LineHeight);
                GradientsModel.RefreshImagesCollection();
                CanvasModel = ActiveDocument.Canvas;
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

        private FixMissingWindow _fixMissingWnd = null;

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

        private bool _isOpenGLAvailable = false;
        public bool IsOpenGLAvailable
        {
            get
            {
                return _isOpenGLAvailable;
            }

            set
            {
                _isOpenGLAvailable = value; RaisePropertyChanged("IsOpenGLAvailable");
            }
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

        private static ObservableCollection<Toolkit.ColorItem> _colorsRGB = new ObservableCollection<Toolkit.ColorItem>();
        public static ObservableCollection<Toolkit.ColorItem> ColorsRgb
        {
            get
            {
                return _colorsRGB;
            }
            set
            {
                _colorsRGB = value;
            }
        }

        private static ObservableCollection<Toolkit.ColorItem> _colorsRGBshort = new ObservableCollection<Toolkit.ColorItem>();
        public static ObservableCollection<Toolkit.ColorItem> ColorsRgbShort
        {
            get
            {
                return _colorsRGBshort;
            }
            set
            {
                _colorsRGBshort = value;
            }
        }

        private static ObservableCollection<Toolkit.ColorItem> _colorsMono = new ObservableCollection<Toolkit.ColorItem>();
        public static ObservableCollection<Toolkit.ColorItem> ColorsMono
        {
            get
            {
                return _colorsMono;
            }
            set
            {
                _colorsMono = value;
            }
        }

        private static ObservableCollection<Toolkit.ColorItem> _colorsMonoShort = new ObservableCollection<Toolkit.ColorItem>();
        public static ObservableCollection<Toolkit.ColorItem> ColorsMonoShort
        {
            get
            {
                return _colorsMonoShort;
            }
            set
            {
                _colorsMonoShort = value;
            }
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
                    _tools = new ToolViewModel[] {
                        CanvasModel, 
                        VideosModel,
                        GradientsModel,
                        DevicesModel,
                        ProjectsModel,
                        ImagesModel,
                        ProjectPlayerModel,
                        EffectsModel
                    };
                return _tools;
            }
        }

        CanvasViewModel _canvasModel = null;
        public CanvasViewModel CanvasModel
        {
            get
            {
                if (_canvasModel == null)
                {
                    _canvasModel =
                        new CanvasViewModel((string) Application.Current.TryFindResource("Menu_Window_Canvas"));
                    _canvasModel.LoadContent();
                }

                return _canvasModel;
            }
            set { _canvasModel = value; RaisePropertyChanged("CanvasModel"); }
        }

        //private WifiPlayerDevice _wifiPlayer = null;
        //public WifiPlayerDevice WifiPlayer
        //{
        //    get
        //    {
        //        if (_wifiPlayer == null) _wifiPlayer = new WifiPlayerDevice();
        //        return _wifiPlayer;
        //    }
        //    set { _wifiPlayer = value; }
        //}


        DevicesViewModel _devicesModel = null;
        public DevicesViewModel DevicesModel
        {
            get
            {
                if (_devicesModel == null)
                    _devicesModel = new DevicesViewModel(
                        (string)Application.Current.TryFindResource("Menu_Window_Devices"));

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
                    ImagesModel.RefreshImagesCollection(ImagesModel.LineHeight, itemToRemove.FullName);
                    RemoveBrokenReferences();
                }
                catch (Exception ex)
                {
                    ImagesModel.RefreshImagesCollection(ImagesModel.LineHeight);
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
                    if (timeline is LightTimeLineViewModel) continue;

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

        #region Videos model

        VideosViewModel _videosModel = null;
        public VideosViewModel VideosModel
        {
            get
            {
                if (_videosModel == null)
                {
                    _videosModel = new VideosViewModel(
                        (String)Application.Current.TryFindResource("Menu_Window_Videos"));
                }

                return _videosModel;
            }
        }

        #endregion
        
        #region Effects model

        EffectsViewModel _effectsModel = null;
        public EffectsViewModel EffectsModel
        {
            get
            {
                if (_effectsModel == null)
                {
                    _effectsModel = new EffectsViewModel(
                        (String)Application.Current.TryFindResource("Menu_Window_Effects"));
                }

                return _effectsModel;
            }
        }

        #endregion

        #region Gradients model

        GradientsViewModel _gradientsModel = null;
        public GradientsViewModel GradientsModel
        {
            get
            {
                if (_gradientsModel == null)
                {
                    _gradientsModel = new GradientsViewModel(
                        (String)Application.Current.TryFindResource("Menu_Window_Gradients"));
                }

                return _gradientsModel;
            }
        }

        #endregion
        
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
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        // replace xsi:type to correctly open old projects
                        TextReader str = new StreamReader(fs);
                        var allText = str.ReadToEnd();
                        allText = allText.Replace("<Line>", "<Line xsi:type=\"PixelTimeLineViewModel\">");

                        TextReader textReader = new StringReader(allText);
                        using (XmlTextReader tr = new XmlTextReader(textReader))
                        {
                            try
                            {
                                var file = (FileViewModel) _projectSerializer.Deserialize(tr);
                                file.IsDirty = false;
                                FixMissingLinks(file);
                                file.FilePath = path;
                                _files.Add(file);
                                if (!string.IsNullOrEmpty(file.AudioPath)) file.OpenAudio.Execute(file.AudioPath);
                                ActiveDocument = file;
                                LastUsedFileHandler?.Invoke(path);
                                IgnoreAllMissingLinks(file);
                            }
                            catch (OperationCanceledException ex)
                            {
                                MessageBox.Show(ex.Message, Application.Current.MainWindow.Title, MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            catch (Exception ex)
                            {
                                ResourceDictionary rd = Application.Current.Resources.MergedDictionaries[0];
                                string msg = (String)rd["Doc_Open_DeserializeError"];
                                MessageBox.Show(msg + "\n" + ex.Message, Application.Current.MainWindow.Title, MessageBoxButton.OK, 
                                    MessageBoxImage.Error);
                            }
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

        private void FixMissingLinks(FileViewModel file)
        {
            var brokenItem = GetBrokenLink(file);
            while (brokenItem != null)
            {
                file.IsDirty = true;
                var line = TimelineHelper.FindParentTimeline(file, brokenItem);
                var wnd = new FixMissingWindow(
                    () => //ReplaceAllMissingLinks,
                    {
                        var dialog = new FolderBrowserDialog();
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            ReplaceAllMissingLinks(file, dialog.SelectedPath);
                        }
                    },
                    () => //SetOtherImageFromCollection,
                    {
                        var targetHeight = line.TimelineHeightPx != 0 ? line.TimelineHeightPx : Settings.Default.LineHeight;
                        IReplaceItem w = brokenItem.OpenReplaceWindow();
                        w.Height = targetHeight;
                        if (brokenItem is VideoThumbnailViewModel) w = new ReplaceVideoItemWindow(VideosModel);
                        if (((Window)w).ShowDialog() == true)
                        {
                            TimeLineItemViewModel newItem = w.SelectedItem as TimeLineItemViewModel;
                            if (newItem.ActualHeight == targetHeight || brokenItem.GetType() == typeof(VideoThumbnailViewModel))
                            {
                                brokenItem.Replace(newItem.FullName);
                            }
                            else
                            {
                                MessageBox.Show(
                                    (string) Application.Current.TryFindResource("FixMissingWindow_HeightMismatch"),
                                    "PixelTuner",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    },
                    () => //SetOtherImageFromDrive,
                    {
                        var ofd = new OpenFileDialog {Filter = "Images|*.bmp;*.jpg;*.jpeg;*.png"};
                        if (brokenItem is VideoThumbnailViewModel) ofd.Filter = "Clips|*.avi;*.gif;";
                        if (ofd.ShowDialog() == true)
                        {
                            var b = Bitmap.FromFile(ofd.FileName);
                            var targetHeight = line.TimelineHeightPx != 0 ? line.TimelineHeightPx : Settings.Default.LineHeight;

                            if (b.Height == targetHeight || brokenItem.GetType() == typeof(VideoThumbnailViewModel))
                            {
                                brokenItem.Replace(ofd.FileName);
                            }
                            else
                            {
                                MessageBox.Show(
                                    (string) Application.Current.TryFindResource("FixMissingWindow_HeightMismatch"),
                                    "PixelTuner",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    },
                    () => //SetFileToIgnore,
                    {
                        brokenItem.ReplaceWithBlank(Settings.Default.LineHeight);
                    },
                    () => //SetAllFilesToIgnore);
                    {
                        IgnoreAllMissingLinks(file);
                    })
                {
                    MissingPath = brokenItem.FullName,
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (wnd.ShowDialog() == true)
                {
                    wnd.SelectedAction.Invoke();
                }
                else
                {
                    throw new OperationCanceledException("Operation cancelled.");
                }

                brokenItem = GetBrokenLink(file);
            }
        }

        private void ReplaceAllMissingLinks(FileViewModel file, string dialogSelectedPath)
        {
            foreach (var timeLine in file.TimeLines)
            {
                if (timeLine.GetType() != typeof(LightTimeLineViewModel))
                    for (int i = 0; i < timeLine.Items.Count; i++)
                    {
                        var item = timeLine.Items[i] as TimeLineItemViewModel;
                        item.Replace(Path.Combine(dialogSelectedPath, item.Name));
                    }
            }
        }
        
        private void IgnoreAllMissingLinks(FileViewModel file)
        {
            foreach (var timeLine in file.TimeLines)
            {
                if (timeLine.GetType() != typeof(LightTimeLineViewModel))
                    for (int i = 0; i < timeLine.Items.Count; i++)
                    {
                        var item = (timeLine.Items[i] as TimeLineItemViewModel);
                        if (!File.Exists(item.FullName))
                            if (item is VideoThumbnailViewModel)
                            {
                                (item as VideoThumbnailViewModel).ReplaceWithBlank(Settings.Default.LineHeight);
                            }
                            else
                            {
                                item.ReplaceWithBlank(Settings.Default.LineHeight);
                            }
                    }
            }
        }

        private TimeLineItemViewModel GetBrokenLink(FileViewModel file)
        {
            foreach (var timeLine in file.TimeLines)
            {
                if (timeLine.GetType() != typeof(LightTimeLineViewModel))
                    foreach (TimeLineItemViewModel lineItem in timeLine.Items)
                        if (!File.Exists(lineItem.FullName)) { return lineItem; }
            }
            return null;
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
            model.TimeLines.Add(new PixelTimeLineViewModel());
            model.Canvas = new CanvasViewModel();
            model.Canvas.LoadContent();
            _files.Add(model);
            ActiveDocument = _files.Last();
        }

        #endregion

        #region ImportCommand
        RelayCommand _importCommand = null;
        public System.Windows.Input.ICommand ImportCommand
        {
            get
            {
                if (_importCommand == null)
                {
                    _importCommand = new RelayCommand((p) => OnImport(p), (p) => CanImport(p));
                }

                return _importCommand;
            }
        }

        private bool CanImport(object parameter)
        {
            return true;
        }

        private void OnImport(object parameter)
        {
            CanvasViewModel c = new CanvasViewModel();

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = c.SourceDir;
            ofd.Filter = "SVG files|*.svg";
            if (ofd.ShowDialog() == true)
            {
                c.LoadContent(ofd.FileName);
                CanvasModel = c;
                if (ActiveDocument != null) ActiveDocument.Canvas = c;
            }
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
                {
                    fileToSave.FilePath = dlg.FileName;
                    LastUsedFileHandler?.Invoke(dlg.FileName);
                }
                else
                {
                    return;
                }
            }

            if (fileToSave.FilePath != null)
            {
                ValidateFile(fileToSave);
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

        private void ValidateFile(FileViewModel fileToSave)
        {
            foreach (var line in fileToSave.TimeLines)
            {
                for (int i = 0; i < line.Items.Count - 1; i++)
                {
                    if (line.Items[i].EndTime > line.Items[i + 1].StartTime)
                        line.Items[i].EndTime = line.Items[i + 1].StartTime;
                }
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

        #region ExportCommand
#if DEBUG
        RelayCommand _exportCommand = null;
        public ICommand ExportCommand
        {
            get
            {
                if (_exportCommand == null)
                {
                    _exportCommand = new RelayCommand((p) => OnExport(p), (p) => CanExport(p));
                }

                return _exportCommand;
            }
        }

        private bool CanExport(object parameter)
        {
            if (ActiveDocument == null) return false;

            bool canExport = (ActiveDocument.TimeLines.Count((i) => 
                PixelDeviceViewModel.TimelineFilter(i) && (i as PixelTimeLineViewModel).LineHeight == Settings.Default.LineHeight) > 0)
                || (ActiveDocument.TimeLines.Count(LightMasterDeviceViewModel.TimelineFilter) > 0)
                || (ActiveDocument.TimeLines.Count(EpicPoiDeviceViewModel.TimelineFilter) > 0)
                || (ActiveDocument.TimeLines.Count(VideoDeviceViewModel.TimelineFilter) > 0);
            return canExport;
        }

        private void OnExport(object parameter)
        {
            Export(ActiveDocument);
        }

        internal void Export(FileViewModel fileToSave, bool saveAsFlag = false)
        {
            TimelinesToUpload dialog = null;

            if (fileToSave.TimeLines.Count((i) => 
                PixelDeviceViewModel.TimelineFilter(i) && (i as PixelTimeLineViewModel).LineHeight == Settings.Default.LineHeight) > 0)
            {
                dialog = new TimelinesToUpload((i) => 
                    PixelDeviceViewModel.TimelineFilter(i) && (i as PixelTimeLineViewModel).LineHeight == Settings.Default.LineHeight, RootViewModel.This.ActiveDocument);

                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    var dlg = new SaveFileDialog();
                    dlg.Filter = "Binary project file|*.bin";
                    if (dlg.ShowDialog().GetValueOrDefault())
                    {
                        File.WriteAllBytes(dlg.FileName,
                            fileToSave.GetPixelBinaryPresentation(dialog.SelectedTimeLines, (byte) Settings.Default.LineHeight).ToArray());
                    }
                }
            }

            if (fileToSave.TimeLines.Count(LightMasterDeviceViewModel.TimelineFilter) > 0)
            {
                dialog = new TimelinesToUpload(LightMasterDeviceViewModel.TimelineFilter, RootViewModel.This.ActiveDocument);
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    var dlg = new SaveFileDialog();
                    dlg.Filter = "Binary project file|*.bin";
                    if (dlg.ShowDialog().GetValueOrDefault())
                    {
                        File.WriteAllBytes(dlg.FileName,
                            fileToSave.GetLightBinaryPresentation(dialog.SelectedTimeLines).ToArray());
                    }
                }
            }

            if (fileToSave.TimeLines.Count(EpicPoiDeviceViewModel.TimelineFilter) > 0)
            {
                dialog = new TimelinesToUpload(EpicPoiDeviceViewModel.TimelineFilter, RootViewModel.This.ActiveDocument);
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    var dlg = new SaveFileDialog();
                    dlg.Filter = "Binary project file|*.bin";
                    if (dlg.ShowDialog().GetValueOrDefault())
                    {
                        File.WriteAllBytes(dlg.FileName,
                            fileToSave.GetEpicBinaryPresentation(dialog.SelectedTimeLines).ToArray());
                    }
                }
            }

            if (fileToSave.TimeLines.Count(VideoDeviceViewModel.TimelineFilter) > 0)
            {
                dialog = new TimelinesToUpload(VideoDeviceViewModel.TimelineFilter, RootViewModel.This.ActiveDocument);
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    var dlg = new SaveFileDialog();
                    dlg.Filter = "Binary project file|*.bin";
                    if (dlg.ShowDialog().GetValueOrDefault())
                    {
                        //var line = dialog.SelectedTimeLines.First();
                        //if (line != null)
                        var bin = fileToSave.GetVideoBinaryPresentation(dialog.SelectedTimeLines).ToArray();
                        File.WriteAllBytes(dlg.FileName, bin);
                    }
                }
            }

        }

        public Visibility ExportCommandVisibility
        {
            get
            {
                return Visibility.Visible;
            }
        }
#else
        public Visibility ExportCommandVisibility
        {
            get
            {
                return Visibility.Collapsed;
            }
        }
#endif


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
            try
            {
                if (fileToClose.IsDirty)
                {
                    var res =
                        MessageBox.Show(
                            string.Format((String) Application.Current.TryFindResource("Global_SaveChanges"),
                                fileToClose.FileName),
                            Application.Current.MainWindow.Title, MessageBoxButton.YesNoCancel);
                    if (res == MessageBoxResult.Cancel)
                        return false;
                    if (res == MessageBoxResult.Yes)
                    {
                        Save(fileToClose);
                    }
                }

                FileViewModel oldDoc = _activeDocument;
                if (fileToClose.DisconnectCommand.CanExecute(null)) fileToClose.DisconnectCommand.Execute(null);
                if (fileToClose.StopAudio.CanExecute(null)) fileToClose.StopAudio.Execute(null);
                _files.Remove(fileToClose);
                _activeDocument = _files.Count > 0 ? _files.Last() : null;
                RaisePropertyChanged("ActiveDocument");
                if (ActiveDocumentChanged != null)
                    ActiveDocumentChanged(this, new DocumentChangedEventArgs(oldDoc, _activeDocument));
            }
            catch
            {
            }
            return true;
        }

        #endregion 

        #region ArtnetCommand
        RelayCommand _artnetCommand = null;
        public ICommand ArtnetCommand
        {
            get
            {
                if (_artnetCommand == null)
                {
                    _artnetCommand = new RelayCommand((p) => OnArtnetCommand(p), (p) => CanArtnetCommand(p));
                }

                return _artnetCommand;
            }
        }

        private bool CanArtnetCommand(object parameter)
        {
            return true;
        }

        private void OnArtnetCommand(object parameter)
        {
            ArtNetWindow anw = new ArtNetWindow();
            anw.Owner = Application.Current.MainWindow;
            anw.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            anw.ShowDialog();
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

            ImagesModel.RefreshImagesCollection(ImagesModel.LineHeight);
            GradientsModel.RefreshImagesCollection();

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
