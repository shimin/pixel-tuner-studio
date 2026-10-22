using CommunityToolkit.Mvvm.ComponentModel;
using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Core.Preview;
using PixelTuner.UI.Resources;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.PreviewPanel;

/// <summary>How the preview draws what a timeline plays.</summary>
public enum PreviewMode
{
    /// <summary>Nothing to show: no timeline, or the build refuses it.</summary>
    None,

    /// <summary>A pixel poi or staff: the picture painted in the air by a swing, tiled at the speed of the device.</summary>
    Poi,

    /// <summary>A pixel hoop: a ring that moves on an orbit and leaves an afterglow.</summary>
    Hoop,

    /// <summary>A video module: the frame on the markup of the prop, over its outline.</summary>
    Video,

    /// <summary>E-Pic or LightMaster: the colours of the channels.</summary>
    Colours,
}

/// <summary>
/// The preview of the selected timeline at the playhead (the sound's position while it plays): what the device that plays the timeline
/// would light, read back from its .bin (<see cref="TimelinePreview"/>), at the speed of its kind of device. Rebuilt when the project changes;
/// the controls only draw it. Every timeline's preview is kept, so the canvas can show the video of the timelines that play on its outlines.
/// </summary>
public sealed partial class PreviewViewModel : ObservableObject
{
    private readonly EditorViewModel _editor;
    private readonly Dictionary<Guid, (TimelinePreview? Preview, IReadOnlyList<BuildIssue> Issues)> _built = [];

    public PreviewViewModel(EditorViewModel editor) => _editor = editor;

    /// <summary>Raised when the picture changes: the playhead moved, another timeline was selected, or the project changed.</summary>
    public event EventHandler? Changed;

    // ---- what is built

    /// <summary>The timeline shown: the selected one, or the one the selected item is on.</summary>
    /// <summary>The name of the timeline shown, with the kind of device.</summary>
    public string Name => Timeline is { } t ? $"{t.Name} · {(Mode == PreviewMode.Hoop ? DeviceKind.Hoop : Current?.DeviceType ?? t.Kind)}" : "";

    public Timeline? Timeline => _editor.Timelines.Selected switch
    {
        Timeline timeline => timeline,
        TimelineItem item => _editor.Timelines.TimelineOf(item),
        _ => null,
    };

    private (TimelinePreview? Preview, IReadOnlyList<BuildIssue> Issues) Built(Timeline timeline)
    {
        if (_built.TryGetValue(timeline.Id, out var known)) return known;
        TimelinePreview? preview;
        IReadOnlyList<BuildIssue> issues;
        try
        {
            preview = TimelinePreview.Build(_editor.Document.Project, _editor.BinMedia, timeline, out issues);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            preview = null;
            issues = [];
        }
        return _built[timeline.Id] = (preview, issues);
    }

    /// <summary>What a timeline plays, built for the device that plays it; null while the build refuses it.</summary>
    public TimelinePreview? PreviewOf(Timeline timeline) => Built(timeline).Preview;

    public TimelinePreview? Current => Timeline is { } timeline ? PreviewOf(timeline) : null;

    /// <summary>Why there is nothing to show: what the build says about the timeline, in words.</summary>
    public string? Problem
    {
        get
        {
            if (Timeline is not { } timeline) return Strings.Preview_NoTimeline;
            var (preview, issues) = Built(timeline);
            if (preview is not null) return null;
            var error = issues.FirstOrDefault(i => i.Severity == BuildSeverity.Error);
            return error is null ? Strings.Preview_Nothing : EditorViewModel.IssueText(error);
        }
    }

    public bool HasProblem => Problem is not null;

    // ---- what is shown

    /// <summary>For a pixel timeline: a poi or staff (the default) or a hoop. The file is the same; they differ in how it is drawn.</summary>
    [ObservableProperty] private bool _asHoop;

    partial void OnAsHoopChanged(bool value)
    {
        OnPropertyChanged(nameof(PixelView));
        Redraw();
    }

    /// <summary>The position of the pixel box: 0 poi or staff, 1 hoop.</summary>
    public int PixelView
    {
        get => AsHoop ? 1 : 0;
        set => AsHoop = value == 1;
    }

    public bool IsPixel => Mode is PreviewMode.Poi or PreviewMode.Hoop;
    public bool IsHoop => Mode == PreviewMode.Hoop;

