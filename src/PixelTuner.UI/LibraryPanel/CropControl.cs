using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PixelRect = PixelTuner.Core.Imaging.PixelRect;

namespace PixelTuner.UI.LibraryPanel;

/// <summary>
/// The picture being imported with the crop rectangle over it. Drags are worked out by <see cref="CropGeometry"/>;
/// this only draws and turns the pointer into picture pixels.
/// </summary>
public sealed class CropControl : Control
{
    public static readonly StyledProperty<Avalonia.Media.Imaging.Bitmap?> SourceProperty =
        AvaloniaProperty.Register<CropControl, Avalonia.Media.Imaging.Bitmap?>(nameof(Source));

    public static readonly StyledProperty<CropRect?> CropProperty =
        AvaloniaProperty.Register<CropControl, CropRect?>(nameof(Crop));

    private const double GrabScreenPixels = 9;

    private CropHandle _handle;
    private PixelRect _start;
    private (double X, double Y) _down;

    static CropControl()
    {
        AffectsRender<CropControl>(SourceProperty, CropProperty);
        AffectsMeasure<CropControl>(SourceProperty);
    }

    /// <summary>The picture to cut.</summary>
    public Avalonia.Media.Imaging.Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public CropRect? Crop
    {
        get => GetValue(CropProperty);
        set => SetValue(CropProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != CropProperty) return;
        if (change.OldValue is CropRect old) old.PropertyChanged -= Changed;
        if (change.NewValue is CropRect crop) crop.PropertyChanged += Changed;
    }

    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvalidateVisual();

    // The picture is drawn as large as fits, in its own proportions, centred.
    private (Rect Area, double Scale) Layout()
    {
        if (Source is not { } source || source.PixelSize.Width == 0) return (default, 1);
        double scale = Math.Min(Bounds.Width / source.PixelSize.Width, Bounds.Height / source.PixelSize.Height);
        var size = new Size(source.PixelSize.Width * scale, source.PixelSize.Height * scale);
        return (new Rect((Bounds.Width - size.Width) / 2, (Bounds.Height - size.Height) / 2, size.Width, size.Height), scale);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Source is not { } source) return default;
        var picture = source.PixelSize;
        double width = double.IsInfinity(availableSize.Width) ? picture.Width : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? picture.Height : availableSize.Height;
        double scale = Math.Min(width / picture.Width, height / picture.Height);
        return new Size(picture.Width * scale, picture.Height * scale);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Source is not { } bitmap || Crop is not { } crop) return;
        var (area, scale) = Layout();
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = scale >= 2 ? Avalonia.Media.Imaging.BitmapInterpolationMode.None : Avalonia.Media.Imaging.BitmapInterpolationMode.MediumQuality }))
            context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), area);

        var rect = new Rect(area.X + crop.X * scale, area.Y + crop.Y * scale, crop.Width * scale, crop.Height * scale);
        // Dim what is cut away: four rectangles around the crop.
        var dim = new SolidColorBrush(Colors.Black, 0.55);
        context.FillRectangle(dim, new Rect(area.X, area.Y, area.Width, rect.Y - area.Y));
        context.FillRectangle(dim, new Rect(area.X, rect.Bottom, area.Width, area.Bottom - rect.Bottom));
        context.FillRectangle(dim, new Rect(area.X, rect.Y, rect.X - area.X, rect.Height));
        context.FillRectangle(dim, new Rect(rect.Right, rect.Y, area.Right - rect.Right, rect.Height));
        context.DrawRectangle(null, new Pen(Brushes.White, 2), rect);
        foreach (var corner in new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight })
            context.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 1), new Rect(corner.X - 4, corner.Y - 4, 8, 8));
    }

    private (double X, double Y) ToPicture(Point p)
    {
        var (area, scale) = Layout();
        return ((p.X - area.X) / scale, (p.Y - area.Y) / scale);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Crop is not { } crop || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _down = ToPicture(e.GetPosition(this));
        _start = crop.Value;
        _handle = CropGeometry.HitTest(_start, _down.X, _down.Y, GrabScreenPixels / Layout().Scale);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Crop is not { } crop || Source is not { } source) return;
        var p = ToPicture(e.GetPosition(this));
        if (_handle == CropHandle.None)
        {
            Cursor = CropGeometry.HitTest(crop.Value, p.X, p.Y, GrabScreenPixels / Layout().Scale) switch
            {
                CropHandle.TopLeft or CropHandle.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
                CropHandle.TopRight or CropHandle.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
                CropHandle.Move => new Cursor(StandardCursorType.SizeAll),
                _ => new Cursor(StandardCursorType.Cross),
            };
            return;
        }
        var rect = CropGeometry.Drag(_handle, _start, _down.X, _down.Y, p.X, p.Y, source.PixelSize.Width, source.PixelSize.Height);
        crop.Set(rect.X, rect.Y, rect.Width, rect.Height);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _handle = CropHandle.None;
        e.Pointer.Capture(null);
    }
}
