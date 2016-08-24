using System.Windows.Media;
using Common.ViewModel;
using System.Xml.Serialization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace PixelTuner.ViewModel
{
    public class CanvasElementViewModel : ViewModelBase, Common.Controls.ITimeLineMeta
    {
        public CanvasElementViewModel()
        {
            ColumnCollection = new ObservableCollection<DataGridColumn>();
            RowsData = new ObservableCollection<object>();
            OnDoubleClickCommand = new DelegateCommand(OnDoubleClick);
        }

        #region Pixel markup

        public ObservableCollection<DataGridColumn> ColumnCollection
        {
            get;
            private set;
        }

        public ObservableCollection<object> RowsData
        {
            get;
            private set;
        }

        int _rowsCount = 2;
        public int RowsCount
        {
            get
            {
                return _rowsCount;
            }

            set
            {
                RowsData.Clear();
                _rowsCount = value; RaisePropertyChanged("RowsCount");
                for (int i = 0; i < _rowsCount; i++) RowsData.Add(new object());
            }
        }

        int _columnsCount = 2;
        public int ColumnsCount
        {
            get
            {
                return _columnsCount;
            }

            set
            {
                ColumnCollection.Clear();
                _columnsCount = value; RaisePropertyChanged("ColumnsCount");
                for (int i = 0; i < _columnsCount; i++) ColumnCollection.Add(new DataGridTextColumn());
            }
        }

        #endregion

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
            get {
                return new TranslateTransform(
                    -(_geometry.Bounds.Left + _geometry.Bounds.Width / 2),
                    -(_geometry.Bounds.Top + _geometry.Bounds.Height / 2));
                //return _transform; 
            }
            set {  }
        }

        #region Commands

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
