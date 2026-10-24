using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Layout;

namespace PixelTuner.UI.LedLayouts;

/// <summary>
/// The drawing of the props with the markup state of each outline (PixelTuner 2's canvas pane). It draws <see cref="CanvasPanelViewModel"/> and
/// turns the pointer into points of the drawing; what is picked is decided by <see cref="CanvasDrawing.HitTest"/>.
/// </summary>
public sealed class CanvasDrawingControl : Control
{
    public static readonly StyledProperty<CanvasPanelViewModel?> PanelProperty =
        AvaloniaProperty.Register<CanvasDrawingControl, CanvasPanelViewModel?>(nameof(Panel));

    // Transparent: the page shows the window behind it. (Only its outline is drawn; the fill stays so the colour is one edit away.)
    private static readonly IBrush Page = new SolidColorBrush(Color.FromArgb(0, 0x2B, 0x2B, 0x2B));
    private static readonly IBrush Unmarked = new SolidColorBrush(Colors.White, 0.16);
    private static readonly IBrush Marked = new SolidColorBrush(Color.FromRgb(0x33, 0xAA, 0xFF), 0.34);
    private static readonly IBrush Stroke = new SolidColorBrush(Colors.White, 0.7);
    private static readonly IBrush SelectedStroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x57));

    // A press on an outline may turn into a drag onto a video timeline once the pointer has moved this far.
    private const double DragDistance = 6;
    private PointerPressedEventArgs? _pressed;
    private Point _pressedAt;
    private bool _dragging;

    private readonly Dictionary<string, Geometry?> _geometries = [];
    private CanvasDrawing? _geometriesOf;

    static CanvasDrawingControl() => AffectsRender<CanvasDrawingControl>(PanelProperty);

    public CanvasDrawingControl() => ClipToBounds = true;

    public CanvasPanelViewModel? Panel
    {
        get => GetValue(PanelProperty);
        set => SetValue(PanelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != PanelProperty) return;
        if (change.OldValue is CanvasPanelViewModel old) old.Changed -= Redrawn;
        if (change.NewValue is CanvasPanelViewModel panel) panel.Changed += Redrawn;
    }

    private void Redrawn(object? sender, EventArgs e) => InvalidateVisual();

    // The page is drawn as large as it fits, in its own proportions, centred.
    private (double Scale, Point Origin, (double X, double Y, double Width, double Height) Page)? Fit()
    {
        if (Panel is not { HasDrawing: true } panel) return null;
        var page = panel.Page;
        if (page.Width <= 0 || page.Height <= 0 || Bounds.Width < 1 || Bounds.Height < 1) return null;
        double scale = Math.Min(Bounds.Width / page.Width, Bounds.Height / page.Height);
        return (scale, new Point((Bounds.Width - page.Width * scale) / 2, (Bounds.Height - page.Height * scale) / 2), page);
    }

    private Geometry? GeometryOf(CanvasShape shape)
    {
        if (!ReferenceEquals(_geometriesOf, Panel?.Drawing))
        {
            _geometries.Clear();
            _geometriesOf = Panel?.Drawing;
        }
        if (!_geometries.TryGetValue(shape.Id, out var geometry))
        {
            try
            {
                geometry = Geometry.Parse(shape.PathData);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                geometry = null;
            }
            _geometries[shape.Id] = geometry;
        }
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Fit() is not var (scale, origin, page) || Panel is not { } panel) return;
        context.FillRectangle(Page, new Rect(origin.X, origin.Y, page.Width * scale, page.Height * scale));

        var toScreen = Matrix.CreateTranslation(-page.X, -page.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y);
        foreach (var element in panel.Elements)
        {
            if (GeometryOf(element.Shape) is not { } geometry) continue;
            var t = element.Shape.Transform;
            bool selected = ReferenceEquals(panel.Selected?.Shape, element.Shape);
            using (context.PushTransform(new Matrix(t.A, t.B, t.C, t.D, t.E, t.F) * toScreen))
            {
                // The pen is in drawing units: a line that is the same on screen at every zoom.
                var pen = new Pen(selected ? SelectedStroke : Stroke, (selected ? 2.5 : 1) / scale);
                context.DrawGeometry(element.HasMarkup ? Marked : Unmarked, pen, geometry);
            }
            if (element.HasMarkup) DrawLeds(context, element, toScreen, scale, panel.PlayingColours(element));
        }
    }

    // The LEDs of a marked outline as dots over it, coloured by number (or by the video that plays on it), so the state of the markup can be seen without opening it.
    private static void DrawLeds(DrawingContext context, CanvasElementViewModel element, Matrix toScreen, double scale, IReadOnlyList<Rgb>? playing)
    {
        var layout = element.Layout!;
        var (bx, by, bw, bh) = element.Shape.Bounds;
        if (layout.Columns < 1 || layout.Rows < 1 || bw <= 0 || bh <= 0) return;
        double radius = Math.Max(1, Math.Min(bw / layout.Columns, bh / layout.Rows) * scale * 0.3);
        for (int i = 0; i < layout.Leds.Count; i++)
        {
            int cell = layout.Leds[i];
            if (cell < 0 || cell >= layout.Columns * layout.Rows) continue;
            var point = toScreen.Transform(new Point(bx + (cell % layout.Columns + 0.5) * bw / layout.Columns, by + (cell / layout.Columns + 0.5) * bh / layout.Rows));
            // A video that plays on the outline fills it; without one the LEDs are coloured by their number.
            var colour = playing is not null ? (i < playing.Count ? Color.FromRgb(playing[i].R, playing[i].G, playing[i].B) : Colors.Black) : LedMarkupCanvas.GradientColour(i + 1, layout.Leds.Count);
            context.DrawEllipse(new SolidColorBrush(colour), null, point, radius, radius);
        }
    }

    // A point of the control in drawing units.
    private (double X, double Y)? ToDrawing(Point point)
    {
        if (Fit() is not var (scale, origin, page)) return null;
        return ((point.X - origin.X) / scale + page.X, (point.Y - origin.Y) / scale + page.Y);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Panel is not { } panel || ToDrawing(e.GetPosition(this)) is not var (x, y)) return;
        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed) return;
        panel.SelectAt(x, y);
        // A double click opens the markup of the outline; the right button only selects, for the menu.
        if (e.ClickCount == 2 && properties.IsLeftButtonPressed && panel.Selected is { } element) panel.Edit(element);
        else if (properties.IsLeftButtonPressed && panel.Selected is not null)
        {
            _pressed = e;
            _pressedAt = e.GetPosition(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _pressed = null;
    }

    // The outline is dragged onto a video timeline to give it its layout, as in PixelTuner 2.
    private async Task StartDragAsync(CanvasPanelViewModel panel, PointerPressedEventArgs pressed)
    {
        if (panel.Selected is not { Layout: { } layout } || !panel.Selected.HasMarkup)
        {
            panel.RefuseDrag();
            return;
        }
        var item = DataTransferItem.Create(LayoutDrag.Format, layout.Id.ToString());
        var transfer = new DataTransfer();
        transfer.Add(item);
        _dragging = true;
        try
        {
            await DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Link);
        }
        finally
        {
            _dragging = false;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Panel is not { } panel) return;
        var position = e.GetPosition(this);
        if (_pressed is { } pressed && !_dragging && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && Math.Abs(position.X - _pressedAt.X) + Math.Abs(position.Y - _pressedAt.Y) > DragDistance)
        {
            _pressed = null;
            _ = StartDragAsync(panel, pressed);
            return;
        }
        if (ToDrawing(position) is var (x, y)) panel.Hover(x, y);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (Panel is { } panel) panel.HoverText = "";
    }
}
