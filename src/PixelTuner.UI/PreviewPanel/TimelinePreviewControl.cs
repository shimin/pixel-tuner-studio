using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Preview;

namespace PixelTuner.UI.PreviewPanel;

/// <summary>
/// Draws <see cref="PreviewViewModel"/>: what the selected timeline plays at the playhead, as its device lights it. A poi shows its picture
/// painted in the air, a hoop a ring with an afterglow, a video module its frame on the markup of the prop, E-Pic and LightMaster their
/// colours. What is shown is worked out by <see cref="TimelinePreview"/>; this only draws it.
/// </summary>
public sealed class TimelinePreviewControl : Control
{
    public static readonly StyledProperty<PreviewViewModel?> PreviewProperty =
        AvaloniaProperty.Register<TimelinePreviewControl, PreviewViewModel?>(nameof(Preview));

    private static readonly IBrush Dark = new SolidColorBrush(Color.FromRgb(0x08, 0x08, 0x0B));
    private static readonly IPen Hairline = new Pen(new SolidColorBrush(Colors.White, 0.35), 1);
    private static readonly IBrush Marker = new SolidColorBrush(Colors.White, 0.9);

    private static readonly ConditionalWeakTable<PreviewPicture, Bitmap> s_bitmaps = [];
    private Geometry? _outline;
    private object? _outlineSource;

    static TimelinePreviewControl() => AffectsRender<TimelinePreviewControl>(PreviewProperty);

    public TimelinePreviewControl() => ClipToBounds = true;

