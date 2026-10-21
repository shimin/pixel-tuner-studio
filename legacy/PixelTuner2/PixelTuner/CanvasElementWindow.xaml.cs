using PixelTuner.Controls;
using PixelTuner.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PixelTuner
{
    /// <summary>
    /// LED markup editor: maps grid cells (X, Y pixels of the frame) to LED numbers of the strip.
    /// Edits a working copy; the element changes only on OK.
    /// </summary>
    public partial class CanvasElementWindow : Window
    {
        private bool _closingHandled;

        public CanvasElementWindow(CanvasElementViewModel element)
        {
            InitializeComponent();
            CanvasElement = element;

            LedMarkupIssues issues;
            var markup = element.CreateMarkup(out issues);
            Editor = new LedMarkupEditor(markup, issues, element.TilesMargin);
            DataContext = Editor;
            MarkupCanvas.Editor = Editor;
            MarkupCanvas.ShapeGeometry = element.Geometry;
            Loaded += (s, e) => MarkupCanvas.Focus();
        }

        public CanvasElementViewModel CanvasElement { get; private set; }

        public LedMarkupEditor Editor { get; private set; }

        /// <summary>True when the markup was written back to the element.</summary>
        public bool Applied { get; private set; }

        private bool HasChanges
        {
            get { return Editor.IsChanged || Editor.HasSourceIssues || Editor.Spacing != CanvasElement.TilesMargin; }
        }

        private void Apply()
        {
            CanvasElement.ApplyMarkup(Editor.Markup);
            CanvasElement.TilesMargin = Editor.Spacing;
            Applied = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (HasChanges) Apply();
            _closingHandled = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _closingHandled = true;
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_closingHandled || !Editor.IsChanged) return;
            var answer = MessageBox.Show(this,
                LedMarkupEditor.L("Markup_SaveChanges", "Save markup changes?"),
                Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) e.Cancel = true;
            else if (answer == MessageBoxResult.Yes) Apply();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            var inText = e.OriginalSource is TextBoxBase;

            if (e.Key == Key.Escape)
            {
                if (!MarkupCanvas.CancelGesture())
                {
                    _closingHandled = !Editor.IsChanged;
                    Close();
                }
                e.Handled = true;
                return;
            }
            if (inText) return;

            if (mods == ModifierKeys.Control && e.Key == Key.Z) { Editor.Undo(); e.Handled = true; }
            else if ((mods == ModifierKeys.Control && e.Key == Key.Y) ||
                     (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z)) { Editor.Redo(); e.Handled = true; }
            else if (mods == ModifierKeys.None && e.Key == Key.Delete) { Execute(Editor.DeleteRangeCommand); e.Handled = true; }
            else if (mods == ModifierKeys.None && e.Key == Key.R) { Execute(Editor.ReverseRangeCommand); e.Handled = true; }
        }

        private static void Execute(ICommand command)
        {
            if (command.CanExecute(null)) command.Execute(null);
        }

        private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // At zoom 1 the grid fits the visible area; leave room for margins and scroll bars.
            MarkupCanvas.FitWidth = System.Math.Max(50, Scroller.ActualWidth - 40);
            MarkupCanvas.FitHeight = System.Math.Max(50, Scroller.ActualHeight - 40);
        }

        private void MarkupCanvas_CellNumberRequested(object sender, CellEventArgs e)
        {
            var current = Editor.Markup.GetNumber(e.Cell);
            if (current == 0) return;
            var w = new SetLedNumberWindow(current, Editor.Markup.Count) { Owner = this };
            if (w.ShowDialog() == true && w.Value != current)
                Editor.MoveCellTo(e.Cell, w.Value);
        }
    }
}
