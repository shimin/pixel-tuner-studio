using System.Windows.Media;
using Common.ViewModel;
using System.Xml.Serialization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;

namespace PixelTuner.ViewModel
{
    public class LedTile : ViewModelBase
    {
        public LedTile(CanvasElementViewModel parentElement, int idx)
        {
            _parent = parentElement;
            _index = idx;
            TurnOnCommand = new DelegateCommand(OnTurnedOn);
        }

        int _index = 0;
        
        public int Y
        {
            get
            {
                return _index / _parent.ColumnsCount;
            }
        }
        
        public int X
        {
            get
            {
                return _index % _parent.ColumnsCount;
            }
        }

        CanvasElementViewModel _parent = null;

        bool _selected = false;

        int _ledNumber;
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

        [XmlIgnore]
        public ICommand TurnOnCommand { get; private set; }

        private void OnTurnedOn(object arg)
        {
            if (_parent.SelectedLeds.Contains(this))
            {
                _parent.SelectedLeds.Remove(this);
                LedNumber = 0;
            }
            else
            {
                LedNumber = _parent.SelectedLeds.Count;
                _parent.SelectedLeds.Add(this);
            }
        }
    }

    public class CanvasElementViewModel : ViewModelBase, Common.Controls.ITimeLineMeta
    {
        public CanvasElementViewModel()
        {
            LedTiles = new ObservableCollection<LedTile>();
            SelectedLeds = new ObservableCollection<LedTile>();
            OnDoubleClickCommand = new DelegateCommand(OnDoubleClick);
        }

        #region Pixel markup

        int _tilesMargin = 0;
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

        public ObservableCollection<LedTile> LedTiles
        {
            get;
            private set;
        }
        
        public ObservableCollection<LedTile> SelectedLeds
        {
            get;
            private set;
        }
        
        int _rowsCount = 0;
        public int RowsCount
        {
            get
            {
                return _rowsCount;
            }

            set
            {
                _rowsCount = value; UpdateLedTiles(); RaisePropertyChanged("RowsCount");
            }
        }

        int _columnsCount = 0;
        public int ColumnsCount
        {
            get
            {
                return _columnsCount;
            }

            set
            {
                _columnsCount = value; UpdateLedTiles(); RaisePropertyChanged("ColumnsCount");
            }
        }

        #endregion

        #region Properties

        Geometry _geometry;
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
        public Brush Stroke
        {
            get { return _stroke; }
            set { _stroke = value; RaisePropertyChanged("Stroke"); }
        }

        Brush _fill = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        public Brush Fill
        {
            get { return _fill; }
            set { _fill = value; RaisePropertyChanged("Fill"); }
        }

        Transform _transform;
        public Transform Transform
        {
            get { return _transform; }
            set { _transform = value; RaisePropertyChanged("Transform"); RaisePropertyChanged("PreviewTransform"); }
        }

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
        public ICommand OnDoubleClickCommand { get; private set; }

        public List<Point> OrderedLeds
        {
            get
            {
                List<Point> result = new List<Point>();
                foreach (var l in SelectedLeds)
                {
                    result.Add(new Point(l.X, l.Y));
                }
                return result;
            }
        }

        private void OnDoubleClick(object arg)
        {
            var wnd = new CanvasElementWindow();
            wnd.CanvasElement = this;
            wnd.Owner = Application.Current.MainWindow;
            wnd.ShowDialog();
        }

        #endregion

        private void UpdateLedTiles()
        {
            int targetAmount = ColumnsCount * RowsCount;
            int difference = targetAmount - LedTiles.Count;
            if (difference > 0)
            {
                for (int i = 0; i < difference; i++) LedTiles.Add(new LedTile(this, LedTiles.Count));
            } else
            {
                while (difference < 0)
                {
                    LedTiles.RemoveAt(LedTiles.Count + difference);
                    difference++;
                }
            }
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
