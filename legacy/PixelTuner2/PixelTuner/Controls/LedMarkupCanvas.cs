using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PixelTuner.ViewModel;

namespace PixelTuner.Controls
{
    /// <summary>
    /// Grid of LED cells drawn in one element: numbers, strip path, previews of group
    /// numbering, and all mouse gestures of the markup editor.
    ///
    /// Mouse:
    ///   click / drag            number empty cells starting at NextNumber
    ///   Shift+click             line from the last placed LED to the clicked cell
    ///   Shift+drag              line from the press point to the release point
    ///   Ctrl+drag               rectangle, snake or progressive (editor settings)
    ///   Ctrl+Shift+drag         pick a number range from the cells in a rectangle
    ///   Alt+click               continue numbering after this LED
    ///   right click / drag      remove LEDs, numbering closes up
    ///   double click            set the LED number (move it in the chain)
    ///   Ctrl+wheel              zoom
    /// </summary>
    public class LedMarkupCanvas : FrameworkElement
    {
        private enum Mode { None, Paint, Erase, Line, Rect, Select }

        private Mode _mode = Mode.None;
        private int _pressCell = -1;
        private int _anchorCell = -1;
        private int _lastCell = -1;
        private int _hoverCell = -1;
        private bool _releasing;
        private List<int> _preview = new List<int>();
        private int _selectEndCell = -1;

        static LedMarkupCanvas()
        {
            FocusableProperty.OverrideMetadata(typeof(LedMarkupCanvas), new FrameworkPropertyMetadata(true));
        }

        public LedMarkupCanvas()
        {
            ClipToBounds = true;
            SnapsToDevicePixels = true;
        }

        /// <summary>Raised on double click: the window asks for a new number of the cell.</summary>
        public event EventHandler<CellEventArgs> CellNumberRequested;

        #region Dependency properties

        public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
            "Editor", typeof(LedMarkupEditor), typeof(LedMarkupCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnEditorChanged));

        public LedMarkupEditor Editor
        {
            get { return (LedMarkupEditor)GetValue(EditorProperty); }
            set { SetValue(EditorProperty, value); }
        }

