using System.Windows.Media;
using Common.ViewModel;
using System.Xml.Serialization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using Microsoft.Expression.Interactivity.Core;

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
        [XmlAttribute]
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

        [XmlAttribute]
        public bool Selected { get; set; } = false;

        #endregion

        public LedTile()
        {
            TurnOnCommand = new DelegateCommand(OnTurnedOn);
        }

        [XmlIgnore]
        public ICommand TurnOnCommand { get; private set; }

        private void OnTurnedOn(object arg)
        {
            if (Selected)
            {
                Selected = false;
                LedNumber = 0;
            }
            else
            {
                var e = RootViewModel.This.CanvasModel.GetHostElement(this);
                LedNumber = e.GetLastNumber();
                Selected = true;
            }
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

        [XmlElement]
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
        public List<Point> OrderedLeds
        {
            get
            {
                List<Point> result = new List<Point>();
                foreach (var l in LedTiles)
                {
                    if (l.Selected) result.Add(new Point(l.X, l.Y));
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

        Brush _fill = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        [XmlIgnore]
        public Brush Fill
        {
            get { return _fill; }
            set { _fill = value; RaisePropertyChanged("Fill"); }
        }

        Transform _transform;
        [XmlIgnore]
        public Transform Transform
        {
            get { return _transform; }
            set { _transform = value; RaisePropertyChanged("Transform"); RaisePropertyChanged("PreviewTransform"); }
        }

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

        #endregion
        
        #region Commands

        [XmlIgnore]
        public ICommand SetSizeCommand { get; private set; }

        [XmlIgnore]
        public ICommand OnDoubleClickCommand { get; private set; }

        private void OnDoubleClick(object arg)
        {
            var wnd = new CanvasElementWindow();
            wnd.CanvasElement = this;
            wnd.Owner = Application.Current.MainWindow;
            wnd.ShowDialog();
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

        public int GetLastNumber()
        {
            int idx = 0;
            foreach (var ledTile in LedTiles)
            {
                if (ledTile.Selected) idx++;
            }
            return idx;
        }

        public override object Clone()
        {
            var newObj = (CanvasElementViewModel)base.Clone();
            newObj._fill = this._fill;
            newObj._geometry = this._geometry;
            newObj._path = this._path;
            newObj._stroke = this._stroke;
            newObj._transform = this._transform;
            return newObj;
        }
    }
}
