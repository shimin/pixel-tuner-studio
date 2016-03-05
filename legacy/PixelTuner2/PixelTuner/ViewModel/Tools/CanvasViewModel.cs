using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Media;

using PixelTuner.Auxiliary;

using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;


namespace PixelTuner.ViewModel
{
    public class CanvasViewModel : ToolViewModel
    {
        #region Properties

        FileSvgReader _converter = null;
        WpfDrawingSettings _settings = null;

        private string _sourceDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "sources");
        public string SourceDir
        {
            get { return _sourceDir; }
        }

        DrawingGroup _loadedView = null;
        public DrawingGroup LoadedView
        {
            get { return _loadedView; }
            set { _loadedView = value; RaisePropertyChanged("LoadedView"); }
        }

        Uri _loadedViewUri = null;
        public Uri LoadedViewUri
        {
            get { return _loadedViewUri; }
            set { _loadedViewUri = value; RaisePropertyChanged("LoadedViewUri"); }
        }

        private Geometry _pageSize;
        public Geometry PageSize
        {
            get { return _pageSize; }
            set { _pageSize = value; RaisePropertyChanged("PageSize"); }
        }

        //private ObservableCollection<CanvasElementViewModel> _elements = new ObservableCollection<CanvasElementViewModel>();
        //public ObservableCollection<CanvasElementViewModel> Elements
        //{
        //    get { return _elements; }
        //    set { _elements = value; }
        //}

        public IEnumerable<CanvasElementViewModel> Elements
        {
            get { return ElementsDictionary.Values; }
        }

        private Dictionary<string, CanvasElementViewModel> _elementsDictionary = new Dictionary<string, CanvasElementViewModel>();
        public Dictionary<string, CanvasElementViewModel> ElementsDictionary
        {
            get { return _elementsDictionary; }
            set { _elementsDictionary = value; }
        }

        #endregion

        public CanvasViewModel()
            : base("Canvas")
        {
            RootViewModel.This.ActiveDocumentChanged += new RootViewModel.DocumentChangedEventHandler(OnActiveDocumentChanged);
            ContentId = ToolContentId;

            _settings = new WpfDrawingSettings();
            _settings.CultureInfo = _settings.NeutralCultureInfo;
        }

        public const string ToolContentId = "CanvasTool";

        void OnActiveDocumentChanged(object sender, EventArgs e)
        {
            if (RootViewModel.This.ActiveDocument != null &&
                RootViewModel.This.ActiveDocument.FilePath != null &&
                File.Exists(RootViewModel.This.ActiveDocument.FilePath))
            {
                
            }
            else
            {
                
            }
        }

        internal void LoadContent()
        {
            _converter = new FileSvgReader(_settings);
            string fileToLoad = Path.Combine(_sourceDir, "human.svg");

            if (File.Exists(fileToLoad))
            {
                LoadedView = _converter.Read(fileToLoad);
                if (LoadedView != null)
                {
                    var layers = SvgHelper.Find<DrawingGroup>(
                        (obj) => { 
                            return (obj.GetValue(System.Windows.FrameworkElement.NameProperty) as string) == "DrawingLayer"; 
                        }, LoadedView);

                    PageSize = layers[0].ClipGeometry;

                    foreach (var d in layers[0].Children)
                    {
                        var group = d as DrawingGroup;
                        if (d != null)
                        {
                            ParseGroup(group, group.Transform);
                        }
                    }
                }
            }
        }

        private void ParseGroup(DrawingGroup drawingGroup, Transform transform)
        {
            foreach (var e in drawingGroup.Children)
            {
                var group = e as DrawingGroup;
                if (group != null) ParseGroup(group, group.Transform ?? transform);
                else
                {
                    CanvasElementViewModel newElementModel = new CanvasElementViewModel();
                    newElementModel.Geometry = (e as GeometryDrawing).Geometry;
                    newElementModel.Transform = transform;
                    newElementModel.Path = e.GetValue(System.Windows.FrameworkElement.NameProperty) as string;
                    newElementModel.Fill = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    if (!ElementsDictionary.ContainsKey(newElementModel.Path))
                    {
                        ElementsDictionary.Add(newElementModel.Path, newElementModel);
                    }
                }
            }
        }

    }
}
