using System.Windows.Media;
using Common.ViewModel;
using System.Xml.Serialization;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Common.Imaging;
using Microsoft.Expression.Interactivity.Core;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace PixelTuner.ViewModel
{
    public class LedTile : ViewModelBase
    {
        #region Pixel markup

        [XmlIgnore]
        public CanvasElementViewModel Parent { get; set; } = null;

        int _ledNumber;
        /// <summary>
        /// Differ than Index because reflects actual LED index in physical matrix
        /// </summary>
        [XmlAttribute]
        public int LedNumber
        {
            get
            {
                return _ledNumber;
            }

            set
            {
                _ledNumber = value; RaisePropertyChanged("LedNumber");
            }
        }

        double _opacityVal = 0.75;
        [XmlIgnore]
        public double OpacityVal
        {
            get
            {
                return _opacityVal;
            }

            set
            {
                _opacityVal = value; RaisePropertyChanged("OpacityVal");
            }
        }

        [XmlAttribute]
        public int Index { get; set; } = 0;

        /// <summary>
        /// Y (row) position of the LED in abstract markup matrix
        /// </summary>
        [XmlIgnore]
        public int Y
        {
            get
            {
                return Index / Parent.ColumnsCount;
            }
        }

        /// <summary>
        /// X (column) position of the LED in abstract markup matrix
        /// </summary>
        [XmlIgnore]
        public int X
        {
            get
            {
                return Index % Parent.ColumnsCount;
            }
        }
        
        private bool _selected = false;
        [XmlAttribute]
        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                RaisePropertyChanged("Selected");
            }
        }

        #endregion

        public LedTile()
        {
        }

        public override object Clone()
        {
            var newObj = (LedTile)base.Clone();
            newObj._selected = _selected;
            newObj._ledNumber = _ledNumber;
            newObj.Index = Index;
            newObj.Parent = Parent;
            return newObj;
        }
    }

    public class CanvasElementViewModel : ViewModelBase, Common.Controls.ITimeLineMeta
    {
        public CanvasElementViewModel()
        {
            LedTiles = new ObservableCollection<LedTile>();
            //SelectedLeds = new ObservableCollection<LedTile>();
            SetSizeCommand = new ActionCommand(SetSize);
            OnDoubleClickCommand = new DelegateCommand(OnDoubleClick);
            CopyMarkupCommand = new ActionCommand(OnCopyMarkupCommand);
            PasteMarkupCommand = new RelayCommand(OnPasteMarkupCommand, CanPasteMarkup);
            LedTiles.CollectionChanged += LedTiles_CollectionChanged;
            Application.Current.Exit += (sender, args) => { _frameTimer?.Stop(); };
        }

        private void LedTiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e != null && e.NewItems != null)
                foreach (LedTile eNewItem in e.NewItems) { eNewItem.Parent = this; }
        }

        #region Properties

        int _tilesMargin = 2;
        [XmlAttribute]
        public int TilesMargin
        {
            get
            {
                return _tilesMargin;
            }

            set
            {
                _tilesMargin = value; RaisePropertyChanged("TilesMargin");
            }
        }

        [XmlArray]
        public ObservableCollection<LedTile> LedTiles
        {
            get;
            set;
        }

        //[XmlElement]
        //public ObservableCollection<LedTile> SelectedLeds
        //{
        //    get;
        //    set;
        //}

        [XmlIgnore]
        public List<System.Drawing.Point> OrderedLeds
        {
            get
            {
                var result = new List<System.Drawing.Point>();
                var ordered = LedTiles.OrderBy(o => o.LedNumber);
                foreach (var l in ordered)
                {
                    if (l.Selected) result.Add(new System.Drawing.Point(l.X, l.Y));
                }
                return result;
            }
        }

        int _rowsCount = 0;
        [XmlAttribute]
        public int RowsCount
        {
            get
            {
                return _rowsCount;
            }

            set
            {
                _rowsCount = value; RaisePropertyChanged("RowsCount");
            }
        }

        int _columnsCount = 0;
        [XmlAttribute]
        public int ColumnsCount
        {
            get
            {
                return _columnsCount;
            }

            set
            {
                _columnsCount = value; RaisePropertyChanged("ColumnsCount");
            }
        }

        Geometry _geometry;
        [XmlIgnore]
        public Geometry Geometry
        {
            get { return _geometry; }
            set { _geometry = value; RaisePropertyChanged("Geometry"); RaisePropertyChanged("CenterPoint"); }
        }

        string _path;
        [XmlAttribute]
        public string Path
        {
            get { return _path; }
            set { _path = value; RaisePropertyChanged("Path"); }
        }

        Brush _stroke = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        [XmlIgnore]
        public Brush Stroke
        {
            get { return _stroke; }
            set { _stroke = value; RaisePropertyChanged("Stroke"); }
        }

        Brush _fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        [XmlIgnore]
        public Brush Fill
        {
            get { return _fill; }
            private set { _fill = value; RaisePropertyChanged("Fill"); }
        }

        Transform _transform = new MatrixTransform();
        [XmlIgnore]
        public Transform Transform
        {
            get
            {
                if (_transform == null) _transform = new MatrixTransform();
                return _transform;
            }
            set { _transform = value; RaisePropertyChanged("Transform"); RaisePropertyChanged("PreviewTransform"); }
        }

        private int _xPlaybackOffset;
        [XmlIgnore]
        public int XPlaybackOffset
        {
            get { return _xPlaybackOffset; }
            set { _xPlaybackOffset = value; RaisePropertyChanged("XPlaybackOffset"); }
        }

        private int _yPlaybackOffset;
        [XmlIgnore]
        public int YPlaybackOffset
        {
            get { return _yPlaybackOffset; }
            set { _yPlaybackOffset = value; RaisePropertyChanged("YPlaybackOffset"); }
        }
        
        [XmlIgnore]
        public double ActualWidth { get; set; }

        [XmlIgnore]
        public double ActualHeight { get; set; }
        
        [XmlIgnore]
        public Transform PreviewTransform
        {
            get
            {
                return new MatrixTransform(
                        Transform.Value.M11,
                        Transform.Value.M12,
                        Transform.Value.M21,
                        Transform.Value.M22,
                        -Geometry.Bounds.Left,
                        -Geometry.Bounds.Top);
            }
        }

        [XmlIgnore]
        public Transform DragTransform
        {
            get
            {
                return new TranslateTransform(
                    -(_geometry.Bounds.Left + _geometry.Bounds.Width / 2),
                    -(_geometry.Bounds.Top + _geometry.Bounds.Height / 2));
                //return _transform; 
            }
            set { }
        }

        [XmlIgnore]
        public Size RenderSize { get; set; }

        #endregion
        
        #region Commands

        [XmlIgnore]
        public ICommand SetSizeCommand { get; private set; }

        [XmlIgnore]
        public ICommand OnDoubleClickCommand { get; private set; }

        private void OnDoubleClick(object arg)
        {
            var doc = RootViewModel.This.ActiveDocument;
            doc.PushUndo(true);

            var wnd = new CanvasElementWindow(this);
            wnd.Owner = Application.Current.MainWindow;
            wnd.ShowDialog();

            if (wnd.Applied)
            {
                doc.IsDirty = true;
            }
        }
        
        [XmlIgnore]
        public ICommand CopyMarkupCommand { get; private set; }

        private void OnCopyMarkupCommand(object arg)
        {
            CanvasViewModel.CopyMarkup(this);
        }
        
        [XmlIgnore]
        public ICommand PasteMarkupCommand { get; private set; }

        public bool CanPasteMarkup(object arg)
        {
            return CanvasViewModel.CanPasteMarkup();
        }

        private void OnPasteMarkupCommand(object arg)
        {
            CanvasViewModel.PasteMarkup(this);
        }

        #endregion

        private void SetSize(object arg)
        {
            var targetAmount = ColumnsCount * RowsCount;
            var difference = targetAmount - LedTiles.Count;
            if (difference > 0)
            {
                for (var i = 0; i < difference; i++) LedTiles.Add(new LedTile() {Parent = this, Index = LedTiles.Count});
            }
            else
            {
                while (difference < 0)
                {
                    LedTiles.RemoveAt(LedTiles.Count + difference);
                    difference++;
                }
            }
        }

        /// <summary>
        /// Builds an editable markup chain from the stored tiles. The chain keeps the order
        /// the export uses (OrderedLeds); gaps and repeats of stored numbers go to <paramref name="issues"/>.
        /// </summary>
        public LedMarkup CreateMarkup(out LedMarkupIssues issues)
        {
            var pairs = LedTiles
                .Where(t => t.Selected)
                .Select(t => new KeyValuePair<int, int>(t.Index, t.LedNumber));
            return LedMarkup.FromNumbers(ColumnsCount, RowsCount, pairs, out issues);
        }

        /// <summary>Writes the markup back: one tile per grid cell, numbers 1..N.</summary>
        public void ApplyMarkup(LedMarkup markup)
        {
            ColumnsCount = markup.Columns;
            RowsCount = markup.Rows;
            var count = markup.CellCount;
            while (LedTiles.Count > count) LedTiles.RemoveAt(LedTiles.Count - 1);
            while (LedTiles.Count < count) LedTiles.Add(new LedTile() { Parent = this, Index = LedTiles.Count });
            for (var i = 0; i < count; i++)
            {
                var tile = LedTiles[i];
                var number = markup.GetNumber(i);
                tile.Parent = this;
                tile.Index = i;
                tile.LedNumber = number;
                tile.Selected = number > 0;
            }
            RaisePropertyChanged("OrderedLeds");
        }

        /// <summary>Deep copy of the tiles, so undo snapshots and pasted markup don't share them.</summary>
        public static ObservableCollection<LedTile> CloneTiles(IEnumerable<LedTile> tiles, CanvasElementViewModel parent)
        {
            var result = new ObservableCollection<LedTile>();
            foreach (var t in tiles)
            {
                var copy = (LedTile)t.Clone();
                copy.Parent = parent;
                result.Add(copy);
            }
            return result;
        }

        public void SetFill(Brush fill)
        {
            Fill = fill;
        }

        private Uri _currentUri = null;
        private int _currentFrameIdx = 0;
        private List<BitmapFrame> _frames = null;
        private System.Timers.Timer _frameTimer;
        private int _xOffset, _yOffset;

        public void SetFill(ImageSource image, int xOffset, int yOffset)
        {
            if (image == null)
            {
                if (_frameTimer != null)
                {
                    _frameTimer.Stop();
                    _frameTimer = null;
                }
            }
            _xOffset = xOffset;
            _yOffset = yOffset;

            var bImage = image as BitmapImage;
            if (bImage == null)
            {
                if (_frameTimer != null)
                {
                    _frameTimer.Stop();
                    _frameTimer = null;
                }
                return;
            }
            if (bImage.UriSource == _currentUri) return;

            _currentUri = bImage.UriSource;
            _currentFrameIdx = 0;
            if (_frameTimer != null)
            {
                _frameTimer.Stop();
                _frameTimer = null;
            }

            BitmapDecoder bd = new GifBitmapDecoder(_currentUri, BitmapCreateOptions.None, BitmapCacheOption.Default);
            _frames = new List<BitmapFrame>(bd.Frames);
            _frameTimer = new Timer(120);
            _frameTimer.Elapsed += FrameUpdater;
            _frameTimer.Start();
        }

        private void FrameUpdater(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    var image = _frames[_currentFrameIdx];
                    var xScale = image.Width / ColumnsCount;
                    var yScale = image.Height / RowsCount;

                    Fill = new ImageBrush(image)
                    {
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top,
                        RelativeTransform = new ScaleTransform(xScale, yScale),
                        Transform = new TranslateTransform(-_xOffset * (ActualWidth / ColumnsCount), -_yOffset * (ActualHeight / RowsCount))
                    };

                    _currentFrameIdx = (_currentFrameIdx + 3) % _frames.Count;
                }));
            }
            catch (Exception ex)
            {
                
            }
            
        }

        public override object Clone()
        {
            var newObj = (CanvasElementViewModel)base.Clone();
            newObj._fill = _fill;
            newObj._geometry = _geometry;
            newObj._path = _path;
            newObj._stroke = _stroke;
            newObj._transform = _transform;
            newObj._columnsCount = _columnsCount;
            newObj._rowsCount = _rowsCount;
            newObj._tilesMargin = _tilesMargin;
            newObj.LedTiles = CloneTiles(LedTiles, newObj);
            return newObj;
        }
    }
}