        public static readonly DependencyProperty ShapeGeometryProperty = DependencyProperty.Register(
            "ShapeGeometry", typeof(Geometry), typeof(LedMarkupCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Outline of the canvas element, drawn under the grid.</summary>
        public Geometry ShapeGeometry
        {
            get { return (Geometry)GetValue(ShapeGeometryProperty); }
            set { SetValue(ShapeGeometryProperty, value); }
        }

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
            "Zoom", typeof(double), typeof(LedMarkupCanvas),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        public static readonly DependencyProperty FitWidthProperty = DependencyProperty.Register(
            "FitWidth", typeof(double), typeof(LedMarkupCanvas),
            new FrameworkPropertyMetadata(400.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>Width the grid fits into at zoom 1 (the scroll viewport).</summary>
        public double FitWidth
        {
            get { return (double)GetValue(FitWidthProperty); }
            set { SetValue(FitWidthProperty, value); }
        }

        public static readonly DependencyProperty FitHeightProperty = DependencyProperty.Register(
            "FitHeight", typeof(double), typeof(LedMarkupCanvas),
            new FrameworkPropertyMetadata(400.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double FitHeight
        {
            get { return (double)GetValue(FitHeightProperty); }
            set { SetValue(FitHeightProperty, value); }
        }

        private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (LedMarkupCanvas)d;
            var oldEditor = e.OldValue as LedMarkupEditor;
            var newEditor = e.NewValue as LedMarkupEditor;
            if (oldEditor != null) oldEditor.MarkupChanged -= c.Editor_MarkupChanged;
            if (newEditor != null) newEditor.MarkupChanged += c.Editor_MarkupChanged;
        }

        private void Editor_MarkupChanged(object sender, EventArgs e)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }

        #endregion

        private LedMarkup Markup { get { return Editor == null ? null : Editor.Markup; } }

        public bool IsGestureActive { get { return _mode != Mode.None; } }

        #region Layout

        protected override Size MeasureOverride(Size availableSize)
        {
            var m = Markup;
            double aspect = 1;
            var g = ShapeGeometry;
            if (g != null && !g.Bounds.IsEmpty && g.Bounds.Width > 0 && g.Bounds.Height > 0)
                aspect = g.Bounds.Width / g.Bounds.Height;
            else if (m != null && m.Columns > 0 && m.Rows > 0)
                aspect = (double)m.Columns / m.Rows;

            var fitW = Math.Max(50, FitWidth);
            var fitH = Math.Max(50, FitHeight);
            double w, h;
            if (fitW / fitH > aspect) { h = fitH; w = h * aspect; }
            else { w = fitW; h = w / aspect; }
            var zoom = Math.Max(0.1, Zoom);
            return new Size(Math.Floor(w * zoom), Math.Floor(h * zoom));
        }

        private double CellW { get { var m = Markup; return m == null || m.Columns == 0 ? 0 : ActualWidth / m.Columns; } }
        private double CellH { get { var m = Markup; return m == null || m.Rows == 0 ? 0 : ActualHeight / m.Rows; } }

        private int HitCell(Point p, bool clamp)
        {
            var m = Markup;
            if (m == null || m.CellCount == 0 || CellW <= 0 || CellH <= 0) return -1;
            var x = (int)Math.Floor(p.X / CellW);
            var y = (int)Math.Floor(p.Y / CellH);
            if (clamp)
            {
                x = Math.Max(0, Math.Min(m.Columns - 1, x));
                y = Math.Max(0, Math.Min(m.Rows - 1, y));
            }
            return m.IsInside(x, y) ? m.CellIndex(x, y) : -1;
        }

        private Rect CellRect(int cell, double inset)
        {
            var m = Markup;
            var r = new Rect(m.CellX(cell) * CellW, m.CellY(cell) * CellH, CellW, CellH);
            var i = Math.Min(inset, Math.Min(r.Width, r.Height) * 0.4);
            r.Inflate(-i, -i);
            return r;
        }

        private Point CellCenter(int cell)
        {
            var m = Markup;
            return new Point((m.CellX(cell) + 0.5) * CellW, (m.CellY(cell) + 0.5) * CellH);
        }

        #endregion

        #region Rendering

        private static readonly Brush BackgroundBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));
        private static readonly Brush ShapeFill = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));
        private static readonly Brush EmptyCellBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)));
        private static readonly Brush PlainCellBrush = Frozen(new SolidColorBrush(Color.FromArgb(0xD0, 0xE0, 0x40, 0x40)));
        private static readonly Brush PreviewBrush = Frozen(new SolidColorBrush(Color.FromArgb(0xA0, 0x30, 0xD0, 0x60)));
        private static readonly Brush SelectFill = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0x40, 0xC0, 0xFF)));
        private static readonly Brush StartBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x20, 0xE0, 0x40)));
        private static readonly Brush EndBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x40, 0x40)));
        private static readonly Typeface NumberFace = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Typeface PreviewFace = new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);

        protected override void OnRender(DrawingContext dc)
        {
            var size = new Rect(0, 0, ActualWidth, ActualHeight);
            dc.DrawRectangle(BackgroundBrush, null, size);
            var m = Markup;
            var editor = Editor;
            if (m == null || m.CellCount == 0 || size.Width < 1 || size.Height < 1) return;

            DrawShape(dc, size);

            double cw = CellW, ch = CellH, cmin = Math.Min(cw, ch);
            double inset = editor.Spacing * Math.Max(0.1, Zoom) / 2;
            double radius = cmin * 0.15;
            var issues = editor.ShowSourceIssues ? editor.SourceIssues : null;
            var count = m.Count;

            var rangePen = Frozen(new Pen(Brushes.Cyan, Math.Max(2, cmin * 0.08)));
            var dupPen = Frozen(new Pen(Brushes.Red, Math.Max(2, cmin * 0.1)));
            var gapPen = Frozen(new Pen(Brushes.Orange, Math.Max(2, cmin * 0.1)));
            var hoverPen = Frozen(new Pen(Brushes.White, Math.Max(1.5, cmin * 0.06)));
            var emptyPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)), 1));

            var previewSet = new HashSet<int>(_preview);
            var fontCache = new Dictionary<string, FormattedText>();

            for (int cell = 0; cell < m.CellCount; cell++)
            {
                var n = m.GetNumber(cell);
                var r = CellRect(cell, inset);
                Brush fill;
                if (n == 0) fill = EmptyCellBrush;
                else fill = editor.ShowGradient ? GradientBrush(n, count) : PlainCellBrush;

                Pen pen = n == 0 ? emptyPen : null;
                if (issues != null && issues.DuplicateCells.Contains(cell)) pen = dupPen;
                else if (issues != null && issues.CellsAfterGap.Contains(cell)) pen = gapPen;
                if (n > 0 && editor.IsInRange(n)) pen = rangePen;
                if (cell == _hoverCell) pen = hoverPen;

                dc.DrawRoundedRectangle(fill, pen, r, radius, radius);

                if (n > 0 && editor.ShowNumbers && !previewSet.Contains(cell))
                    DrawNumber(dc, n.ToString(CultureInfo.InvariantCulture), r, NumberFace, TextBrushFor(fill), fontCache);
            }

            if (editor.ShowPath && count > 1) DrawPath(dc, m, cmin);

            DrawPreview(dc, m, inset, radius, fontCache);
        }

        private void DrawShape(DrawingContext dc, Rect size)
        {
            var g = ShapeGeometry;
            if (g == null || g.Bounds.IsEmpty || g.Bounds.Width <= 0 || g.Bounds.Height <= 0) return;
            var b = g.Bounds;
            double sx = size.Width / b.Width, sy = size.Height / b.Height;
            dc.PushTransform(new MatrixTransform(sx, 0, 0, sy, -b.Left * sx, -b.Top * sy));
            dc.DrawGeometry(ShapeFill, new Pen(Brushes.Black, 1.0 / Math.Max(0.0001, Math.Min(sx, sy))), g);
            dc.Pop();
        }

        private void DrawPath(DrawingContext dc, LedMarkup m, double cmin)
        {
            var thickness = Math.Max(1, cmin * 0.07);
            var solid = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0xD0, 0xFF, 0xFF, 0xFF)), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
            var dashed = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)), thickness) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) });
            var arrowLen = Math.Max(4, cmin * 0.22);

            var chain = m.Chain;
            for (int i = 1; i < chain.Count; i++)
            {
                int a = chain[i - 1], b = chain[i];
                var pa = CellCenter(a);
                var pb = CellCenter(b);
                bool adjacent = Math.Abs(m.CellX(a) - m.CellX(b)) <= 1 && Math.Abs(m.CellY(a) - m.CellY(b)) <= 1;
                dc.DrawLine(adjacent ? solid : dashed, pa, pb);

                var v = pb - pa;
                if (v.Length >= arrowLen * 2.5)
                {
                    v.Normalize();
                    var mid = pa + (pb - pa) / 2;
                    var tip = mid + v * (arrowLen / 2);
                    var normal = new Vector(-v.Y, v.X);
                    dc.DrawLine(solid, tip, tip - v * arrowLen + normal * arrowLen * 0.6);
                    dc.DrawLine(solid, tip, tip - v * arrowLen - normal * arrowLen * 0.6);
                }
            }

            var marker = Math.Max(3, cmin * 0.16);
            dc.DrawEllipse(StartBrush, new Pen(Brushes.Black, 1), CellCenter(chain[0]), marker, marker);
            var end = CellCenter(chain[chain.Count - 1]);
            dc.DrawRectangle(EndBrush, new Pen(Brushes.Black, 1), new Rect(end.X - marker, end.Y - marker, marker * 2, marker * 2));
        }

        private void DrawPreview(DrawingContext dc, LedMarkup m, double inset, double radius, Dictionary<string, FormattedText> fontCache)
        {
            if (_mode == Mode.Select && _anchorCell >= 0 && _selectEndCell >= 0)
            {
                var r = Rect.Union(CellRect(_anchorCell, 0), CellRect(_selectEndCell, 0));
                dc.DrawRectangle(SelectFill, new Pen(Brushes.DeepSkyBlue, 2) { DashStyle = DashStyles.Dash }, r);
                return;
            }
            if (_preview.Count == 0) return;

            var numbers = PreviewNumbers();
            foreach (var kv in numbers)
            {
                var r = CellRect(kv.Key, inset);
                dc.DrawRoundedRectangle(PreviewBrush, new Pen(Brushes.White, 1), r, radius, radius);
                DrawNumber(dc, kv.Value.ToString(CultureInfo.InvariantCulture), r, PreviewFace, Brushes.White, fontCache);
            }
        }

        /// <summary>Numbers the pending line/rectangle would get.</summary>
        private List<KeyValuePair<int, int>> PreviewNumbers()
        {
            var m = Markup;
            var editor = Editor;
            var result = new List<KeyValuePair<int, int>>();
            var seen = new HashSet<int>();
            var next = editor.NextNumber;
            if (editor.Overwrite)
                next -= _preview.Count(c => m.IsNumbered(c) && m.GetNumber(c) < editor.NextNumber && seen.Add(c));
            seen.Clear();
            foreach (var c in _preview)
            {
                if (!seen.Add(c)) continue;
                if (m.IsNumbered(c) && !editor.Overwrite) continue;
                result.Add(new KeyValuePair<int, int>(c, next++));
            }
            return result;
        }

        private static void DrawNumber(DrawingContext dc, string text, Rect r, Typeface face, Brush brush, Dictionary<string, FormattedText> cache)
        {
            var size = Math.Min(r.Height * 0.55, r.Width * 0.95 / (0.58 * text.Length));
            if (size < 5) return;
            size = Math.Round(size * 2) / 2;
            var key = text + "|" + size + "|" + face.Style + "|" + brush.GetHashCode();
            FormattedText ft;
            if (!cache.TryGetValue(key, out ft))
            {
                ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, brush);
                cache[key] = ft;
            }
            dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) / 2, r.Y + (r.Height - ft.Height) / 2));
        }

        private static Brush GradientBrush(int number, int count)
        {
            var t = count <= 1 ? 0 : (double)(number - 1) / (count - 1);
            // Blue at the strip start through cyan and green to yellow at the end.
            var hue = 230 - 175 * t;
            return Frozen(new SolidColorBrush(FromHsv(hue, 0.7, 0.95)));
        }

        private static Brush TextBrushFor(Brush fill)
        {
            var s = fill as SolidColorBrush;
            if (s == null) return Brushes.Black;
            var c = s.Color;
            var lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) * c.A / 255.0;
            return lum > 120 ? Brushes.Black : Brushes.White;
        }

        private static Color FromHsv(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            var c = v * s;
            var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            var mm = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb((byte)((r + mm) * 255), (byte)((g + mm) * 255), (byte)((b + mm) * 255));
        }

        private static T Frozen<T>(T f) where T : Freezable
        {
            if (f.CanFreeze) f.Freeze();
            return f;
        }

        #endregion

        #region Mouse

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            var editor = Editor;
            if (editor == null || _mode != Mode.None) return;
            var cell = HitCell(e.GetPosition(this), false);
            if (cell < 0) return;
            e.Handled = true;
            var mods = Keyboard.Modifiers;

            if (e.ClickCount == 2 && mods == ModifierKeys.None)
            {
                if (editor.Markup.IsNumbered(cell))
                {
                    var h = CellNumberRequested;
                    if (h != null) h(this, new CellEventArgs(cell));
                }
                return;
            }

            if (mods == ModifierKeys.Alt)
            {
                editor.ContinueAfter(cell);
                return;
            }

            _pressCell = _lastCell = cell;
            if (mods == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                _mode = Mode.Select;
                _anchorCell = _selectEndCell = cell;
            }
            else if (mods == ModifierKeys.Control)
            {
                _mode = Mode.Rect;
                _anchorCell = cell;
                UpdateShapePreview(cell);
            }
            else if (mods == ModifierKeys.Shift)
            {
                _mode = Mode.Line;
                var last = editor.LastPlacedCell;
                _anchorCell = editor.Markup.IsNumbered(last) && last != cell ? last : cell;
                UpdateShapePreview(cell);
            }
            else
            {
                _mode = Mode.Paint;
                editor.BeginGesture();
                editor.PlaceCells(new[] { cell }, false);
            }
            CaptureMouse();
            InvalidateVisual();
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            Focus();
            var editor = Editor;
            if (editor == null || _mode != Mode.None) return;
            var cell = HitCell(e.GetPosition(this), false);
            if (cell < 0) return;
            e.Handled = true;
            _mode = Mode.Erase;
            _pressCell = _lastCell = cell;
            editor.BeginGesture();
            editor.EraseCells(new[] { cell });
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var editor = Editor;
            if (editor == null) return;
            var p = e.GetPosition(this);
            var hover = HitCell(p, false);
            if (hover != _hoverCell)
            {
                _hoverCell = hover;
                editor.SetHover(hover);
                if (_mode == Mode.None) InvalidateVisual();
            }
            if (_mode == Mode.None) return;

            var cell = HitCell(p, true);
            if (cell < 0 || cell == _lastCell) return;
            var m = editor.Markup;

            switch (_mode)
            {
                case Mode.Paint:
                    editor.PlaceCells(LedMarkup.LineCells(m, m.CellX(_lastCell), m.CellY(_lastCell), m.CellX(cell), m.CellY(cell)).Skip(1).ToList(), false);
                    break;
                case Mode.Erase:
                    editor.EraseCells(LedMarkup.LineCells(m, m.CellX(_lastCell), m.CellY(_lastCell), m.CellX(cell), m.CellY(cell)));
                    break;
                case Mode.Line:
                    // Dragging with Shift draws from the press point instead of the last LED.
                    _anchorCell = _pressCell;
                    UpdateShapePreview(cell);
                    break;
                case Mode.Rect:
                    UpdateShapePreview(cell);
                    break;
                case Mode.Select:
                    _selectEndCell = cell;
                    break;
            }
            _lastCell = cell;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_mode == Mode.Erase || _mode == Mode.None) return;
            e.Handled = true;
            FinishGesture(true);
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            if (_mode != Mode.Erase) return;
            e.Handled = true;
            FinishGesture(true);
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (_releasing || _mode == Mode.None) return;
            // Capture lost from outside (window switch): keep painting, drop pending shapes.
            FinishGesture(_mode == Mode.Paint || _mode == Mode.Erase);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverCell >= 0 && _mode == Mode.None)
            {
                _hoverCell = -1;
                if (Editor != null) Editor.SetHover(-1);
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var z = Zoom * Math.Pow(1.15, e.Delta / 120.0);
                Zoom = Math.Max(0.5, Math.Min(12, z));
                e.Handled = true;
                return;
            }
            base.OnMouseWheel(e);
        }

        /// <summary>Esc: drops the gesture in progress. Returns false when there was none.</summary>
        public bool CancelGesture()
        {
            if (_mode == Mode.None) return false;
            if (_mode == Mode.Paint || _mode == Mode.Erase) Editor.CancelGesture();
            ResetGesture();
            return true;
        }

        private void FinishGesture(bool commit)
        {
            var editor = Editor;
            var m = editor.Markup;
            switch (_mode)
            {
                case Mode.Paint:
                case Mode.Erase:
                    if (commit) editor.EndGesture(); else editor.CancelGesture();
                    break;
                case Mode.Line:
                case Mode.Rect:
                    if (commit && _preview.Count > 0)
                    {
                        editor.BeginGesture();
                        editor.PlaceCells(_preview, editor.Overwrite);
                        editor.EndGesture();
                    }
                    break;
                case Mode.Select:
                    if (commit)
                        editor.SelectRangeFromCells(LedMarkup.RectCells(m, m.CellX(_anchorCell), m.CellY(_anchorCell), m.CellX(_selectEndCell), m.CellY(_selectEndCell), false, false));
                    break;
            }
            ResetGesture();
        }

        private void ResetGesture()
        {
            _mode = Mode.None;
            _preview = new List<int>();
            _anchorCell = _pressCell = _lastCell = _selectEndCell = -1;
            if (IsMouseCaptured)
            {
                _releasing = true;
                ReleaseMouseCapture();
                _releasing = false;
            }
            InvalidateVisual();
        }

        private void UpdateShapePreview(int cell)
        {
            var editor = Editor;
            var m = editor.Markup;
            if (_mode == Mode.Line)
                _preview = LedMarkup.LineCells(m, m.CellX(_anchorCell), m.CellY(_anchorCell), m.CellX(cell), m.CellY(cell));
            else if (_mode == Mode.Rect)
                _preview = LedMarkup.RectCells(m, m.CellX(_anchorCell), m.CellY(_anchorCell), m.CellX(cell), m.CellY(cell), editor.ByColumns, editor.Snake);
        }

        #endregion
    }

    public class CellEventArgs : EventArgs
    {
        public CellEventArgs(int cell) { Cell = cell; }
        public int Cell { get; private set; }
    }
}
