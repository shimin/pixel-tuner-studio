using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PixelTuner.Core.Layout;

namespace PixelTuner.UI.LedLayouts;

/// <summary>
/// The grid of a prop with its LEDs over the outline of the prop (PixelTuner 2's <c>LedMarkupCanvas</c>). It draws
/// <see cref="LedMarkupViewModel"/> and turns the pointer into cells and keys; what a press, a drag or a double click mean is decided in
/// the view model, which is tested without a window. Put it in a ScrollViewer and give it the viewport: at zoom 1 the grid fits in it
/// (in the proportions of the outline when there is one, so the cells are as wide and high as the prop is), Ctrl + wheel zooms.
/// </summary>
public sealed class LedMarkupCanvas : Control
{
    public static readonly StyledProperty<LedMarkupViewModel?> EditorProperty =
        AvaloniaProperty.Register<LedMarkupCanvas, LedMarkupViewModel?>(nameof(Editor));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<LedMarkupCanvas, double>(nameof(Zoom), 1.0);

    /// <summary>The visible part of the scroll viewer the canvas sits in: the grid fits into it at zoom 1.</summary>
    public static readonly StyledProperty<Size> ViewportProperty =
        AvaloniaProperty.Register<LedMarkupCanvas, Size>(nameof(Viewport), new Size(400, 400));

    public const double MinZoom = 0.5, MaxZoom = 12;

