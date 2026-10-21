using System;
using System.Windows;
using System.Windows.Input;
using PixelTuner.ViewModel;

namespace PixelTuner
{
    /// <summary>
    /// Asks for a new LED number. The LED moves to that place in the chain,
    /// the LEDs between shift by one, so numbers stay unique and continuous.
    /// </summary>
    public partial class SetLedNumberWindow : Window
    {
        private readonly int _max;

        public SetLedNumberWindow(int value, int max)
        {
            InitializeComponent();
            _max = Math.Max(1, max);
            Value = value;
            boxValue.Maximum = _max;
            boxValue.Value = value;
            boxHint.Text = string.Format(LedMarkupEditor.L("Markup_SetNumberHint", "1–{0}. Other LEDs shift to make room."), _max);
            Loaded += (s, e) => boxValue.Focus();
        }

        public int Value { get; private set; }

        private void Accept()
        {
            object raw = boxValue.Value;
            var v = raw == null ? Value : Convert.ToInt32(raw);
            Value = Math.Max(1, Math.Min(_max, v));
            DialogResult = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) DialogResult = false;
            else if (e.Key == Key.Enter) Accept();
        }

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            Accept();
        }
    }
}
