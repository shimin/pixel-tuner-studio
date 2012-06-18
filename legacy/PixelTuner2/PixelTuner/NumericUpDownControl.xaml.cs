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

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for NumericUpDownControl.xaml
    /// </summary>
    public partial class NumericUpDownControl : INotifyPropertyChanged
    {
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #region Value
        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); RaisePropertyChanged("Value"); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(NumericUpDownControl),
            new UIPropertyMetadata(0, new PropertyChangedCallback(OnValueChanged)));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine("OnValueChanged triggered");
        }
        #endregion

        #region MaxValue
        public int MaxValue
        {
            get { return (int)GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); RaisePropertyChanged("MaxValue"); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register("MaxValue", typeof(int), typeof(NumericUpDownControl),
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
            DependencyProperty.Register("MinValue", typeof(int), typeof(NumericUpDownControl),
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
            DependencyProperty.Register("Step", typeof(int), typeof(NumericUpDownControl),
            new UIPropertyMetadata(1, new PropertyChangedCallback(OnStepChanged)));

        private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine("OnStepChanged triggered");
            // проверку на достижение максимального значения?
        }
        #endregion

        public NumericUpDownControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Button_Click_Up(object sender, RoutedEventArgs e)
        {
            if (this.Value < this.MinValue)
                this.Value = this.MinValue;
            else if (this.Value < this.MaxValue - this.Step)
                this.Value += this.Step;
            else
                this.Value = this.MaxValue;
        }

        private void Button_Click_Down(object sender, RoutedEventArgs e)
        {
            if (this.Value > this.MaxValue)
                this.Value = this.MaxValue;
            else if (this.Value > this.MinValue + this.Step)
                this.Value -= this.Step;
            else
                this.Value = this.MinValue;
        }
    }
}
