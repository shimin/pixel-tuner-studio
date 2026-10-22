using System.Globalization;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.ViewModels;

// What the selected item becomes in the .bin: its entry, its picture block, who shares it, the size of the file against the
// memory of the device, and what the build says about the item. The numbers come from the file the build makes (BinInfo), so
// the inspector shows what the device will get, not an estimate.
public sealed partial class EditorViewModel
{
    // Resources are told by their hash, so what was decoded once stays right.
    private ShowMedia? _binMedia;
    private ItemBinInfo? _binInfo;
    private TimelineItem? _binInfoOf;

    internal ShowMedia BinMedia => _binMedia ??= new ShowMedia(id => Document.OpenMedia(id) ?? throw new FileNotFoundException(id));

    private void ForgetBinInfo() => _binInfo = null;

    /// <summary>The selected item's place in the .bin; null when no item is selected or no file can be built for it at all.</summary>
    public ItemBinInfo? BinInfo
    {
        get
        {
            if (Item is not { } item || Timelines.TimelineOf(item) is not { } timeline)
            {
                _binInfo = null;
                return null;
            }
            if (_binInfo is null || !ReferenceEquals(_binInfoOf, item))
            {
                _binInfoOf = item;
                try
                {
                    _binInfo = Core.Export.BinInfo.Of(Document.Project, BinMedia, timeline, item);
                }
                catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or NotSupportedException or ArgumentException)
                {
                    _binInfo = null;
                }
            }
            return _binInfo;
        }
    }

    public bool HasBinInfo => BinInfo is not null;

    /// <summary>"Entry 3 of 5, picture 1.2 KB, shared with 1 more. File 12.3 KB of 3.6 MB."</summary>
    public string BinLine
    {
        get
        {
            if (BinInfo is not { } info) return "";
            var parts = new List<string>();
            if (!info.Built) return Strings.Bin_NotBuilt;
            if (info.EntryNumber is { } number) parts.Add(string.Format(Strings.Bin_Entry, number, info.EntryCount));
            if (info.BlockSize is { } block) parts.Add(string.Format(Strings.Bin_Block, Size(block)));
            int shared = info.SharedWithItems.Count + info.SharedWithPauses;
            if (shared > 0) parts.Add(string.Format(Strings.Bin_Shared, shared));
            string head = string.Join(", ", parts);
            string file = info.MemoryBytes > 0
                ? string.Format(Strings.Bin_FileOf, Size(info.FileSize), Size(info.MemoryBytes), (100.0 * info.FileSize / info.MemoryBytes).ToString("0.#", CultureInfo.CurrentCulture))
                : string.Format(Strings.Bin_File, Size(info.FileSize));
            return head.Length == 0 ? file : head + ". " + file;
        }
    }

    /// <summary>What the build says about the selected item and the file, one line each; errors first.</summary>
    public IReadOnlyList<string> BinIssues =>
        BinInfo?.Issues.OrderByDescending(i => i.Severity).Select(IssueText).ToList() ?? [];

    public bool HasBinIssues => BinIssues.Count > 0;

    /// <summary>The words for what the build found. The build says what, the panel says it in words.</summary>
    public static string IssueText(BuildIssue issue) => issue.Code switch
    {
        BuildIssueCode.NothingToPlay => Strings.Issue_NothingToPlay,
        BuildIssueCode.EmptyTimelineSkipped => Strings.Issue_EmptyTimelineSkipped,
        BuildIssueCode.TooManyTimelines => Strings.Issue_TooManyTimelines,
        BuildIssueCode.TimelineKindMismatch => Strings.Issue_TimelineKindMismatch,
        BuildIssueCode.MissingMedia => Strings.Issue_MissingMedia,
        BuildIssueCode.WrongHeight => string.Format(Strings.Issue_WrongHeight, issue.Detail),
        BuildIssueCode.StretchingTooLarge => Strings.Issue_StretchingTooLarge,
        BuildIssueCode.NoLedLayout => Strings.Issue_NoLedLayout,
        BuildIssueCode.VideoPairEndsDiffer => Strings.Issue_VideoPairEndsDiffer,
        BuildIssueCode.SharedBlockOffsets => Strings.Issue_SharedBlockOffsets,
        BuildIssueCode.LightStepNotMultiple => Strings.Issue_LightStepNotMultiple,
        BuildIssueCode.UnknownEffect => Strings.Issue_UnknownEffect,
        BuildIssueCode.LightTooLong => Strings.Issue_LightTooLong,
        BuildIssueCode.TooBig => string.Format(Strings.Issue_TooBig, Size(issue.Detail ?? 0)),
        _ => issue.Code.ToString(),
    };

    /// <summary>
    /// How long the device holds one column of the picture (poi, hoop, fan), one colour of a gradient (E-Pic) or one frame of an
    /// animation (video), at the item's stretching: the speed the stretching sets.
    /// </summary>
    public string ColumnTimeText
    {
        get
        {
            if (Item is not { Kind: ItemKind.Image or ItemKind.Gradient or ItemKind.Video } i) return "";
            return i.Kind switch
            {
                ItemKind.Video => string.Format(Strings.Inspector_FrameLasts, Span(TimeSpan.FromMilliseconds(i.Stretching)),
                    (1000.0 / i.Stretching).ToString("0.##", CultureInfo.CurrentCulture)),
                ItemKind.Gradient => string.Format(Strings.Inspector_ColourLasts, Span(Core.Imaging.ItemPreview.EpicPassDuration(1, i.Stretching))),
                _ => string.Format(Strings.Inspector_ColumnLasts, Span(Core.Imaging.ItemPreview.PassDuration(1, i.Stretching))),
            };
        }
    }

    /// <summary>"200 µs", "0.8 ms", "40 ms".</summary>
    public static string Span(TimeSpan time) => time < TimeSpan.FromMilliseconds(1)
        ? (time.TotalMilliseconds * 1000).ToString("0.##", CultureInfo.CurrentCulture) + " µs"
        : $"{time.TotalMilliseconds.ToString("0.##", CultureInfo.CurrentCulture)} ms";

    /// <summary>"512 B", "1.2 KB", "3.6 MB".</summary>
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => (bytes / 1024.0).ToString("0.#", CultureInfo.CurrentCulture) + " KB",
        _ => (bytes / 1024.0 / 1024.0).ToString("0.#", CultureInfo.CurrentCulture) + " MB",
    };
}
