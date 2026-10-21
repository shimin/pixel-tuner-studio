using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PixelTuner.Core.Model;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.Timelines;

/// <summary>
/// Draws a <see cref="TimelineEditor"/> and forwards the pointer to it: the ruler with markers and
/// the playhead, one row per timeline with its items. Ctrl and the wheel zoom, Shift and the
/// wheel (or a sideways wheel) scroll in time, the plain wheel scrolls the rows.
/// </summary>
public sealed class TimelineControl : Control
{
    public static readonly StyledProperty<TimelineEditor?> EditorProperty =
        AvaloniaProperty.Register<TimelineControl, TimelineEditor?>(nameof(Editor));

    private int _rows = -1;
    private DispatcherTimer? _timer;

    static TimelineControl()
    {
        AffectsRender<TimelineControl>(EditorProperty);
        FocusableProperty.OverrideDefaultValue<TimelineControl>(true);
    }

    public TimelineEditor? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != EditorProperty) return;
        if (change.OldValue is TimelineEditor old) old.Changed -= EditorChanged;
        if (change.NewValue is TimelineEditor editor) editor.Changed += EditorChanged;
        InvalidateMeasure();
        InvalidateVisual();
    }

    // Saving reports from a pool thread; drawing belongs to the UI thread.
    private void EditorChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => EditorChanged(sender, e));
            return;
        }
        if (Editor is { } editor && editor.Project.Timelines.Count != _rows) InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The playhead follows the sound about thirty times a second; the editor redraws only on a real change.
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => Editor?.Tick());
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Editor is not { } editor || e.KeyModifiers != KeyModifiers.None) return;
        switch (e.Key)
        {
            case Key.Space:
                editor.PlayPause();
                e.Handled = true;
                break;
            case Key.Home:
                editor.SetPlayhead(TimeSpan.Zero);
                editor.ScrollTo(TimeSpan.Zero);
                e.Handled = true;
                break;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _rows = Editor?.Project.Timelines.Count ?? 0;
        double width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        return new Size(width, Editor?.TotalHeight ?? TimelineEditor.RulerHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Editor is { } editor) editor.ViewWidth = finalSize.Width;
        return base.ArrangeOverride(finalSize);
    }

    // ---- input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Editor is not { } editor || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        var p = e.GetPosition(this);
        editor.PointerDown(p.X, p.Y);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Editor is not { } editor) return;
        var p = e.GetPosition(this);
        if (editor.IsDragging)
        {
            editor.PointerMove(p.X, p.Y);
            return;
        }
        Cursor = editor.HitTest(p.X, p.Y).Kind switch
        {
            HitKind.ItemStart or HitKind.ItemEnd => new Cursor(StandardCursorType.SizeWestEast),
            HitKind.Marker => new Cursor(StandardCursorType.SizeWestEast),
            HitKind.Item => new Cursor(StandardCursorType.Hand),
            HitKind.Ruler or HitKind.Wave => new Cursor(StandardCursorType.Ibeam),
            _ => Cursor.Default,
        };
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Editor is not { } editor || !editor.IsDragging) return;
        var up = e.GetPosition(this);
        editor.PointerUp(up.X, up.Y);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Editor?.CancelDrag();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Editor is not { } editor) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            editor.ZoomAt(Math.Exp(e.Delta.Y * 0.15), e.GetPosition(this).X);
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.Delta.X != 0)
        {
            double delta = e.Delta.X != 0 ? e.Delta.X : e.Delta.Y;
            editor.ScrollBy(-delta * 80);
            e.Handled = true;
        }
    }

    // ---- drawing

    private IBrush ThemeBrush(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush ? brush : new SolidColorBrush(fallback);

    private static Color ParseColor(string? text, Color fallback) => Color.TryParse(text, out var c) ? c : fallback;

    private FormattedText Text(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Editor is not { } editor) return;

        var width = Bounds.Width;
        var surface = ThemeBrush("PtSurfaceBrush", Colors.White);
        var card = ThemeBrush("PtCardBrush", Colors.White);
        var muted = ThemeBrush("PtMutedTextBrush", Colors.Gray);
        var text = ThemeBrush("SystemControlForegroundBaseHighBrush", Colors.Black);
        var accent = ThemeBrush("SystemControlHighlightAccentBrush", Colors.DodgerBlue);
        var line = new Pen(muted, 1) { DashStyle = null };
        var faint = new Pen(new SolidColorBrush(((ISolidColorBrush)muted).Color, 0.25), 1);

        context.FillRectangle(surface, new Rect(0, 0, width, Bounds.Height));

        DrawWaveHeader(context, editor, card, muted, text, faint);

        // Rows: a header with the name, and a lane for the items.
        for (int i = 0; i < editor.Project.Timelines.Count; i++)
        {
            var timeline = editor.Project.Timelines[i];
            double top = editor.TrackTop(i);
            var row = new Rect(0, top, width, TimelineEditor.TrackHeight);
            context.FillRectangle(i % 2 == 0 ? card : surface, row);
            context.DrawLine(faint, new Point(0, top + TimelineEditor.TrackHeight), new Point(width, top + TimelineEditor.TrackHeight));

            var color = ParseColor(timeline.Color, Colors.DodgerBlue);
            context.FillRectangle(new SolidColorBrush(color), new Rect(0, top, 4, TimelineEditor.TrackHeight));
            if (ReferenceEquals(editor.Selected, timeline))
                context.DrawRectangle(null, new Pen(accent, 2), new Rect(1, top + 1, TimelineEditor.HeaderWidth - 2, TimelineEditor.TrackHeight - 2));
            DrawClipped(context, new Rect(12, top, TimelineEditor.HeaderWidth - 18, TimelineEditor.TrackHeight), () =>
            {
                context.DrawText(Text(timeline.Name, 14, text, FontWeight.SemiBold), new Point(12, top + 10));
                context.DrawText(Text(KindLabel(timeline.Kind), 12, muted), new Point(12, top + 32));
            });
        }

        // Everything in the time area is clipped, so items slide under the name column.
        var timeArea = new Rect(TimelineEditor.HeaderWidth, 0, Math.Max(0, width - TimelineEditor.HeaderWidth), Bounds.Height);
        using (context.PushClip(timeArea))
        {
            DrawRulerAndGrid(context, editor, timeArea, card, muted, text, line, faint);

            DrawWave(context, editor, timeArea, card, accent, faint);

            for (int i = 0; i < editor.Project.Timelines.Count; i++)
                DrawPauses(context, editor, editor.Project.Timelines[i], editor.TrackTop(i), faint);

            for (int i = 0; i < editor.Project.Timelines.Count; i++)
                foreach (var item in editor.Project.Timelines[i].Items)
                    DrawItem(context, editor, editor.Project.Timelines[i], item, editor.TrackTop(i), text, accent);

            DrawMarkers(context, editor, accent, text);
            DrawPlayhead(context, editor, accent);
            DrawDenial(context, editor, timeArea);
        }

        context.DrawLine(new Pen(muted, 1), new Point(0, TimelineEditor.RulerHeight), new Point(width, TimelineEditor.RulerHeight));
        context.DrawLine(faint, new Point(TimelineEditor.HeaderWidth, 0), new Point(TimelineEditor.HeaderWidth, Bounds.Height));
    }

    /// <summary>A timeline that will not take the dragged item is tinted red and says why.</summary>
    private void DrawDenial(DrawingContext context, TimelineEditor editor, Rect timeArea)
    {
        if (editor.Preview?.Denial is not { } denial) return;
        int row = editor.Project.Timelines.IndexOf(denial.Target);
        if (row < 0) return;
        double top = editor.TrackTop(row);
        var red = Color.FromRgb(209, 73, 91);
        context.FillRectangle(new SolidColorBrush(red, 0.18), new Rect(timeArea.X, top, timeArea.Width, TimelineEditor.TrackHeight));
        context.DrawRectangle(null, new Pen(new SolidColorBrush(red), 2), new Rect(timeArea.X + 1, top + 1, timeArea.Width - 2, TimelineEditor.TrackHeight - 2));

        var label = Text(denial.Text(), 12, Brushes.White, FontWeight.SemiBold);
        double x = timeArea.X + 10, y = top + (TimelineEditor.TrackHeight - label.Height) / 2;
        context.DrawRectangle(new SolidColorBrush(red), null, new Rect(x - 6, y - 3, label.Width + 12, label.Height + 6), 4, 4);
        context.DrawText(label, new Point(x, y));
    }

    private void DrawWaveHeader(DrawingContext context, TimelineEditor editor, IBrush card, IBrush muted, IBrush text, Pen faint)
    {
        double top = TimelineEditor.RulerHeight;
        context.FillRectangle(card, new Rect(0, top, Bounds.Width, TimelineEditor.WaveHeight));
        context.DrawLine(faint, new Point(0, top + TimelineEditor.WaveHeight), new Point(Bounds.Width, top + TimelineEditor.WaveHeight));
        DrawClipped(context, new Rect(12, top, TimelineEditor.HeaderWidth - 18, TimelineEditor.WaveHeight), () =>
        {
            context.DrawText(Text("♪ " + Strings.Soundtrack_Label, 14, text, FontWeight.SemiBold), new Point(12, top + 10));
            string sub = editor.Project.AudioResourceId is null
                ? Strings.Soundtrack_None
                : System.IO.Path.GetFileName((editor.Project.AudioSourcePath ?? "").Replace('\\', '/'));
            context.DrawText(Text(sub, 12, muted), new Point(12, top + 32));
        });
    }

    private void DrawWave(DrawingContext context, TimelineEditor editor, Rect timeArea, IBrush card, IBrush accent, Pen faint)
    {
        double top = TimelineEditor.RulerHeight, height = TimelineEditor.WaveHeight;
        context.FillRectangle(card, new Rect(timeArea.X, top, timeArea.Width, height));
        double middle = top + height / 2, amplitude = height / 2 - 6;
        context.DrawLine(faint, new Point(timeArea.X, middle), new Point(timeArea.Right, middle));
        if (editor.Wave is not { } wave) return;

        var pen = new Pen(new SolidColorBrush(((ISolidColorBrush)accent).Color, 0.75), 1);
        double endX = editor.TimeToX(wave.Duration);
        // One column of pixels at a time: the lowest and highest sample under it, at any zoom.
        for (double x = Math.Max(timeArea.X, editor.TimeToX(TimeSpan.Zero)); x < Math.Min(timeArea.Right, endX); x++)
        {
            var (min, max) = wave.Range(editor.XToTime(x), editor.XToTime(x + 1));
            double y1 = middle - Math.Clamp(max, -1, 1) * amplitude, y2 = middle - Math.Clamp(min, -1, 1) * amplitude;
            context.DrawLine(pen, new Point(x + 0.5, y1), new Point(x + 0.5, Math.Max(y2, y1 + 1)));
        }
    }

    private static void DrawClipped(DrawingContext context, Rect clip, Action draw)
    {
        using (context.PushClip(clip)) draw();
    }

    private static string KindLabel(string kind) => kind switch
    {
        TimelineKind.Video => Strings.Kind_Video,
        TimelineKind.EpicPoi => Strings.Kind_EpicPoi,
        TimelineKind.Light => Strings.Kind_Light,
        _ => Strings.Kind_Pixel,
    };

    private void DrawRulerAndGrid(DrawingContext context, TimelineEditor editor, Rect area, IBrush card, IBrush muted, IBrush text, Pen line, Pen faint)
    {
        context.FillRectangle(card, new Rect(area.X, 0, area.Width, TimelineEditor.RulerHeight));
        double step = TimeFormat.RulerStep(editor.PixelsPerSecond);
        double minor = step / 5 * editor.PixelsPerSecond >= 14 ? step / 5 : step;
        int perStep = (int)Math.Round(step / minor);
        long firstIndex = Math.Max(0, (long)Math.Floor(editor.XToTime(area.X).TotalSeconds / minor));
        double last = editor.XToTime(area.Right).TotalSeconds;

        // Count ticks by index: adding a fraction repeatedly drifts and mislabels the seconds.
        for (long n = firstIndex; n * minor <= last; n++)
        {
            double t = n * minor;
            double x = editor.TimeToX(TimeSpan.FromSeconds(t));
            bool major = n % perStep == 0;
            context.DrawLine(major ? line : faint, new Point(x, major ? TimelineEditor.RulerHeight - 14 : TimelineEditor.RulerHeight - 7), new Point(x, TimelineEditor.RulerHeight));
            if (major)
            {
                context.DrawText(Text(TimeFormat.Format(TimeSpan.FromSeconds(Math.Round(t, 6)), step), 11, muted), new Point(x + 3, TimelineEditor.RulerHeight - 29));
                context.DrawLine(faint, new Point(x, TimelineEditor.RulerHeight), new Point(x, Bounds.Height));
            }
        }
    }

    /// <summary>The gaps between items are black entries in the .bin; the lane shows them as black stretches.</summary>
    private static void DrawPauses(DrawingContext context, TimelineEditor editor, Timeline timeline, double top, Pen outline)
    {
        foreach (var (start, end) in TimelineEditor.Pauses(timeline))
        {
            double left = editor.TimeToX(start), right = editor.TimeToX(end);
            if (right < 0 || left > editor.ViewWidth) continue;
            var rect = new Rect(left, top + 6, Math.Max(1, right - left), TimelineEditor.TrackHeight - 12);
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 8, 10), 0.9), outline, rect, 4, 4);
        }
    }

    private void DrawItem(DrawingContext context, TimelineEditor editor, Timeline timeline, TimelineItem item, double top, IBrush text, IBrush accent)
    {
        var timelineColor = ParseColor(timeline.Color, Colors.DodgerBlue);
        var fill = item.Kind == ItemKind.Solid ? ParseColor(item.Color, timelineColor) : timelineColor;
        bool beingDragged = editor.Preview is { Item: { } dragged } && ReferenceEquals(dragged, item);

        var thumb = editor.Thumbnails.Get(item, timeline);

        void Draw(TimeSpan start, TimeSpan duration, double opacity, bool outline, double top)
        {
            double left = editor.TimeToX(start), right = editor.TimeToX(start + duration);
            if (right < editor.ViewWidth * -1 || left > editor.ViewWidth) return;
            var rect = new Rect(left, top + 6, Math.Max(2, right - left), TimelineEditor.TrackHeight - 12);
            bool selected = ReferenceEquals(editor.Selected, item);
            context.DrawRectangle(new SolidColorBrush(fill, opacity * 0.85), selected || outline ? new Pen(accent, 2) : new Pen(new SolidColorBrush(fill), 1), rect, 4, 4);

            // The content is clipped to the item: the last pass is cut off where the item ends.
            var inner = rect.Deflate(1);
            using (context.PushClip(inner))
            using (context.PushOpacity(opacity))
            {
                if (item.Kind == ItemKind.Effect) DrawEffect(context, editor, item, inner, left);
                else if (thumb is not null) DrawTiles(context, editor, item, thumb, inner, left, right);
            }

            if (rect.Width > 24)
            {
                var label = Text(ItemLabel(item), 12, new SolidColorBrush(thumb is null ? Contrast(fill) : Colors.White, opacity));
                if (thumb is not null)
                    context.FillRectangle(new SolidColorBrush(Colors.Black, 0.55 * opacity), new Rect(rect.X + 3, rect.Y + 3, Math.Min(label.Width + 8, rect.Width - 6), label.Height + 4), 3);
                DrawClipped(context, rect.Deflate(new Thickness(5, 0)), () => context.DrawText(label, new Point(rect.X + 5, rect.Y + 5)));
            }
        }

        Draw(item.Start, item.Duration, beingDragged ? 0.35 : 1, outline: false, top);
        if (beingDragged)
        {
            // The outline sits on the track the item would land on.
            int row = editor.Project.Timelines.IndexOf(editor.Preview!.Timeline!);
            Draw(editor.Preview.Start, editor.Preview.Duration, 1, outline: true, row < 0 ? top : editor.TrackTop(row));
        }
    }

    /// <summary>
    /// The picture repeated along the item, as the device repeats it from the item's start to its end. A pass is as wide as it
    /// takes to play at the item's speed; when passes are too narrow to tell apart, the average colour is drawn.
    /// </summary>
    private static void DrawTiles(DrawingContext context, TimelineEditor editor, TimelineItem item, Thumbnail thumb, Rect inner, double left, double right)
    {
        var run = editor.TileLayout(left, right, thumb.Pass(item), TimelineEditor.HeaderWidth, editor.ViewWidth);
        if (run.Averaged)
        {
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(thumb.Average.R, thumb.Average.G, thumb.Average.B)), inner);
            return;
        }

        if (thumb.Bitmap is not { } bitmap) return;
        var source = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        // Sharp columns when a pass is wider than its picture, smooth when it is squeezed.
        var mode = run.PassWidth >= thumb.Columns ? BitmapInterpolationMode.None : BitmapInterpolationMode.MediumQuality;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = mode }))
        {
            for (int n = run.FirstPass; n <= run.LastPass; n++)
                context.DrawImage(bitmap, source, new Rect(left + n * run.PassWidth, inner.Y, run.PassWidth, inner.Height));
        }

        if (run.PassWidth < TimelineEditor.PassLineWidth) return;
        var line = new Pen(new SolidColorBrush(Colors.White, 0.7), 1);
        for (int n = Math.Max(1, run.FirstPass); n <= run.LastPass; n++)
        {
            double x = left + n * run.PassWidth;
            if (x < right) context.DrawLine(line, new Point(x, inner.Y), new Point(x, inner.Bottom));
        }
    }

    /// <summary>A light effect as the colours the device will show across it.</summary>
    private static void DrawEffect(DrawingContext context, TimelineEditor editor, TimelineItem item, Rect inner, double left)
    {
        const double Step = 3;
        double from = Math.Max(inner.X, TimelineEditor.HeaderWidth), to = Math.Min(inner.Right, editor.ViewWidth);
        for (double x = from; x < to; x += Step)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, x - left) / editor.PixelsPerSecond);
            try
            {
                var c = PixelTuner.Core.Export.LightExport.ColorAt(item, t);
                context.FillRectangle(new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)), new Rect(x, inner.Y, Step + 0.5, inner.Height));
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException)
            {
                return;   // an effect the export does not know: the plain block stays
            }
        }
    }

    private static Color Contrast(Color background) =>
        (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) > 150 ? Colors.Black : Colors.White;

    private static string ItemLabel(TimelineItem item) => item.Kind switch
    {
        ItemKind.Effect => item.Effect ?? "",
        ItemKind.Solid => item.Color ?? "",
        _ => string.IsNullOrEmpty(item.SourcePath) ? item.Kind : System.IO.Path.GetFileName(item.SourcePath.Replace('\\', '/')),
    };

    private void DrawMarkers(DrawingContext context, TimelineEditor editor, IBrush accent, IBrush text)
    {
        foreach (var marker in editor.Project.Markers)
        {
            var position = editor.Preview is { Marker: { } dragged } && ReferenceEquals(dragged, marker) ? editor.Preview.Start : marker.Position;
            double x = editor.TimeToX(position);
            bool selected = ReferenceEquals(editor.Selected, marker);
            var brush = selected ? accent : new SolidColorBrush(Colors.OrangeRed);
            var flag = new StreamGeometry();
            using (var g = flag.Open())
            {
                g.BeginFigure(new Point(x - 6, 2), true);
                g.LineTo(new Point(x + 6, 2));
                g.LineTo(new Point(x, 14));
                g.EndFigure(true);
            }
            context.DrawGeometry(brush, null, flag);
            context.DrawLine(new Pen(brush, 1), new Point(x, 14), new Point(x, Bounds.Height));
            if (!string.IsNullOrEmpty(marker.Name))
            {
                var label = Text(marker.Name, 11, text);
                // A backing so the name stays readable over the ruler's time labels.
                context.FillRectangle(ThemeBrush("PtCardBrush", Colors.White), new Rect(x + 7, 1, label.Width + 4, label.Height + 1));
                context.DrawText(label, new Point(x + 9, 1));
            }
        }
    }

    private void DrawPlayhead(DrawingContext context, TimelineEditor editor, IBrush accent)
    {
        double x = editor.TimeToX(editor.Playhead);
        var pen = new Pen(accent, 2);
        context.DrawLine(pen, new Point(x, TimelineEditor.RulerHeight - 10), new Point(x, Bounds.Height));
        var cap = new StreamGeometry();
        using (var g = cap.Open())
        {
            g.BeginFigure(new Point(x - 6, TimelineEditor.RulerHeight - 12), true);
            g.LineTo(new Point(x + 6, TimelineEditor.RulerHeight - 12));
            g.LineTo(new Point(x, TimelineEditor.RulerHeight - 2));
            g.EndFigure(true);
        }
        context.DrawGeometry(accent, null, cap);
    }
}