    // Numbers are written in the cells that are large enough to hold them.
    private const double NumbersFromCellSize = 14;
    private const double ArrowsFromCellSize = 22;

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
    private static readonly IBrush ShapeFill = new SolidColorBrush(Colors.White, 0.19);
    private static readonly IBrush EmptyCell = new SolidColorBrush(Colors.White, 0.22);
    private static readonly IBrush PlainCell = new SolidColorBrush(Color.FromRgb(0xE0, 0x40, 0x40), 0.82);
    private static readonly IBrush PreviewBrush = new SolidColorBrush(Color.FromRgb(0x30, 0xD0, 0x60), 0.63);
    private static readonly IBrush SelectFill = new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0xFF), 0.19);
    private static readonly IBrush StartBrush = new SolidColorBrush(Color.FromRgb(0x20, 0xE0, 0x40));
    private static readonly IBrush EndBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x40, 0x40));
    private static readonly IPen EmptyPen = new Pen(new SolidColorBrush(Colors.White, 0.3), 1);
    private static readonly IPen SelectPen = new Pen(Brushes.DeepSkyBlue, 2, new DashStyle([4, 3], 0));
    private static readonly IPen MarkerPen = new Pen(Brushes.Black, 1);

    private readonly Dictionary<(string Text, int Size, bool Dark, bool Italic), FormattedText> _texts = [];
    private bool _pressed;
    private CanvasShape? _outlineSource;
    private Geometry? _outline;

    static LedMarkupCanvas()
    {
        AffectsRender<LedMarkupCanvas>(EditorProperty, ZoomProperty);
        AffectsMeasure<LedMarkupCanvas>(EditorProperty, ZoomProperty, ViewportProperty);
    }

    public LedMarkupCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public LedMarkupViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, Math.Clamp(value, MinZoom, MaxZoom));
    }

    public Size Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != EditorProperty) return;
        if (change.OldValue is LedMarkupViewModel old) old.Redraw -= Redrawn;
        if (change.NewValue is LedMarkupViewModel editor) editor.Redraw += Redrawn;
    }

    private void Redrawn(object? sender, EventArgs e)
    {
        // A new grid size is another size of the control.
        InvalidateMeasure();
        InvalidateVisual();
    }

    // ---- where the grid is

    // The outline of the prop, as the box the grid is stretched over; none without an outline or with one that has no size.
    private (double X, double Y, double Width, double Height)? OutlineBox =>
        Editor?.Outline is { } shape && shape.Bounds is { Width: > 0, Height: > 0 } box ? box : null;

    // The size of the whole grid: as large as fits in the viewport, with room for the scroll bars, in the proportions of the outline
    // (or of the grid itself), times the zoom.
    private Size GridSize()
    {
        if (Editor is not { } editor) return default;
        double aspect = OutlineBox is { } box ? box.Width / box.Height : (double)editor.Markup.Columns / editor.Markup.Rows;
        double fitWidth = Math.Max(50, Viewport.Width - 20), fitHeight = Math.Max(50, Viewport.Height - 20);
        double width, height;
        if (fitWidth / fitHeight > aspect)
        {
            height = fitHeight;
            width = height * aspect;
        }
        else
        {
            width = fitWidth;
            height = width / aspect;
        }
        double zoom = Math.Max(0.1, Zoom);
        return new Size(Math.Max(editor.Markup.Columns, Math.Floor(width * zoom)), Math.Max(editor.Markup.Rows, Math.Floor(height * zoom)));
    }

    protected override Size MeasureOverride(Size availableSize) => GridSize();

    private (double Width, double Height) CellSize()
    {
        if (Editor is not { } editor) return (1, 1);
        var size = GridSize();
        return (size.Width / editor.Markup.Columns, size.Height / editor.Markup.Rows);
    }

    private (int X, int Y) CellAt(Point point, bool clamp)
    {
        var (w, h) = CellSize();
        int x = (int)Math.Floor(point.X / w), y = (int)Math.Floor(point.Y / h);
        if (clamp && Editor is { } editor) (x, y) = (Math.Clamp(x, 0, editor.Markup.Columns - 1), Math.Clamp(y, 0, editor.Markup.Rows - 1));
        return (x, y);
    }

    // ---- drawing

    // Blue at the strip start through cyan and green to yellow at the end.
    private static Color GradientColour(int number, int count)
    {
        double t = count <= 1 ? 0 : (double)(number - 1) / (count - 1);
        return Hsv(230 - 175 * t, 0.7, 0.95);
    }

    private static Color Hsv(double hue, double saturation, double value)
    {
        hue = (hue % 360 + 360) % 360;
        double c = value * saturation, h = hue / 60, x = c * (1 - Math.Abs(h % 2 - 1)), m = value - c;
        (double r, double g, double b) = (int)h switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    private static bool IsLight(IBrush brush) =>
        brush is ISolidColorBrush { Color: var c } && (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) * c.A / 255.0 > 120;

    private void Number(DrawingContext context, Rect rect, string text, bool dark, bool italic = false)
    {
        double size = Math.Min(rect.Height * 0.55, rect.Width * 0.95 / (0.58 * text.Length));
        if (size < 5 || Math.Min(rect.Width, rect.Height) < NumbersFromCellSize * 0.7) return;
        int rounded = (int)Math.Round(size);
        var key = (text, rounded, dark, italic);
        if (!_texts.TryGetValue(key, out var formatted))
        {
            if (_texts.Count > 20000) _texts.Clear();
            formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, italic ? FontStyle.Italic : FontStyle.Normal, FontWeight.SemiBold), rounded, dark ? Brushes.Black : Brushes.White);
            _texts[key] = formatted;
        }
        context.DrawText(formatted, new Point(rect.X + (rect.Width - formatted.Width) / 2, rect.Y + (rect.Height - formatted.Height) / 2));
    }

    private static Rect CellRect(LedMarkup markup, int cell, double cellWidth, double cellHeight, double inset = 0)
    {
        var (x, y) = markup.Position(cell);
        double shrinkX = Math.Min(inset, cellWidth * 0.4), shrinkY = Math.Min(inset, cellHeight * 0.4);
        return new Rect(x * cellWidth + shrinkX, y * cellHeight + shrinkY, cellWidth - 2 * shrinkX, cellHeight - 2 * shrinkY);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Editor is not { } editor) return;
        var markup = editor.Markup;
        var size = GridSize();
        var (cw, ch) = CellSize();
        context.FillRectangle(Background, new Rect(0, 0, size.Width, size.Height));
        DrawOutline(context, size);

        double smallest = Math.Min(cw, ch);
        double inset = editor.Spacing * Math.Max(0.1, Zoom) / 2, radius = smallest * 0.15;
        double ring = Math.Max(2, smallest * 0.08), hoverRing = Math.Max(1.5, smallest * 0.06);
        var rangePen = new Pen(Brushes.Cyan, ring);
        var repeatPen = new Pen(Brushes.Red, Math.Max(2, smallest * 0.1));
        var gapPen = new Pen(Brushes.Orange, Math.Max(2, smallest * 0.1));
        var hoverPen = new Pen(Brushes.White, hoverRing);
        var issues = editor.ShowSourceIssuesOnGrid ? editor.SourceIssues : null;
        var repeated = issues is null ? null : new HashSet<int>(issues.DuplicateCells);
        var afterGap = issues is null ? null : new HashSet<int>(issues.CellsAfterGap);
        var previewCells = editor.Preview.Select(p => p.Cell).ToHashSet();

        for (int c = 0; c < markup.Columns * markup.Rows; c++)
        {
            int number = markup.NumberOf(c);
            var rect = CellRect(markup, c, cw, ch, inset);
            IBrush fill = number == 0 ? EmptyCell : editor.ShowGradient ? new SolidColorBrush(GradientColour(number, markup.Count)) : PlainCell;
            IPen? pen = number == 0 ? EmptyPen : null;
            if (repeated is not null && repeated.Contains(c)) pen = repeatPen;
            else if (afterGap is not null && afterGap.Contains(c)) pen = gapPen;
            if (number > 0 && editor.IsInRange(number)) pen = rangePen;
            if (c == editor.HoverCell) pen = hoverPen;
            context.DrawRectangle(fill, pen, rect, radius, radius);
            if (number > 0 && editor.ShowNumbers && !previewCells.Contains(c)) Number(context, rect, number.ToString(CultureInfo.InvariantCulture), IsLight(fill));
        }

        if (editor.ShowPath && markup.Count > 1) DrawPath(context, markup, cw, ch);
        DrawGesture(context, editor, markup, cw, ch, inset, radius);

        if (editor.TransferCell >= 0)
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x57)), 3), CellRect(markup, editor.TransferCell, cw, ch), radius, radius);
    }

    // The outline of the prop under the grid, stretched over it: the grid is a picture of the prop, not of its pixels.
    private void DrawOutline(DrawingContext context, Size size)
    {
        if (Editor?.Outline is not { } shape || OutlineBox is not { } box) return;
        if (!ReferenceEquals(shape, _outlineSource))
        {
            _outlineSource = shape;
            try
            {
                _outline = Geometry.Parse(shape.PathData);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                _outline = null;   // path data the platform cannot read: the grid is drawn without the outline
            }
        }
        if (_outline is null) return;

        double sx = size.Width / box.Width, sy = size.Height / box.Height;
        var t = shape.Transform;
        var matrix = new Matrix(t.A, t.B, t.C, t.D, t.E, t.F) * Matrix.CreateTranslation(-box.X, -box.Y) * Matrix.CreateScale(sx, sy);
        using (context.PushTransform(matrix))
            context.DrawGeometry(ShapeFill, new Pen(Brushes.Black, 1.0 / Math.Max(0.0001, Math.Min(sx, sy))), _outline);
    }

    private static Point Centre(LedMarkup markup, int cell, double width, double height)
    {
        var (x, y) = markup.Position(cell);
        return new Point((x + 0.5) * width, (y + 0.5) * height);
    }

    // The route of the strip, from LED to LED, with an arrow in the middle of each step; steps that jump over cells are dashed. A green
    // disc marks the first LED and a red square the last.
    private static void DrawPath(DrawingContext context, LedMarkup markup, double cw, double ch)
    {
        double smallest = Math.Min(cw, ch);
        double thickness = Math.Max(1, smallest * 0.04);
        var solid = new Pen(new SolidColorBrush(Colors.White, 0.82), thickness, lineCap: PenLineCap.Round);
        var dashed = new Pen(new SolidColorBrush(Colors.White, 0.56), thickness, new DashStyle([2, 2], 0));
        double arrow = Math.Max(4, smallest * 0.22);

        var near = new StreamGeometry();
        var far = new StreamGeometry();
        var arrows = new StreamGeometry();
        using (var nearContext = near.Open())
        using (var farContext = far.Open())
        using (var arrowContext = arrows.Open())
        {
            var chain = markup.Chain;
            for (int i = 1; i < chain.Count; i++)
            {
                var a = Centre(markup, chain[i - 1], cw, ch);
                var b = Centre(markup, chain[i], cw, ch);
                var (x0, y0) = markup.Position(chain[i - 1]);
                var (x1, y1) = markup.Position(chain[i]);
                var target = Math.Abs(x0 - x1) <= 1 && Math.Abs(y0 - y1) <= 1 ? nearContext : farContext;
                target.BeginFigure(a, false);
                target.LineTo(b);
                target.EndFigure(false);

                double length = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                if (smallest < ArrowsFromCellSize || length < arrow * 2.5) continue;
                double ux = (b.X - a.X) / length, uy = (b.Y - a.Y) / length;
                var middle = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                var tip = new Point(middle.X + ux * arrow / 2, middle.Y + uy * arrow / 2);
                // Two strokes from the tip, as PixelTuner 2 drew its arrowheads.
                arrowContext.BeginFigure(new Point(tip.X - ux * arrow - uy * arrow * 0.6, tip.Y - uy * arrow + ux * arrow * 0.6), false);
                arrowContext.LineTo(tip);
                arrowContext.LineTo(new Point(tip.X - ux * arrow + uy * arrow * 0.6, tip.Y - uy * arrow - ux * arrow * 0.6));
                arrowContext.EndFigure(false);
            }
        }
        context.DrawGeometry(null, solid, near);
        context.DrawGeometry(null, dashed, far);
        context.DrawGeometry(null, solid, arrows);

        double marker = Math.Max(3, smallest * 0.16);
        var start = Centre(markup, markup.Chain[0], cw, ch);
        context.DrawEllipse(StartBrush, MarkerPen, start, marker, marker);
        var end = Centre(markup, markup.Chain[^1], cw, ch);
        context.DrawRectangle(EndBrush, MarkerPen, new Rect(end.X - marker, end.Y - marker, marker * 2, marker * 2));
    }

    // What the pointer that is down is making: the cells and numbers of a line or a fill, or the rectangle of a range pick.
    private void DrawGesture(DrawingContext context, LedMarkupViewModel editor, LedMarkup markup, double cw, double ch, double inset, double radius)
    {
        if (editor.Selection is { } selection)
        {
            var rect = CellRect(markup, markup.Cell(selection.X0, selection.Y0), cw, ch).Union(CellRect(markup, markup.Cell(selection.X1, selection.Y1), cw, ch));
            context.DrawRectangle(SelectFill, SelectPen, rect);
            return;
        }
        foreach (var (c, number) in editor.Preview)
        {
            var rect = CellRect(markup, c, cw, ch, inset);
            context.DrawRectangle(PreviewBrush, new Pen(Brushes.White, 1), rect, radius, radius);
            Number(context, rect, number.ToString(CultureInfo.InvariantCulture), dark: false, italic: true);
        }
    }

    // ---- the pointer

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Editor is not { } editor || _pressed) return;
        var point = e.GetCurrentPoint(this);
        var (x, y) = CellAt(point.Position, clamp: false);
        if (!editor.Markup.Contains(x, y)) return;
        Focus();

        GestureKeys keys;
        if (point.Properties.IsRightButtonPressed) keys = GestureKeys.Erase;
        else if (point.Properties.IsLeftButtonPressed)
        {
            keys = GestureKeys.None;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keys |= GestureKeys.Shift;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) keys |= GestureKeys.Ctrl;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) keys |= GestureKeys.Alt;
        }
        else return;

        e.Handled = true;
        // The first click of a double click has placed the LED already; the second asks where to move it.
        if (e.ClickCount == 2 && keys == GestureKeys.None)
        {
            editor.BeginTransfer(x, y);
            return;
        }

        editor.Press(x, y, keys);
        if (editor.Gesture == Gesture.None) return;
        _pressed = true;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Editor is not { } editor) return;
        // With the button down the pointer may leave the grid: the nearest cell goes on counting.
        var (x, y) = CellAt(e.GetPosition(this), clamp: _pressed);
        editor.Move(x, y);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_pressed) Editor?.Hover(-1, -1);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed || Editor is not { } editor) return;
        _pressed = false;
        var (x, y) = CellAt(e.GetPosition(this), clamp: true);
        editor.Release(x, y);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // Lost from outside (another window came up): painting stays, shapes still in the making are dropped.
        if (!_pressed || Editor is not { } editor) return;
        _pressed = false;
        if (editor.Gesture is Gesture.Paint or Gesture.Erase) editor.Release(-1, -1);
        else editor.CancelGesture();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var primary = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.KeyModifiers != primary)
        {
            base.OnPointerWheelChanged(e);
            return;
        }
        Zoom *= Math.Pow(1.15, e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Editor is not { } editor) return;
        var primary = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        bool command = e.KeyModifiers.HasFlag(primary);
        if (e.Key == Key.Escape)
        {
            if (editor.IsAskingToLeave) editor.KeepEditingCommand.Execute(null);
            else if (editor.CancelGesture()) { }   // the release that follows has nothing left to do
            else if (editor.IsTransferring) editor.CancelTransferCommand.Execute(null);
            else editor.RequestClose();
        }
        else if (command && e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)) editor.UndoCommand.Execute(null);
        else if (command && (e.Key == Key.Y || (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))) editor.RedoCommand.Execute(null);
        else if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Delete or Key.Back && editor.DeleteRangeCommand.CanExecute(null)) editor.DeleteRangeCommand.Execute(null);
        else if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.R && editor.ReverseRangeCommand.CanExecute(null)) editor.ReverseRangeCommand.Execute(null);
        else return;
        e.Handled = true;
    }
}
