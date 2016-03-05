using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Common.ViewModel;
using System.Windows;

namespace PixelTuner.ViewModel
{
    public class CanvasElementViewModel : ViewModelBase, Common.Controls.ITimeLineMeta
    {
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
            set { _transform = value; RaisePropertyChanged("Transform"); }
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

        public object Clone()
        {
            var newObj = (CanvasElementViewModel)Activator.CreateInstance(GetType());
            newObj._fill = this._fill;
            newObj._geometry = this._geometry;
            newObj._path = this._path;
            newObj._stroke = this._stroke;
            newObj._transform = this._transform;
            return newObj;
        }
    }
}
