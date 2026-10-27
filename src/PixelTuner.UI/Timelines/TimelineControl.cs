using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PixelTuner.Core.Model;
using PixelTuner.UI.LedLayouts;
using PixelTuner.UI.LibraryPanel;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.Timelines;

/// <summary>
/// Draws a <see cref="TimelineEditor"/> and forwards the pointer to it: the ruler with markers and
/// the playhead, one row per timeline with its items. Ctrl (Cmd) and the wheel zoom, and so does the plain wheel over the ruler
/// or the sound lane; Shift and the wheel (or a sideways wheel) scroll in time, the plain wheel elsewhere scrolls the rows.
/// </summary>
public sealed class TimelineControl : Control
{
    public static readonly StyledProperty<TimelineEditor?> EditorProperty =
        AvaloniaProperty.Register<TimelineControl, TimelineEditor?>(nameof(Editor));

    private int _rows = -1;
    private DispatcherTimer? _timer;
    private bool _soundtrackHovered;
    private HeaderButton? _hoveredButton;

    public TimelineControl()
    {
        // An LED layout dragged from the canvas panel can be dropped on a video timeline.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnLayoutDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnLayoutDragLeave);
        AddHandler(DragDrop.DropEvent, OnLayoutDrop);
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private LedLayout? DraggedLayout(DragEventArgs e) =>
        Editor is { } editor && e.DataTransfer.TryGetValue(LayoutDrag.Format) is { } id && Guid.TryParse(id, out var guid)
            ? editor.Project.LedLayouts.FirstOrDefault(l => l.Id == guid)
            : null;

    private LibraryEntry? DraggedEntry(DragEventArgs e) =>
        Editor is { } editor && e.DataTransfer.TryGetValue(LibraryDrag.Format) is { } id && Guid.TryParse(id, out var guid) ? editor.FindEntry(guid) : null;

    private void OnLayoutDragOver(object? sender, DragEventArgs e)
    {
        if (Editor is not { } editor) return;
        double y = e.GetPosition(this).Y;
        if (DraggedEntry(e) is { } entry)
        {
            editor.HoverEntry(y, entry);
            e.DragEffects = editor.LayoutDrop is { Allowed: true } ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
            return;
        }
        if (DraggedLayout(e) is null) return;   // not ours: the window decides (a package file)
        editor.HoverLayout(y);
        e.DragEffects = editor.TimelineAt(y) is { Kind: TimelineKind.Video } ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnLayoutDragLeave(object? sender, DragEventArgs e) => Editor?.LeaveLayout();

    private void OnLayoutDrop(object? sender, DragEventArgs e)
    {
        if (Editor is { } dropTo && DraggedEntry(e) is { } entry)
        {
            var at = e.GetPosition(this);
            dropTo.DropEntry(at.X, at.Y, entry);
            e.Handled = true;
            return;
        }
        if (Editor is not { } editor || DraggedLayout(e) is not { } layout) return;
        editor.DropLayout(e.GetPosition(this).Y, layout);
        e.Handled = true;
    }

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
        var hit = editor.HitTest(p.X, p.Y);
        if (hit.Kind == HitKind.SoundtrackButton)
        {
            editor.RaiseSoundtrackButtonClicked();
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (hit is { Kind: HitKind.CornerButton, Button: { } button })
        {
            editor.Press(button);
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        var mode = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta) ? SelectionMode.Toggle
            : e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? SelectionMode.Range : SelectionMode.Replace;
        editor.PointerDown(p.X, p.Y, mode);
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
        var hit = editor.HitTest(p.X, p.Y);
        bool hovered = hit.Kind == HitKind.SoundtrackButton;
        if (hovered != _soundtrackHovered)
        {
            _soundtrackHovered = hovered;
            InvalidateVisual();
        }
        if (hit.Button != _hoveredButton)
        {
            _hoveredButton = hit.Button;
            ToolTip.SetTip(this, _hoveredButton is { } over ? ButtonTip(over) : null);
            InvalidateVisual();
        }
        Cursor = hit.Kind switch
        {
            HitKind.ItemStart or HitKind.ItemEnd => new Cursor(StandardCursorType.SizeWestEast),
            HitKind.Marker => new Cursor(StandardCursorType.SizeWestEast),
            HitKind.Item => new Cursor(StandardCursorType.Hand),
            HitKind.Ruler or HitKind.Wave => new Cursor(StandardCursorType.Ibeam),
            HitKind.SoundtrackButton => new Cursor(StandardCursorType.Hand),
            HitKind.CornerButton => editor.IsEnabled(hit.Button!.Value) ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            _ => Cursor.Default,
        };
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredButton is null && !_soundtrackHovered) return;
        _hoveredButton = null;
        _soundtrackHovered = false;
        ToolTip.SetTip(this, null);
        InvalidateVisual();
    }

    private static string ButtonTip(HeaderButton button) => button switch
    {
        HeaderButton.Delete => Strings.Command_Delete,
        HeaderButton.MoveUp => Strings.Command_MoveUp,
        _ => Strings.Command_MoveDown,
    };

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
        var at = e.GetPosition(this);   // zoom is around the playhead, wherever the pointer is
        bool zoomKey = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        // The plain wheel over the ruler or the sound lane zooms: the rows below scroll with it, and they are not there.
        bool overTime = at.Y < TimelineEditor.TracksTop && e.KeyModifiers == KeyModifiers.None && e.Delta.X == 0;
        if (zoomKey || overTime)
        {
            editor.ZoomOnPlayhead(Math.Exp(e.Delta.Y * 0.15));
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
        // Thin lines: the same weight against the page in both themes (the muted grey is darker against a light page, so it takes less).
        var faint = new Pen(new SolidColorBrush(((ISolidColorBrush)muted).Color, ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? 0.25 : 0.16), 1);

        context.FillRectangle(surface, new Rect(0, 0, width, Bounds.Height));

        DrawWaveHeader(context, editor, card, muted, text, faint, accent);

        // Rows: a header with the name, and a lane for the items.
        for (int i = 0; i < editor.Project.Timelines.Count; i++)
        {
            var timeline = editor.Project.Timelines[i];
            double top = editor.TrackTop(i);
            // No line between rows: two shades that alternate by place (so they follow a timeline that is moved), tinted by kind.
            var row = new Rect(0, top, width, TimelineEditor.TrackHeight);
            context.FillRectangle(new SolidColorBrush(TimelineRows.Background(timeline.Kind, i, ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark)), row);

            var color = ParseColor(timeline.Color, Colors.DodgerBlue);
            context.FillRectangle(new SolidColorBrush(color), new Rect(0, top, 4, TimelineEditor.TrackHeight));
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

            DrawWave(context, editor, timeArea, card, faint);

            for (int i = 0; i < editor.Project.Timelines.Count; i++)
                DrawPauses(context, editor, editor.Project.Timelines[i], editor.TrackTop(i), faint);

            for (int i = 0; i < editor.Project.Timelines.Count; i++)
                foreach (var item in editor.Project.Timelines[i].Items)
                    DrawItem(context, editor, editor.Project.Timelines[i], item, editor.TrackTop(i), text, accent);

            for (int i = 0; i < editor.Project.Timelines.Count; i++)
                foreach (var item in editor.Project.Timelines[i].Items)
                    if (editor.IsSelected(item)) DrawSelectionGlow(context, editor, editor.Project.Timelines[i], item, editor.TrackTop(i));

            DrawMarkers(context, editor, accent, text);
            DrawPlayhead(context, editor, accent);
            DrawDenial(context, editor, timeArea);
            DrawLayoutDrop(context, editor, timeArea);
        }

        // The selected timeline is marked as an item is, along its whole row: the name column and the lane.
        for (int i = 0; i < editor.Project.Timelines.Count; i++)
            if (ReferenceEquals(editor.Selected, editor.Project.Timelines[i]))
                DrawGlow(context, new Rect(0, editor.TrackTop(i), width, TimelineEditor.TrackHeight), 4);

        context.DrawLine(new Pen(new SolidColorBrush(((ISolidColorBrush)muted).Color, 0.15), 1), new Point(0, TimelineEditor.RulerHeight), new Point(width, TimelineEditor.RulerHeight));
        // The divider of the name column stops where the timelines begin: the rows tell themselves apart by colour.
        context.DrawLine(faint, new Point(TimelineEditor.HeaderWidth, 0), new Point(TimelineEditor.HeaderWidth, TimelineEditor.TracksTop));
        DrawCornerButtons(context, editor, muted, text, accent);
    }

    /// <summary>Delete, move up and move down: small buttons in the corner left of the ruler, acting on the selection.</summary>
    private void DrawCornerButtons(DrawingContext context, TimelineEditor editor, IBrush muted, IBrush text, IBrush accent)
    {
        var mutedColour = ((ISolidColorBrush)muted).Color;
        foreach (var button in Enum.GetValues<HeaderButton>())
        {
            var (x, y, w, h) = TimelineEditor.CornerButtonBounds(button);
            bool enabled = editor.IsEnabled(button), hovered = enabled && _hoveredButton == button;
            var fill = new SolidColorBrush(mutedColour, hovered ? 0.24 : 0.1);
            var border = new Pen(hovered ? accent : new SolidColorBrush(mutedColour, 0.45), 1);
            context.DrawRectangle(fill, border, new Rect(x, y, w, h), 6, 6);
            string glyph = button switch { HeaderButton.Delete => "✕", HeaderButton.MoveUp => "↑", _ => "↓" };
            var caption = Text(glyph, 14, enabled ? (hovered ? accent : text) : new SolidColorBrush(mutedColour, 0.45), FontWeight.SemiBold);
            context.DrawText(caption, new Point(x + (w - caption.Width) / 2, y + (h - caption.Height) / 2));
        }
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

    /// <summary>A layout dragged over a timeline: green where it would go, red and a reason where it would not.</summary>
    private void DrawLayoutDrop(DrawingContext context, TimelineEditor editor, Rect timeArea)
    {
        if (editor.LayoutDrop is not { } hint) return;
        int row = editor.Project.Timelines.IndexOf(hint.Target);
        if (row < 0) return;
        double top = editor.TrackTop(row);
        var colour = hint.Allowed ? Color.FromRgb(51, 170, 119) : Color.FromRgb(209, 73, 91);
        var bounds = new Rect(0, top, timeArea.X + timeArea.Width, TimelineEditor.TrackHeight);
        context.FillRectangle(new SolidColorBrush(colour, 0.18), bounds);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(colour), 2), bounds.Deflate(1));
        if (hint.Allowed) return;

        var label = Text(hint.Text(), 12, Brushes.White, FontWeight.SemiBold);
        double x = timeArea.X + 10, y = top + (TimelineEditor.TrackHeight - label.Height) / 2;
        context.DrawRectangle(new SolidColorBrush(colour), null, new Rect(x - 6, y - 3, label.Width + 12, label.Height + 6), 4, 4);
        context.DrawText(label, new Point(x, y));
    }

