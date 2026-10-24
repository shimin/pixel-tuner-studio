using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.Package.Import;
using PixelTuner.UI.LedLayouts;
using PixelTuner.UI.LibraryPanel;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Timelines;
using PixelTuner.UI.ViewModels;
using PixelTuner.UI.Views;

namespace PixelTuner.UI.Tests;

/// <summary>
/// Dragging with the real controls: an entry of the library onto a timeline, and an outline of the canvas onto a video timeline.
/// The platform's drag loop is not there in the headless window, so the drop is sent as the events it would raise.
/// </summary>
public sealed class HeadlessDragTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-drag").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private ProjectDocument Glasses()
    {
        string package = Path.Combine(_dir, "glasses.pxpkg");
        PtpImporter.Import(Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.ptp"), package);
        return ProjectDocument.Open(package);
    }

    private static DataTransfer Carrying<T>(DataFormat<string> format, T id)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(format, id!.ToString()!));
        return transfer;
    }

    private static Point Over(Window window, TimelineControl control, double x, double y) => control.TranslatePoint(new Point(x, y), window)!.Value;

    [Fact]
    public void An_entry_dropped_on_a_timeline_goes_there_at_the_place_of_the_drop_and_one_it_cannot_play_is_refused_with_the_reason() => HeadlessAppBuilder.Run(() =>
    {
        using var doc = Glasses();
        using var editor = new EditorViewModel(doc);
        var window = new Window { Width = 1400, Height = 900, Content = new EditorView { DataContext = editor } };
        window.Show();
        var control = window.GetVisualDescendants().OfType<TimelineControl>().First();
        var videos = doc.Project.Timelines.Where(t => t.Kind == TimelineKind.Video).ToList();
        var gif = editor.Gallery.AddFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.gif"), LibraryKind.Video);
        var target = videos[1];
        int before = target.Items.Count;
        int row = doc.Project.Timelines.IndexOf(target);
        double y = editor.Timelines.TrackTop(row) + 20;

        // Over a video timeline the hint is "yes"; dropped well after the last item, the item begins where the pointer was.
        editor.Timelines.ZoomAt(1e-3, TimelineEditor.HeaderWidth);   // as far out as it goes: the end of the items is in the window
        var end = target.Items.Max(i => i.End);
        double x = editor.Timelines.TimeToX(end + TimeSpan.FromSeconds(2));
        var transfer = Carrying(LibraryDrag.Format, gif.Id);
        window.DragDrop(Over(window, control, x, y), RawDragEventType.DragEnter, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(Over(window, control, x, y), RawDragEventType.DragOver, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        Assert.True(editor.Timelines.LayoutDrop is { Allowed: true } hint && hint.Target == target, $"hint {editor.Timelines.LayoutDrop}");
        window.DragDrop(Over(window, control, x, y), RawDragEventType.Drop, transfer, DragDropEffects.Copy, RawInputModifiers.None);

        Assert.Equal(before + 1, target.Items.Count);
        var dropped = target.Items.Single(i => i.Start > end);
        Assert.Equal(gif.ResourceId, dropped.ResourceId);
        Assert.InRange((dropped.Start - (end + TimeSpan.FromSeconds(2))).TotalMilliseconds, -60, 60);
        Assert.Same(dropped, editor.Timelines.Selected);
        Assert.Null(editor.Timelines.LayoutDrop);

        // A pixel timeline cannot play a video: red, with the reason, and nothing is put there.
        var pixel = editor.Timelines.AddTimeline("Pixels", TimelineKind.Pixel, "#FF2D7DD2");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();   // the control grows by the new row
        double py = editor.Timelines.TrackTop(doc.Project.Timelines.IndexOf(pixel)) + 20;
        window.DragDrop(Over(window, control, 400, py), RawDragEventType.DragEnter, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(Over(window, control, 400, py), RawDragEventType.DragOver, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        Assert.True(editor.Timelines.LayoutDrop is { Allowed: false } refused && refused.Text() == Strings.Library_WrongKind, $"hint {editor.Timelines.LayoutDrop}");
        window.DragDrop(Over(window, control, 400, py), RawDragEventType.Drop, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        Assert.Empty(pixel.Items);
    });

    [Fact]
    public void A_fixed_LightMaster_entry_can_be_dragged_onto_a_light_timeline() => HeadlessAppBuilder.Run(() =>
    {
        using var doc = ProjectDocument.CreateNew("Show");
        using var editor = new EditorViewModel(doc);
        var window = new Window { Width = 1400, Height = 900, Content = new EditorView { DataContext = editor } };
        window.Show();
        var control = window.GetVisualDescendants().OfType<TimelineControl>().First();
        var light = editor.Timelines.AddTimeline("Light", TimelineKind.Light, "#FFC9A227");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();   // the control grows by the new row
        var sine = LibraryViewModel.LightEntries.First(e => e.Effect == LightEffectKind.SineWave);
        double y = editor.Timelines.TrackTop(0) + 20;
        var transfer = Carrying(LibraryDrag.Format, sine.Id);

        window.DragDrop(Over(window, control, 300, y), RawDragEventType.DragEnter, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(Over(window, control, 300, y), RawDragEventType.DragOver, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(Over(window, control, 300, y), RawDragEventType.Drop, transfer, DragDropEffects.Copy, RawInputModifiers.None);

        var item = Assert.Single(light.Items);
        Assert.Equal((ItemKind.Effect, LightEffectKind.SineWave), (item.Kind, item.Effect));
    });

    [Fact]
    public void Pressing_an_entry_of_the_library_and_moving_starts_a_drag_and_a_click_does_not() => HeadlessAppBuilder.Run(() =>
    {
        using var doc = Glasses();
        using var editor = new EditorViewModel(doc);
        var view = new EditorView { DataContext = editor };
        var window = new Window { Width = 1400, Height = 900, Content = view };
        window.Show();
        editor.Gallery.SectionIndex = 1;   // the glasses are videos
        var list = view.GetVisualDescendants().OfType<ListBox>().First(l => l.Name == "EntryList");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var item = list.GetVisualDescendants().OfType<ListBoxItem>().First();
        var at = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;

        window.MouseDown(at, MouseButton.Left);
        window.MouseMove(at + new Point(2, 0), RawInputModifiers.LeftMouseButton);
        Assert.False(view.IsDraggingEntry);                                            // a click moves a pixel or two
        window.MouseMove(at + new Point(30, 20), RawInputModifiers.LeftMouseButton);
        Assert.True(view.IsDraggingEntry);
    });

    [Fact]
    public void An_outline_of_the_canvas_is_dragged_by_pressing_it_and_moving() => HeadlessAppBuilder.Run(() =>
    {
        using var doc = Glasses();
        using var editor = new EditorViewModel(doc);
        var view = new EditorView { DataContext = editor };
        var window = new Window { Width = 1400, Height = 900, Content = view };
        window.Show();
        view.GetVisualDescendants().OfType<TabControl>().First(t => t.Classes.Contains("rail")).SelectedIndex = 2;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var canvas = view.GetVisualDescendants().OfType<CanvasDrawingControl>().First();

        Point? hit = null;
        for (double x = 5; x < canvas.Bounds.Width && hit is null; x += 6)
            for (double y = 5; y < canvas.Bounds.Height && hit is null; y += 6)
            {
                var q = canvas.TranslatePoint(new Point(x, y), window)!.Value;
                window.MouseDown(q, MouseButton.Left);
                if (editor.Canvas.Selected is not null) hit = q;
                else window.MouseUp(q, MouseButton.Left);
            }
        Assert.NotNull(hit);
        window.MouseMove(hit!.Value + new Point(40, 30), RawInputModifiers.LeftMouseButton);
        Assert.True(canvas.IsDragging);
    });
}