    public PreviewMode Mode
    {
        get
        {
            if (Current is not { } preview) return PreviewMode.None;
            return preview.DeviceType switch
            {
                DeviceKind.Video => PreviewMode.Video,
                DeviceKind.EpicPoi or DeviceKind.Light => PreviewMode.Colours,
                _ => AsHoop ? PreviewMode.Hoop : PreviewMode.Poi,
            };
        }
    }

    /// <summary>What the LEDs show at the playhead.</summary>
    public PreviewFrame? Frame => Current?.At(_editor.Timelines.Playhead);

    /// <summary>The LED layout of the video timeline shown, for placing the LEDs of its frame.</summary>
    public LedLayout? Layout => Timeline is { LedLayoutId: { } id } ? _editor.Document.Project.LedLayouts.FirstOrDefault(l => l.Id == id) : null;

    /// <summary>The outline of the prop in the drawing of the canvas that the shown video timeline's layout belongs to; null without one.</summary>
    public Core.Layout.CanvasShape? Outline => Layout is { } layout ? _editor.Canvas.ShapeOf(layout) : null;

    /// <summary>
    /// The colours the LEDs of a layout show at the playhead, when a video timeline plays on it (black where it plays nothing); null when no
    /// video timeline does. This is how the canvas fills an outline with its video.
    /// </summary>
    public IReadOnlyList<Rgb>? ColoursOn(LedLayout layout)
    {
        foreach (var timeline in _editor.Document.Project.Timelines)
        {
            if (timeline.Kind != TimelineKind.Video || timeline.LedLayoutId != layout.Id || PreviewOf(timeline) is not { } preview) continue;
            return preview.At(_editor.Timelines.Playhead).Colors;
        }
        return null;
    }

    // ---- how a swing is drawn (PixelTuner 2's sliders: tail, scan speed, rotation)

    // TODO(release): the swing model (speed, pitch, orbit, afterglow) is a guess, see docs/before-release.md.
    /// <summary>How fast the device is swung, in millimetres a millisecond (6 m/s): with the time a column is held it sets how wide a column is in the air.</summary>
    [ObservableProperty] private double _swingSpeed = 6;

    /// <summary>Distance between two LEDs of a pixel device in millimetres.</summary>
    [ObservableProperty] private double _ledPitch = 16;

    /// <summary>How long the afterglow of a hoop lasts, in milliseconds.</summary>
    [ObservableProperty] private double _tail = 120;

    /// <summary>Turns of the hoop's orbit a second.</summary>
    [ObservableProperty] private double _orbitSpeed = 1.5;

    partial void OnSwingSpeedChanged(double value) => Redraw();
    partial void OnLedPitchChanged(double value) => Redraw();
    partial void OnTailChanged(double value) => Redraw();
    partial void OnOrbitSpeedChanged(double value) => Redraw();

    /// <summary>One line about the frame: entry, column, speed.</summary>
    public string Info
    {
        get
        {
            if (Current is not { } preview || Frame is not { } frame) return "";
            if (preview.IsLight) return string.Format(Strings.Preview_InfoLight, preview.Height);
            if (!frame.IsPlaying || frame.Picture is null) return Strings.Preview_Stopped;
            return string.Format(Strings.Preview_Info, frame.Entry + 1, frame.Column + 1, frame.Picture.Width, ColumnText(frame.ColumnTime), frame.PassTime.TotalMilliseconds, preview.Height);
        }
    }

    private static string ColumnText(TimeSpan time) =>
        time.TotalMilliseconds >= 1 ? string.Format("{0:0.##} ms", time.TotalMilliseconds) : string.Format("{0:0} µs", time.TotalMilliseconds * 1000);

    // ---- keeping up

    /// <summary>The project changed: every file is built again when it is next asked for.</summary>
    public void ProjectChanged()
    {
        _built.Clear();
        Redraw();
    }

    /// <summary>The selection or the playhead moved.</summary>
    public void Moved() => Redraw();

    private void Redraw()
    {
        OnPropertyChanged(nameof(Timeline));
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsPixel));
        OnPropertyChanged(nameof(IsHoop));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Frame));
        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(HasProblem));
        OnPropertyChanged(nameof(Layout));
        OnPropertyChanged(nameof(Outline));
        OnPropertyChanged(nameof(Info));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