    private void DrawWaveHeader(DrawingContext context, TimelineEditor editor, IBrush card, IBrush muted, IBrush text, Pen faint, IBrush accent)
    {
        double top = TimelineEditor.RulerHeight;
        context.FillRectangle(card, new Rect(0, top, Bounds.Width, TimelineEditor.WaveHeight));
        context.DrawLine(faint, new Point(0, top + TimelineEditor.WaveHeight), new Point(Bounds.Width, top + TimelineEditor.WaveHeight));

        // A button as the toolbar's ("Add timeline"): the Fluent button's own fill, text and corner, 40 px high, over the whole name column
        // less the same space at its sides. Without a sound it asks for one, with one it names it and replaces it on a click.
        bool empty = editor.Project.AudioResourceId is null;
        var button = new Rect(TimelineEditor.SoundtrackButtonInset, TimelineEditor.SoundtrackButtonTop,
            TimelineEditor.HeaderWidth - 2 * TimelineEditor.SoundtrackButtonInset, TimelineEditor.SoundtrackButtonHeight);
        var fill = _soundtrackHovered ? ThemeBrush("ButtonBackgroundPointerOver", Color.FromRgb(0x44, 0x44, 0x44)) : ThemeBrush("ButtonBackground", Color.FromRgb(0x3A, 0x3A, 0x3A));
        var foreground = ThemeBrush("ButtonForeground", Colors.White);
        var border = new Pen(ThemeBrush("ButtonBorderBrush", Colors.Transparent), 1);
        context.DrawRectangle(fill, border, button, 4, 4);

        string label = "♪  " + (empty
            ? Strings.Soundtrack_Import
            : System.IO.Path.GetFileName((editor.Project.AudioSourcePath ?? "").Replace('\\', '/')));
        var caption = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal), 14, foreground)
        {
            MaxTextWidth = button.Width - 16,
            MaxLineCount = 2,
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
        };
        context.DrawText(caption, new Point(button.X + 8, button.Y + (button.Height - caption.Height) / 2));
    }

    private void DrawWave(DrawingContext context, TimelineEditor editor, Rect timeArea, IBrush card, Pen faint)
    {
        double top = TimelineEditor.RulerHeight, height = TimelineEditor.WaveHeight;
        var lane = new Rect(timeArea.X, top, timeArea.Width, height);
        context.FillRectangle(card, lane);
        double middle = top + height / 2, amplitude = height / 2 - WaveStyle.Margin;
        context.DrawLine(faint, new Point(timeArea.X, middle), new Point(timeArea.Right, middle));
        if (editor.Wave is not { } wave) return;

        bool dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        double startX = Math.Max(timeArea.X, editor.TimeToX(TimeSpan.Zero)), endX = Math.Min(timeArea.Right, editor.TimeToX(wave.Duration));
        if (endX <= startX) return;

        // Where the outline bends: every pixel column (the loudest sample under it, at any zoom), or, zoomed in past a point a pixel, every point of the waveform.
        var left = new List<(double X, float Peak)>();
        var right = new List<(double X, float Peak)>();
        double pixelsPerPoint = editor.PixelsPerSecond / wave.PointsPerSecond;
        if (pixelsPerPoint >= 2)
        {
            int first = Math.Max(0, (int)Math.Floor(editor.XToTime(startX).TotalSeconds * wave.PointsPerSecond));
            int last = Math.Min(wave.Count - 1, (int)Math.Ceiling(editor.XToTime(endX).TotalSeconds * wave.PointsPerSecond));
            for (int i = first; i <= last; i++)
            {
                double x = editor.TimeToX(TimeSpan.FromSeconds((i + 0.5) / wave.PointsPerSecond));
                left.Add((x, wave.Left[i]));
                right.Add((x, wave.Right[i]));
            }
        }
        else
        {
            for (double x = startX; x < endX; x++)
            {
                var (l, r) = wave.Peaks(editor.XToTime(x), editor.XToTime(x + 1));
                left.Add((x + 0.5, l));
                right.Add((x + 0.5, r));
            }
        }

        using (context.PushClip(lane))
        {
            DrawChannel(context, left, startX, endX, middle, -amplitude, WaveStyle.Left(dark), up: true);
            DrawChannel(context, right, startX, endX, middle, amplitude, WaveStyle.Right(dark), up: false);
        }
    }

    // One channel: the area between the centre line and the outline, filled with the gradient of the whole half of the lane, and the outline on it.
    private static void DrawChannel(DrawingContext context, List<(double X, float Peak)> peaks, double startX, double endX, double middle, double reach, Color colour, bool up)
    {
        if (peaks.Count == 0) return;
        var area = new StreamGeometry();
        var outline = new StreamGeometry();
        using (var g = area.Open())
        {
            g.BeginFigure(new Point(startX, middle), isFilled: true);
            foreach (var (x, peak) in peaks) g.LineTo(new Point(x, middle + Math.Clamp(peak, 0, 1) * reach));
            g.LineTo(new Point(endX, middle));
            g.EndFigure(isClosed: true);
        }
        using (var g = outline.Open())
        {
            g.BeginFigure(new Point(startX, middle), isFilled: false);
            foreach (var (x, peak) in peaks) g.LineTo(new Point(x, middle + Math.Clamp(peak, 0, 1) * reach));
            g.LineTo(new Point(endX, middle));
            g.EndFigure(isClosed: false);
        }
        var half = up ? new Rect(startX, middle + reach, endX - startX, -reach) : new Rect(startX, middle, endX - startX, reach);
        using (context.PushGeometryClip(area)) context.FillRectangle(WaveStyle.Fill(colour, up), half);
        context.DrawGeometry(null, WaveStyle.Outline(colour), outline);
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
            var rect = ItemRect(editor, start, duration, top);
            // Every item has a 1 px border of a translucent grey (not a colour); a selected one has the glow drawn over everything
            // (DrawSelectionGlow). The place an item being dragged would land on has a plain light outline.
            // The fill stops one pixel short of the edge and the border is that pixel, so none of the timeline's colour shows through the border.
            context.DrawRectangle(new SolidColorBrush(fill, opacity * 0.85), null, rect.Deflate(1), 3, 3);
            context.DrawRectangle(null, outline ? new Pen(new SolidColorBrush(SelectionGrey), 2) : new Pen(new SolidColorBrush(EdgeGrey, 0.4), 1), rect.Deflate(outline ? 0 : 0.5), 4, 4);

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

    private static Rect ItemRect(TimelineEditor editor, TimeSpan start, TimeSpan duration, double top)
    {
        double left = editor.TimeToX(start), right = editor.TimeToX(start + duration);
        return new Rect(left, top + 6, Math.Max(2, right - left), TimelineEditor.TrackHeight - 12);
    }

    // The mark of a selected item (an experiment: no colour): a light grey border that fades out to nothing over this many pixels.
    private const int GlowWidth = 4;
    private static readonly Color SelectionGrey = Color.FromRgb(0xD8, 0xD8, 0xD8);

    /// <summary>The grey of item borders and of the selection glow: light on the dark theme, dark on the light one, where light would not show.</summary>
    private Color EdgeGrey => ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? SelectionGrey : Color.FromRgb(0x40, 0x40, 0x40);

    /// <summary>
    /// A grey border on the inside edge of every selected item that fades away outward in <see cref="GlowWidth"/> pixels. Drawn after all
    /// the items, so the neighbours do not cover it. The grey is light on the dark theme and dark on the light one, where it would not show.
    /// </summary>
    private void DrawSelectionGlow(DrawingContext context, TimelineEditor editor, Timeline timeline, TimelineItem item, double top) =>
        DrawGlow(context, ItemRect(editor, item.Start, item.Duration, top), 4);

    /// <summary>The border and the fade-out of a selection round a rectangle (see <see cref="DrawSelectionGlow"/>).</summary>
    private void DrawGlow(DrawingContext context, Rect rect, double radius)
    {
        var grey = EdgeGrey;
        for (int k = 0; k < GlowWidth; k++)
        {
            // Ring k is one pixel wide: the first lies just inside the edge, the next ones outside it.
            double grow = k - 0.5;
            double corner = radius + Math.Max(0, grow);
            context.DrawRectangle(null, new Pen(new SolidColorBrush(grey, 0.9 * (1 - (double)k / GlowWidth)), 1), rect.Inflate(grow), corner, corner);
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
        if (item.Kind == ItemKind.Video)
        {
            DrawFilmStrip(context, thumb, bitmap, source, run, inner, left);
            return;
        }
        // Sharp columns when a pass is wider than its picture, smooth when it is squeezed.
        var mode = run.PassWidth >= thumb.Columns ? BitmapInterpolationMode.None : BitmapInterpolationMode.MediumQuality;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = mode }))
        {
            for (int n = run.FirstPass; n <= run.LastPass; n++)
                context.DrawImage(bitmap, source, new Rect(left + n * run.PassWidth, inner.Y, run.PassWidth, inner.Height));
        }
    }

    /// <summary>
    /// A video along its pass as a film strip: tiles of one fixed size (the frame in its own proportions at the height of the lane), side by
    /// side from the start of the pass, each showing the frame that plays at its left edge, so the first tile is the first frame and the
    /// strip reads like time. The size of a tile does not depend on the zoom, only how many fit, so zooming moves and cuts tiles but never
    /// stretches them; the last tile of a pass is cut where the pass ends. A pass narrower than a tile shows the left part of the first
    /// frame. Tile edges are on whole pixels, so neighbours neither overlap nor leave a gap. The same again for every pass.
    /// </summary>
    private static void DrawFilmStrip(DrawingContext context, Thumbnail thumb, Bitmap first, Rect source, TimelineEditor.TileRun run, Rect inner, double left)
    {
        context.FillRectangle(Brushes.Black, inner);   // behind the tiles, like film around its frames
        double tile = inner.Height * source.Width / source.Height;
        int frames = thumb.Strip?.Count ?? 0;
        double visibleLeft = Math.Max(inner.X, TimelineEditor.HeaderWidth);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality }))
        {
            for (int n = run.FirstPass; n <= run.LastPass; n++)
            {
                double x0 = left + n * run.PassWidth;
                if (frames == 0 || run.PassWidth < tile)
                {
                    // The still is cut to the pass, so a narrow pass shows the left part of the frame, not a squeezed one.
                    double width = Math.Min(run.PassWidth, tile);
                    context.DrawImage(first, new Rect(0, 0, source.Width * width / tile, source.Height), new Rect(x0, inner.Y, width, inner.Height));
                    continue;
                }
                // Only the tiles that can be seen: the lane is clipped to the item and the window.
                int from = (int)Math.Max(0, Math.Floor((visibleLeft - x0) / tile));
                int to = (int)Math.Min(Math.Ceiling(run.PassWidth / tile) - 1, Math.Floor((inner.Right - x0) / tile));
                for (int i = from; i <= to; i++)
                {
                    double start = i * tile, end = Math.Min(start + tile, run.PassWidth);
                    double u = start / run.PassWidth;   // how far into the pass: which frame plays here
                    var frame = thumb.StripBitmap(Math.Min(frames - 1, (int)(u * frames)));
                    if (frame is null) continue;
                    double xa = Math.Round(x0 + start), xb = Math.Round(x0 + end);
                    if (xb <= xa) continue;
                    // The last tile of a pass is cut, not squeezed.
                    double shown = (end - start) / tile;
                    context.DrawImage(frame, new Rect(0, 0, frame.PixelSize.Width * shown, frame.PixelSize.Height), new Rect(xa, inner.Y, xb - xa, inner.Height));
                }
            }
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

/// <summary>
/// The background of a timeline's row, as PixelTuner 2 had it (<c>TimeLineViewModel.Background</c> and <c>SetBackgrounds</c>): two shades a
/// few levels apart that alternate with the place of the timeline, and a tint for each kind of device. PixelTuner 2 was dark only; the
/// light theme has the same weight against its page and the same tints.
/// </summary>
public static class TimelineRows
{
    // PixelTuner 2's pairs (even place, odd place) in the dark theme.
    private static (Color Even, Color Odd) Dark(string kind) => kind switch
    {
        TimelineKind.Video => (Color.FromRgb(80, 60, 80), Color.FromRgb(70, 50, 70)),
        TimelineKind.EpicPoi => (Color.FromRgb(80, 60, 60), Color.FromRgb(70, 50, 50)),
        TimelineKind.Light => (Color.FromRgb(60, 60, 80), Color.FromRgb(50, 50, 70)),
        _ => (Color.FromRgb(70, 70, 70), Color.FromRgb(64, 64, 64)),
    };

    // The page and the dark rows' base grey in the dark theme, and the page in the light one: the light rows keep the same distance from the
    // page (three quarters of it, a lighter page shows the same distance stronger) and the same tint of each kind.
    private const double DarkPage = 22, LightPage = 247, LightWeight = 0.75;

    public static Color Background(string kind, int place, bool darkTheme)
    {
        var (even, odd) = Dark(kind);
        var dark = place % 2 == 0 ? even : odd;
        if (darkTheme) return dark;

        var (greyEven, greyOdd) = Dark(TimelineKind.Pixel);
        var grey = place % 2 == 0 ? greyEven : greyOdd;
        double baseLevel = LightPage - LightWeight * (grey.R - DarkPage);
        byte Channel(byte value, byte reference) => (byte)Math.Clamp(Math.Round(baseLevel + LightWeight * (value - reference)), 0, 255);
        return Color.FromRgb(Channel(dark.R, grey.R), Channel(dark.G, grey.R), Channel(dark.B, grey.R));
    }
}
