using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.Package.Import;
using PixelTuner.UI.Timelines;
using PixelTuner.UI.ViewModels;
using PixelTuner.UI.Views;

namespace PixelTuner.UI.Tests;

public sealed class HeadlessApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://PixelTuner.UI/")) { Source = new Uri("avares://PixelTuner.UI/Styles/Theme.axaml") });
    }
}

public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessAppBuilder));

    /// <summary>Runs the test body on the UI thread of the headless application.</summary>
    public static void Run(Action body) => Session.Dispatch(() => { body(); return 0; }, CancellationToken.None).GetAwaiter().GetResult();
}

/// <summary>
/// The inspector and the keys with Avalonia's own controls: a list box that gets a new list writes indexes back, a window sees the keys a
/// text box is typing. The view models alone cannot show either.
/// </summary>
public sealed class HeadlessInspectorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-headless").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private ProjectDocument ImportedGlasses()
    {
        string package = Path.Combine(_dir, "glasses.pxpkg");
        PtpImporter.Import(Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.ptp"), package);
        return ProjectDocument.Open(package);
    }

    private static string?[] LayoutNames(ProjectDocument doc) =>
        [.. doc.Project.Timelines.Where(t => t.Kind == TimelineKind.Video).Select(t => doc.Project.LedLayouts.FirstOrDefault(l => l.Id == t.LedLayoutId)?.Name)];

    [Fact]
    public void Clicking_through_headers_items_and_the_sound_lane_keeps_the_layouts_of_an_imported_show() => HeadlessAppBuilder.Run(() =>
    {
        using var doc = ImportedGlasses();
        using var editor = new EditorViewModel(doc);
        var view = new EditorView { DataContext = editor };
        new Window { Width = 1400, Height = 900, Content = view }.Show();
        var box = view.GetVisualDescendants().OfType<ComboBox>().First(c => c.ItemsSource is IEnumerable<string> names && names.Contains("path2996"));
        var video = doc.Project.Timelines.Where(t => t.Kind == TimelineKind.Video).ToList();
        string?[] before = LayoutNames(doc);
        Assert.Equal(["path2996", "path2998"], before);

        editor.Timelines.Select(video[0].Items[0]);
        Assert.Equal("path2996", editor.Preview.Outline?.Id);
        editor.Timelines.Select(video[0]);
        Assert.Equal(1, box.SelectedIndex);
        editor.Timelines.Select(video[1]);
        Assert.Equal(2, box.SelectedIndex);
        editor.Timelines.Select(null);   // the sound lane
        Assert.Equal("path2998", editor.Preview.Outline?.Id);   // the preview stays with the last timeline
        editor.Timelines.Select(video[0]);
        editor.Timelines.Select(video[1].Items[0]);

        Assert.Equal(before, LayoutNames(doc));
        Assert.False(doc.IsDirty);
        Assert.False(doc.CanUndo);
    });

    [Fact]
    public void Picking_a_layout_in_the_box_is_one_edit_that_undo_takes_back() => HeadlessAppBuilder.Run(() =>
    {
        using var doc = ImportedGlasses();
        using var editor = new EditorViewModel(doc);
        var view = new EditorView { DataContext = editor };
        new Window { Width = 1400, Height = 900, Content = view }.Show();
        var box = view.GetVisualDescendants().OfType<ComboBox>().First(c => c.ItemsSource is IEnumerable<string> names && names.Contains("path2996"));
        var first = doc.Project.Timelines.First(t => t.Kind == TimelineKind.Video);

        editor.Timelines.Select(first);
        box.SelectedIndex = 2;
        Assert.Equal(["path2998", "path2998"], LayoutNames(doc));
        Assert.Equal(2, box.SelectedIndex);

        doc.Undo();
        Assert.Equal(["path2996", "path2998"], LayoutNames(doc));
        Assert.Equal(1, box.SelectedIndex);
    });

    [Fact]
    public void Dropping_a_layout_on_a_timeline_gives_it_the_layout_and_it_stays() => HeadlessAppBuilder.Run(() =>
    {
        using var doc = ImportedGlasses();
        using var editor = new EditorViewModel(doc);
        var view = new EditorView { DataContext = editor };
        new Window { Width = 1400, Height = 900, Content = view }.Show();
        var video = doc.Project.Timelines.Where(t => t.Kind == TimelineKind.Video).ToList();

        editor.Timelines.Select(video[0]);
        Assert.True(editor.Timelines.DropLayout(editor.Timelines.TrackTop(1) + 5, doc.Project.LedLayouts[0]));
        Assert.Equal(["path2996", "path2996"], LayoutNames(doc));
        Assert.Equal("path2996", editor.Preview.Outline?.Id);
    });

    // ---- keys

    private static (MainWindow Window, MainViewModel Model, TextBox NameBox) ShowEditor(ProjectDocument doc)
    {
        var model = new MainViewModel { Document = doc };
        var window = new MainWindow { DataContext = model, Width = 1400, Height = 900 };
        window.Show();
        model.Editor!.Timelines.Select(doc.Project.Timelines[0]);
        var box = window.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible && t.Text == "Alpha");
        return (window, model, box);
    }

    private static ProjectDocument Alpha()
    {
        var doc = ProjectDocument.CreateNew("x");
        doc.Project.Timelines.Add(new Timeline { Name = "Alpha", Kind = TimelineKind.Pixel });
        return doc;
    }

    [Theory]
    [InlineData(PhysicalKey.Backspace, 5)]
    [InlineData(PhysicalKey.Backspace, 0)]
    [InlineData(PhysicalKey.Delete, 2)]
    [InlineData(PhysicalKey.Delete, 5)]
    [InlineData(PhysicalKey.Delete, 0)]
    public void Delete_and_Backspace_in_a_text_box_do_not_delete_the_timeline(PhysicalKey key, int caret) => HeadlessAppBuilder.Run(() =>
    {
        using var doc = Alpha();
        var (window, model, box) = ShowEditor(doc);
        using var _ = model;
        box.Focus();
        box.CaretIndex = caret;
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Assert.Single(doc.Project.Timelines);
    });

    [Theory]
    [InlineData(PhysicalKey.Backspace)]
    [InlineData(PhysicalKey.Delete)]
    public void Delete_and_Backspace_delete_the_selection_when_no_text_box_has_the_focus(PhysicalKey key) => HeadlessAppBuilder.Run(() =>
    {
        using var doc = Alpha();
        var (window, model, _) = ShowEditor(doc);
        using var __ = model;
        window.FindDescendantOfType<TimelineControl>()!.Focus();
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Assert.Empty(doc.Project.Timelines);
    });
}
