using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using PixelTuner.Drawing;
using PixelTuner.Properties;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for PictureEditor.xaml
    /// </summary>
    public partial class PictureEditor : Window, INotifyPropertyChanged
    {
        //public PictureEditor()
        //    : this(new ConfigurationItem())
        //{
            
        //}

        public PictureEditor(ConfigurationItem config)
        {
            _drawControl = new Drawing.DrawControl(config);
            InitializeComponent();
            this.SizeChanged += new SizeChangedEventHandler(PictureEditor_SizeChanged);
            this.Loaded += new RoutedEventHandler(PictureEditor_Loaded);
        }

        void PictureEditor_Loaded(object sender, RoutedEventArgs e)
        {
            this.windowsFormsHost.Child = DrawControl;
            DrawControl.PatternLinesChanged += new EventHandler(DrawControl_PatternLinesChanged);
            DrawControl.Resize += new EventHandler(DrawControl_Resize);
            this.Closing += new CancelEventHandler(PictureEditor_Closing);
        }

        void PictureEditor_Closing(object sender, CancelEventArgs e)
        {
            if (DrawControl != null)
            {
                DrawControl.CleanUp();
                DrawControl.Dispose();
            }
        }

        void DrawControl_Resize(object sender, EventArgs e)
        {
            RaisePropertyChanged("CanvasWidth");
        }

        void DrawControl_PatternLinesChanged(object sender, EventArgs e)
        {
            RaisePropertyChanged("CanvasHeight");
        }

        DrawControl _drawControl = null;
        DrawControl DrawControl
        {
            get
            {
                if (_drawControl == null) _drawControl = new DrawControl(Config);
                return _drawControl;
            }
        }

        public ConfigurationItem Config
        {
            get
            {
                if (_drawControl != null) return _drawControl.Configuration;
                else return null;
            }
            set 
            { 
                if (_drawControl != null) _drawControl.Configuration = value; 
            }
        }
        
        void PictureEditor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RaisePropertyChanged("ContentHeight");
        }

        /// <summary>
        /// Возвращает высоту полотна в пикселах. Исходя из количества квадратов и их размера.
        /// </summary>
        public double CanvasHeight
        {
            get
            {
                return _drawControl.ControlHeight;
            }
        }

        /// <summary>
        /// Возвращает высоту формы, за вычетом заголовка
        /// </summary>
        public double ContentHeight
        {
            get
            {
                return this.Height - 40;
            }
        }

        public double CanvasWidth
        {
            get
            {
                if (this.DrawControl == null) return this.Width;
                return this.DrawControl.Width;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
