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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;

namespace PixelTuner.Controls
{
    /// <summary>
    /// Interaction logic for TriangleSlider.xaml
    /// </summary>
    public partial class TriangleSlider : INotifyPropertyChanged
    {
        public TriangleSlider()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        
        #region Coordinate

        public double Coordinate
        {
            get
            {
                return (double)(Value * ((Width - Height) / (MaxValue - MinValue)));
            }
            set
            {
                if (value < MinValue) return;
                double v = value;
                //if (v <= Height) v = Height;

                Value = (int)Math.Round((MaxValue - MinValue) * (v / (Width - Height)));
                RaisePropertyChanged("Coordinate");
            }
        }

        //public double Coordinate
        //{
        //    get { return (double)GetValue(CoordinateProperty); }
        //    set
        //    {
        //        // координата не может быть меньше высоты (для минимального равноширинного треугольника)
        //        double v = value;
        //        if (v <= Height) v = Height;

        //        SetValue(CoordinateProperty, v);
        //        Value = (int)((MaxValue - MinValue) * (v / (Width - Height))); 
        //        RaisePropertyChanged("Coordinate"); 
        //        RaisePropertyChanged("PolyPoints");
        //    }
        //}

        //// Using a DependencyProperty as the backing store for Coordinate.  This enables animation, styling, binding, etc...
        //public static readonly DependencyProperty CoordinateProperty =
        //    DependencyProperty.Register("Coordinate", typeof(double), typeof(TriangleSlider),
        //    new UIPropertyMetadata(10.0, new PropertyChangedCallback(OnCoordinateChanged)));

        //private static void OnCoordinateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    Console.WriteLine("OnCoordinateChanged triggered");
        //}
        #endregion

        #region Value
        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set 
            { 
                SetValue(ValueProperty, value);
                //SetValue(CoordinateProperty, value * ((Width - Height) / (MaxValue - MinValue)));
                RaisePropertyChanged("Value");
                RaisePropertyChanged("PolyPoints");
            }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(TriangleSlider),
            new UIPropertyMetadata(0, new PropertyChangedCallback(OnValueChanged)));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine("OnValueChanged triggered");
        }
        #endregion

        public double HalfHeight
        {
            get
            {
                return Height / 2;
            }
        }

        public string BgPolyPoints
        {
            get
            {
                //return String.Format("0,{1} {0},0 {0},{2} 0,{3} 0,{1}", Width, (int)(Height * 0.44), Height, (int)(Height * 0.56));
                return String.Format("0,0 {0},{1} {0},{3} 0,{2} 0,0", Width, (int)(Height * 0.44), Height, (int)(Height * 0.56));
            }
        }

        public string PolyPoints
        {
            get
            {
                //return String.Format("0,{1} {0},0 {0},{2} 0,{3} 0,{1}", (int)Coordinate, (int)(Height * 0.44), Height, (int)(Height * 0.56));
                return String.Format("0,0 {0},{1} {0},{3} 0,{2} 0,0", (int)Coordinate, (int)(Height * 0.44), Height, (int)(Height * 0.56));
            }
        }

        #region MaxValue
        public int MaxValue
        {
            get { return (int)GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); RaisePropertyChanged("MaxValue"); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register("MaxValue", typeof(int), typeof(TriangleSlider),
            new UIPropertyMetadata(10, new PropertyChangedCallback(OnMaxValueChanged)));

        private static void OnMaxValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine("OnMaxValueChanged triggered");
            // проверку на достижение максимального значения?
        }
        #endregion

        #region MinValue
        public int MinValue
        {
            get { return (int)GetValue(MinValueProperty); }
            set { SetValue(MinValueProperty, value); RaisePropertyChanged("MinValue"); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register("MinValue", typeof(int), typeof(TriangleSlider),
            new UIPropertyMetadata(0, new PropertyChangedCallback(OnMinValueChanged)));

        private static void OnMinValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine("OnMinValueChanged triggered");
            // проверку на достижение максимального значения?
        }
        #endregion

        #region Step
        public int Step
        {
            get { return (int)GetValue(StepProperty); }
            set { SetValue(StepProperty, value); RaisePropertyChanged("Step"); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register("Step", typeof(int), typeof(TriangleSlider),
            new UIPropertyMetadata(1, new PropertyChangedCallback(OnStepChanged)));

        private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine("OnStepChanged triggered");
            // проверку на достижение максимального значения?
        }
        #endregion

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Coordinate = e.GetPosition(sender as IInputElement).X;
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }
    }
}
