using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.ViewModels;

/// <summary>A colour to pick for a timeline with one click.</summary>
public sealed record ColorSwatch(string Hex, IBrush Brush, IRelayCommand Pick);

// What a timeline header controls. Each control is there only for the kinds of timeline whose .bin has a field for it
// (docs/binary-format.md, section 4), and each is one Edit.
public sealed partial class EditorViewModel
{
    private static readonly string[] PaletteColors =
        ["#2D7DD2", "#2FA84F", "#E07A1F", "#C9A227", "#D1495B", "#8E5BD1", "#1FA2A8", "#8A8F98"];

    private Timeline? SelectedTimeline => Timelines.Selected as Timeline;

    public bool IsTimelineSelected => SelectedTimeline is not null;

    // ---- colour

    /// <summary>"#RRGGBB"; a text that is not a colour is refused and the old colour comes back.</summary>
    public string TimelineColorText
    {
        get => SelectedTimeline is { } t ? "#" + t.Color[^6..] : "";
        set
        {
            if (SelectedTimeline is not { } t || value == TimelineColorText) return;
            try
            {
                var c = ColorText.ParseRgb(value);
                Document.Execute(Edits.SetTimelineColor(t, $"#FF{c.R:X2}{c.G:X2}{c.B:X2}"));
            }
            catch (InvalidDataException)
            {
                Reject(value);
            }
        }
    }

    public IBrush TimelineColorBrush
    {
        get
        {
            try
            {
                var c = ColorText.ParseRgb(TimelineColorText);
                return new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            }
            catch (InvalidDataException)
            {
                return Brushes.Transparent;
            }
        }
    }

    public IReadOnlyList<ColorSwatch> Palette { get; private set; } = [];

    private void BuildPalette() =>
        Palette = PaletteColors.Select(hex =>
            new ColorSwatch(hex, new SolidColorBrush(Color.Parse(hex)), new RelayCommand(() => TimelineColorText = hex))).ToList();

    /// <summary>Video modules and the LightMaster do not store the timeline's colour; it only helps the eye.</summary>
    public bool ColorIsOnlyALabel => SelectedTimeline is { Kind: TimelineKind.Video or TimelineKind.Light };

    // ---- flip, monochrome

    public bool ShowFlip => SelectedTimeline is { Kind: TimelineKind.Pixel };

    public bool FlipVertical
    {
        get => SelectedTimeline is { FlipVertical: true };
        set
        {
            if (SelectedTimeline is { Kind: TimelineKind.Pixel } t && t.FlipVertical != value)
                Document.Execute(Edits.SetFlipVertical(t, value));
        }
    }

    public bool ShowMonochrome => SelectedTimeline is { Kind: TimelineKind.Light };

    public bool Monochrome
    {
        get => SelectedTimeline is { Monochrome: true };
        set
        {
            if (SelectedTimeline is { Kind: TimelineKind.Light } t && t.Monochrome != value)
                Document.Execute(Edits.SetMonochrome(t, value));
        }
    }

    // ---- height

    public bool ShowHeight => SelectedTimeline is { Kind: TimelineKind.Pixel };

    /// <summary>The number of LEDs set for the timeline; 0 takes the height of its first picture.</summary>
    public decimal? TimelineHeight
    {
        get => SelectedTimeline?.Height;
        set
        {
            if (SelectedTimeline is not { Kind: TimelineKind.Pixel } t || value is not { } v || v < 0 || (int)v == t.Height) return;
            try
            {
                Document.Execute(Edits.SetTimelineHeight(t, (int)v));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                Message = Strings.Inspector_HeightRefused;
                Refresh();
            }
        }
    }

    /// <summary>What the pictures of the timeline must be high, and where that comes from.</summary>
    public string HeightText
    {
        get
        {
            if (SelectedTimeline is not { Kind: TimelineKind.Pixel } t) return "";
            int height = Edits.PixelHeight(t);
            if (height == 0) return Strings.Inspector_HeightFree;
            return string.Format(t.Height > 0 ? Strings.Inspector_HeightSet : Strings.Inspector_HeightFromPicture, height);
        }
    }

    // ---- LED layout

    public bool ShowLayout => SelectedTimeline is { Kind: TimelineKind.Video };

    public IReadOnlyList<string> LayoutNames =>
        [Strings.Inspector_NoLayout, .. Document.Project.LedLayouts.Select((l, n) => string.IsNullOrEmpty(l.Name) ? string.Format(Strings.Inspector_LayoutN, n + 1) : l.Name)];

    public int LayoutIndex
    {
        get => SelectedTimeline is { LedLayoutId: { } id } ? Document.Project.LedLayouts.FindIndex(l => l.Id == id) + 1 : 0;
        set
        {
            if (SelectedTimeline is not { Kind: TimelineKind.Video } t || value < 0 || value > Document.Project.LedLayouts.Count) return;
            Guid? id = value == 0 ? null : Document.Project.LedLayouts[value - 1].Id;
            if (id != t.LedLayoutId) Document.Execute(Edits.SetLedLayout(Document.Project, t, id));
        }
    }

    // ---- order

    private bool CanMoveUp => SelectedTimeline is { } t && Document.Project.Timelines.IndexOf(t) > 0;
    private bool CanMoveDown => SelectedTimeline is { } t && Document.Project.Timelines.IndexOf(t) is var n && n >= 0 && n < Document.Project.Timelines.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    void MoveTimelineUp() => MoveSelectedTimeline(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    void MoveTimelineDown() => MoveSelectedTimeline(1);

    private void MoveSelectedTimeline(int by)
    {
        if (SelectedTimeline is not { } t) return;
        Document.Execute(Edits.MoveTimeline(Document.Project, t, Document.Project.Timelines.IndexOf(t) + by));
    }
}
