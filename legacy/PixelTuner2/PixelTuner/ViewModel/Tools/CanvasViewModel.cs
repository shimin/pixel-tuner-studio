using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Xml.Serialization;
using Microsoft.Win32;
using PixelTuner.Auxiliary;
using PixelTuner.Properties;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;


namespace PixelTuner.ViewModel
{
    public class CanvasViewModel : ToolViewModel
    {
        #region Properties
        
        private FileSvgReader _converter = null;
        private WpfDrawingSettings _settings = null;

        private static string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        private string _sourceDir = Path.Combine(picturesPath, "PixelTuner 2\\Schemas");

        [XmlIgnore]
        public const string ToolContentId = "CanvasTool";

        [XmlIgnore]
        public string SourceDir
        {
            get { return _sourceDir; }
        }

        DrawingGroup _loadedView = null;
        [XmlIgnore]
        public DrawingGroup LoadedView
        {
            get { return _loadedView; }
            set { _loadedView = value; RaisePropertyChanged("LoadedView"); }
        }
        
        private Geometry _pageSize;
        [XmlIgnore]
        public Geometry PageSize
        {
            get { return _pageSize; }
            set { _pageSize = value; RaisePropertyChanged("PageSize"); }
        }

        private ObservableCollection<CanvasElementViewModel> _elements = new ObservableCollection<CanvasElementViewModel>();
        [XmlElement]
        public ObservableCollection<CanvasElementViewModel> Elements
        {
            get { return _elements; }
            set { _elements = value; RaisePropertyChanged("Elements"); }
        }

        private string _content = "";
        [XmlElement]
        public string Content
        {
            get { return _content; }
            set
            {
                _content = value;
                using (MemoryStream stream = new MemoryStream())
                {
                    StreamWriter writer = new StreamWriter(stream);
                    writer.Write(value);
                    writer.Flush();
                    stream.Position = 0;
                    LoadContent(stream);
                }
            }
        }

        [XmlIgnore]
        public Dictionary<string, CanvasElementViewModel> ElementsDictionary { get; set; } = new Dictionary<string, CanvasElementViewModel>();

        #endregion

        public CanvasViewModel()
            : this("Canvas")
        {
            Elements.CollectionChanged += (sender, args) =>
            {
                foreach (CanvasElementViewModel item in args.NewItems)
                {
                    if (ElementsDictionary.ContainsKey(item.Path))
                    {
                        ElementsDictionary[item.Path] = item;
                    }
                    else
                    {
                        ElementsDictionary.Add(item.Path, item);
                    }
                }
            };
        }

        public CanvasViewModel(string name)
            : base(name)
        {
            IsVisible = true;
            ContentId = ToolContentId;

            _settings = new WpfDrawingSettings();
            _settings.CultureInfo = _settings.NeutralCultureInfo;
            RootViewModel.This.ActiveDocumentChanged += new RootViewModel.DocumentChangedEventHandler(OnActiveDocumentChanged);
        }

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

        /// <summary>
        /// Loads default schema file.
        /// </summary>
        internal void LoadContent()
        {
            string fileToLoad = Path.Combine(_sourceDir, "human.svg");

            if (!string.IsNullOrEmpty(Settings.Default.LastUsedSVG))
                fileToLoad = Settings.Default.LastUsedSVG;

            LoadContent(fileToLoad);
        }

        /// <summary>
        /// Loads specified SVG file.
        /// </summary>
        /// <param name="fileToLoad"></param>
        internal void LoadContent(string fileToLoad)
        {
            if (File.Exists(fileToLoad))
            {
                using (StreamReader sr = new StreamReader(fileToLoad))
                {
                    LoadContent(sr.BaseStream);
                    Settings.Default.LastUsedSVG = fileToLoad;
                }
            }
        }

        /// <summary>
        /// Parses text stream.
        /// </summary>
        /// <param name="svgStream"></param>
        internal void LoadContent(Stream svgStream)
        {
            //Elements.Clear();
            //ElementsDictionary.Clear();
            _converter = new FileSvgReader(_settings);
            LoadedView = _converter.Read(svgStream);
            if (LoadedView != null)
            {
                var layers = SvgHelper.Find<DrawingGroup>(
                    (obj) =>
                    {
                        return (obj.GetValue(System.Windows.FrameworkElement.NameProperty) as string) == "DrawingLayer";
                    },
                    LoadedView);

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
            svgStream.Position = 0;
            using (StreamReader reader = new StreamReader(svgStream, Encoding.UTF8))
            {
                _content = reader.ReadToEnd();
                svgStream.Position = 0;
            }

            RaisePropertyChanged("Elements");
        }
        
        private void ParseGroup(DrawingGroup drawingGroup, Transform transform)
        {
            foreach (var e in drawingGroup.Children)
            {
                var group = e as DrawingGroup;
                if (group != null) ParseGroup(group, group.Transform ?? transform);
                else
                {
                    CanvasElementViewModel newElement = new CanvasElementViewModel();
                    newElement.Geometry = (e as GeometryDrawing).Geometry;
                    newElement.Transform = transform;
                    newElement.Path = e.GetValue(System.Windows.FrameworkElement.NameProperty) as string;
                    newElement.Fill = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    if (!ElementsDictionary.ContainsKey(newElement.Path))
                    {
                        ElementsDictionary.Add(newElement.Path, newElement);
                        Elements.Add(newElement);
                    }
                    else
                    {
                        ElementsDictionary[newElement.Path].Geometry = newElement.Geometry;
                        ElementsDictionary[newElement.Path].Transform = newElement.Transform;
                        ElementsDictionary[newElement.Path].Fill = newElement.Fill;
                    }
                }
            }
        }

        public CanvasElementViewModel GetHostElement(LedTile led)
        {
            foreach (var element in Elements)
            {
                if (element.LedTiles.Contains(led)) return element;
            }
            return null;
        }
    }
}