    public PreviewViewModel? Preview
    {
        get => GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != PreviewProperty) return;
        if (change.OldValue is PreviewViewModel old) old.Changed -= Redrawn;
        if (change.NewValue is PreviewViewModel preview) preview.Changed += Redrawn;
    }

    private void Redrawn(object? sender, EventArgs e) => InvalidateVisual();

    private static Color ColourOf(Rgb colour, double alpha = 1) => Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), colour.R, colour.G, colour.B);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Dark, new Rect(Bounds.Size));
        if (Preview is not { } preview || preview.Frame is not { } frame) return;
        switch (preview.Mode)
        {
            case PreviewMode.Poi: DrawPoi(context, preview, frame); break;
            case PreviewMode.Hoop: DrawHoop(context, preview, frame); break;
            case PreviewMode.Video: DrawVideo(context, preview, frame); break;
            case PreviewMode.Colours: DrawColours(context, frame); break;
        }
    }

    // ---- a poi: the picture painted in the air

    private static Bitmap BitmapOf(PreviewPicture picture) =>
        s_bitmaps.GetValue(picture, p =>
        {
            var bitmap = new WriteableBitmap(new PixelSize(p.Width, p.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
            using var buffer = bitmap.Lock();
            var row = new byte[p.Width * 4];
            for (int y = 0; y < p.Height; y++)
            {
                for (int x = 0; x < p.Width; x++)
                {
                    var c = p.At(x, y);
                    row[x * 4] = c.R;
                    row[x * 4 + 1] = c.G;
                    row[x * 4 + 2] = c.B;
                    row[x * 4 + 3] = 255;
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
            }
            return bitmap;
        });

    // The device is swung at a steady speed, so a column that is held for t takes swing × t of the air, and the picture comes round again
    // after a pass. The LEDs are a line of the pitch given; the whole line is fitted into the height.
    private void DrawPoi(DrawingContext context, PreviewViewModel preview, PreviewFrame frame)
    {
        if (frame.Picture is not { } picture || frame.ColumnTime <= TimeSpan.Zero) return;
        double heightMm = picture.Height * preview.LedPitch;
        double margin = 12, scale = Math.Max(0.0001, (Bounds.Height - 2 * margin) / heightMm);
        double columnPx = preview.SwingSpeed * frame.ColumnTime.TotalMilliseconds * scale, passPx = columnPx * picture.Width;
        double top = margin, heightPx = picture.Height * preview.LedPitch * scale;
        if (passPx < 1) passPx = 1;

        var brush = new ImageBrush(BitmapOf(picture))
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
            DestinationRect = new RelativeRect(0, 0, passPx, heightPx, RelativeUnit.Absolute),
        };
        var mode = columnPx >= 1 ? BitmapInterpolationMode.None : BitmapInterpolationMode.MediumQuality;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = mode }))
        using (context.PushTransform(Matrix.CreateTranslation(0, top)))
            context.DrawRectangle(brush, null, new Rect(0, 0, Bounds.Width, heightPx));

        // The column that is lit now, in the first pass; at the speed of the device it is gone in a blink, which is why the picture is shown whole.
        if (columnPx >= 1)
            context.DrawRectangle(Marker, null, new Rect(frame.Column * columnPx, top - 6, Math.Max(1, columnPx), heightPx + 12));
        if (frame.Laser) context.DrawEllipse(Brushes.Red, null, new Point(Bounds.Width - 14, 14), 5, 5);
    }

    // ---- a hoop: a ring that moves on an orbit and leaves an afterglow

    private const int MaxTrail = 48;

    private void DrawHoop(DrawingContext context, PreviewViewModel preview, PreviewFrame frame)
    {
        if (preview.Current is not { } timeline) return;
        double side = Math.Min(Bounds.Width, Bounds.Height);
        double ring = side * 0.27, orbit = side * 0.17;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var time = preview.Frame!.Time;
        var trail = timeline.Trail(time, TimeSpan.FromMilliseconds(preview.Tail), MaxTrail);
        double fade = Math.Max(1, preview.Tail) / 3;

        // Oldest first, so the newest ring is on top.
        for (int i = trail.Count - 1; i >= 0; i--)
        {
            var (age, colours) = trail[i];
            double alpha = Math.Exp(-age.TotalMilliseconds / fade);
            double phase = 2 * Math.PI * preview.OrbitSpeed * (time - age).TotalSeconds;
            var at = new Point(centre.X + orbit * Math.Cos(phase), centre.Y + orbit * Math.Sin(phase));
            double dot = Math.Max(1.5, ring * 2 * Math.PI / Math.Max(1, colours.Count) * 0.38);
            for (int led = 0; led < colours.Count; led++)
            {
                if (colours[led] == Rgb.Black) continue;
                double angle = 2 * Math.PI * led / colours.Count - Math.PI / 2;
                context.DrawEllipse(new SolidColorBrush(ColourOf(colours[led], alpha)), null,
                    new Point(at.X + ring * Math.Cos(angle), at.Y + ring * Math.Sin(angle)), dot, dot);
            }
        }
        if (frame.Laser) context.DrawEllipse(Brushes.Red, null, new Point(Bounds.Width - 14, 14), 5, 5);
    }

    // ---- video: the frame on the markup of the prop

    private void DrawVideo(DrawingContext context, PreviewViewModel preview, PreviewFrame frame)
    {
        if (preview.Layout is not { Columns: > 0, Rows: > 0 } layout) return;
        var positions = TimelinePreview.Positions(layout);
        var outline = preview.Outline;
        var box = outline?.Bounds is { Width: > 0, Height: > 0 } b ? b : default((double X, double Y, double Width, double Height)?);

        // The grid is stretched over the outline, as in the markup window.
        double aspect = box is { } bb ? bb.Width / bb.Height : (double)layout.Columns / layout.Rows;
        double margin = 12, fitW = Bounds.Width - 2 * margin, fitH = Bounds.Height - 2 * margin;
        double width = fitW / fitH > aspect ? fitH * aspect : fitW, height = width / aspect;
        var origin = new Point((Bounds.Width - width) / 2, (Bounds.Height - height) / 2);
        double cw = width / layout.Columns, ch = height / layout.Rows;

        if (outline is not null && box is { } ob)
        {
            if (!ReferenceEquals(_outlineSource, outline))
            {
                _outlineSource = outline;
                try { _outline = Geometry.Parse(outline.PathData); }
                catch (Exception ex) when (ex is FormatException or ArgumentException) { _outline = null; }
            }
            if (_outline is not null)
            {
                var t = outline.Transform;
                var matrix = new Matrix(t.A, t.B, t.C, t.D, t.E, t.F) * Matrix.CreateTranslation(-ob.X, -ob.Y)
                             * Matrix.CreateScale(width / ob.Width, height / ob.Height) * Matrix.CreateTranslation(origin.X, origin.Y);
                using (context.PushTransform(matrix))
                    context.DrawGeometry(new SolidColorBrush(Colors.White, 0.08), new Pen(new SolidColorBrush(Colors.White, 0.5), 1.0 / Math.Max(0.0001, Math.Min(width / ob.Width, height / ob.Height))), _outline);
            }
        }
        else context.DrawRectangle(null, Hairline, new Rect(origin, new Size(width, height)));

        double radius = Math.Max(1.5, Math.Min(cw, ch) * 0.42);
        for (int led = 0; led < positions.Count && led < frame.Colors.Count; led++)
        {
            var (x, y) = positions[led];
            context.DrawEllipse(new SolidColorBrush(ColourOf(frame.Colors[led])), null, new Point(origin.X + (x + 0.5) * cw, origin.Y + (y + 0.5) * ch), radius, radius);
        }
    }

    // ---- colours: E-Pic and LightMaster

    private void DrawColours(DrawingContext context, PreviewFrame frame)
    {
        int count = Math.Max(1, frame.Colors.Count);
        double margin = 16, gap = 10;
        double size = Math.Min((Bounds.Width - 2 * margin - (count - 1) * gap) / count, Bounds.Height - 2 * margin);
        if (size < 4) return;
        double left = (Bounds.Width - (count * size + (count - 1) * gap)) / 2, top = (Bounds.Height - size) / 2;
        for (int i = 0; i < count; i++)
        {
            var rect = new Rect(left + i * (size + gap), top, size, size);
            var colour = i < frame.Colors.Count ? frame.Colors[i] : Rgb.Black;
            context.DrawRectangle(new SolidColorBrush(ColourOf(colour)), Hairline, rect, size * 0.12, size * 0.12);
        }
    }
}
