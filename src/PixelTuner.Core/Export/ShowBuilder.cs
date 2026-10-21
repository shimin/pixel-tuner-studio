using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Export;

public enum BuildSeverity
{
    /// <summary>The .bin is built, but the user should know.</summary>
    Warning,

    /// <summary>No .bin: something cannot be written to the file.</summary>
    Error,
}

/// <summary>What the editor can say about a show before it is sent to a device. The message text is the UI's business.</summary>
public enum BuildIssueCode
{
    /// <summary>The device has no timelines, or none of them has an item.</summary>
    NothingToPlay,

    /// <summary>A timeline without items is left out of the file.</summary>
    EmptyTimelineSkipped,

    TooManyTimelines,
    TimelineKindMismatch,

    /// <summary>A picture file of the project is missing or cannot be read. <see cref="BuildIssue.ItemId"/> names the item.</summary>
    MissingMedia,

    /// <summary>The timeline's pictures are not as high as the device is. <see cref="BuildIssue.Detail"/> is the height.</summary>
    WrongHeight,

    /// <summary>A poi or a hoop plays no stretching below 4.</summary>
    StretchingTooSmall,

    /// <summary>E-Pic stores the stretching times 5 in 16 bits.</summary>
    StretchingTooLarge,

    NoLedLayout,
    VideoPairEndsDiffer,

    /// <summary>Items with the same GIF and brightness share one picture, so the offset of the later ones is ignored.</summary>
    SharedBlockOffsets,

    /// <summary>The item does not last a whole number of light steps, so it shifts the items after it.</summary>
    LightStepNotMultiple,

    UnknownEffect,

    /// <summary>The show is longer than the LightMaster can sample at this step.</summary>
    LightTooLong,

    /// <summary>The file does not fit in the device. <see cref="BuildIssue.Detail"/> is the size.</summary>
    TooBig,
}

/// <param name="TimelineId">The timeline the issue is about, if any.</param>
/// <param name="ItemId">The item the issue is about, if any.</param>
/// <param name="Detail">A number the message needs (a height, a size), if any.</param>
public sealed record BuildIssue(BuildSeverity Severity, BuildIssueCode Code, Guid? TimelineId = null, Guid? ItemId = null, long? Detail = null);

/// <param name="Bin">The file; null when there is an error.</param>
public sealed record BuildResult(byte[]? Bin, IReadOnlyList<BuildIssue> Issues)
{
    public bool Succeeded => Bin is not null;

    public IEnumerable<BuildIssue> Errors => Issues.Where(i => i.Severity == BuildSeverity.Error);

    public IEnumerable<BuildIssue> Warnings => Issues.Where(i => i.Severity == BuildSeverity.Warning);
}

/// <summary>
/// The one way a project becomes a .bin: for the export to a file and for the upload to a device. It reads the
/// timelines a device plays and the build settings from the project, checks everything the file format and the device
/// cannot take, and only then calls the exports that are checked against PixelTuner 2.
/// </summary>
public sealed class ShowBuilder(ShowProject project, ShowMedia? media)
{
    /// <summary>The file for a device of the project.</summary>
    public BuildResult Build(DeviceBinding device)
    {
        var timelines = device.TimelineIds
            .Select(id => project.Timelines.FirstOrDefault(t => t.Id == id))
            .OfType<Timeline>()
            .ToList();
        return Build(device.DeviceType, timelines, device.LedCount, device.MemoryBytes);
    }

    /// <summary>The file for a kind of device and the timelines it plays, in order.</summary>
    /// <param name="ledCount">LEDs of a poi or a hoop; 0 takes the height of the first timeline.</param>
    /// <param name="memoryBytes">What the device can hold; 0 when not known.</param>
    public BuildResult Build(string deviceKind, IReadOnlyList<Timeline> timelines, int ledCount = 0, long memoryBytes = 0)
    {
        string kind = Edits.TimelineKindOf(deviceKind);
        var issues = new List<BuildIssue>();
        Check(deviceKind, kind, timelines, ledCount, issues);
        if (issues.Any(i => i.Severity == BuildSeverity.Error)) return new BuildResult(null, issues);

        byte[] bin;
        try
        {
            bin = Export(deviceKind, timelines, ledCount, issues);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException)
        {
            issues.Add(new BuildIssue(BuildSeverity.Error, BuildIssueCode.MissingMedia));
            return new BuildResult(null, issues);
        }
        catch (NotSupportedException) when (deviceKind == DeviceKind.Light)
        {
            issues.Add(new BuildIssue(BuildSeverity.Error, BuildIssueCode.LightTooLong));
            return new BuildResult(null, issues);
        }
        catch (NotSupportedException)
        {
            issues.Add(new BuildIssue(BuildSeverity.Error, BuildIssueCode.VideoPairEndsDiffer));
            return new BuildResult(null, issues);
        }

        if (memoryBytes > 0 && bin.Length > memoryBytes)
        {
            issues.Add(new BuildIssue(BuildSeverity.Error, BuildIssueCode.TooBig, Detail: bin.Length));
            return new BuildResult(null, issues);
        }
        return new BuildResult(bin, issues);
    }

    private byte[] Export(string deviceKind, IReadOnlyList<Timeline> timelines, int ledCount, List<BuildIssue> issues)
    {
        var settings = project.Build;
        var used = timelines.Where(t => t.Items.Count > 0).ToList();
        switch (deviceKind)
        {
            case DeviceKind.Poi or DeviceKind.Hoop:
            {
                var export = new PixelExport(Media()) { Depth = settings.Depth, GapStretching = (ushort)settings.GapStretching };
                return export.Export(used, (ushort)HeightOf(export, used, ledCount));
            }
            case DeviceKind.Fan:
                return new PixelExport(Media()) { Depth = settings.Depth, GapStretching = (ushort)settings.GapStretching }.ExportFan(used[0]);
            case DeviceKind.Video:
                return new VideoExport(project, Media()) { Depth = settings.Depth, GapStretching = (ushort)settings.GapStretching }.Export(used);
            case DeviceKind.EpicPoi:
                return new EpicExport(Media()) { Depth = settings.Depth, GapStretching = (ushort)settings.GapStretching }.Export(used);
            default:
                return LightExport.Export(used, settings.LightRate);
        }
    }

    private ShowMedia Media() => media ?? throw new InvalidOperationException("The project has no media to build from.");

    /// <summary>H of a pixel .bin: the device's LEDs, else the height of the first timeline.</summary>
    private static int HeightOf(PixelExport export, List<Timeline> used, int ledCount)
    {
        if (ledCount > 0) return ledCount;
        int known = Edits.PixelHeight(used[0]);
        return known > 0 ? known : export.TimelineHeight(used[0]);
    }

    private void Check(string deviceKind, string kind, IReadOnlyList<Timeline> timelines, int ledCount, List<BuildIssue> issues)
    {
        void Error(BuildIssueCode code, Timeline? t = null, TimelineItem? i = null, long? detail = null) =>
            issues.Add(new BuildIssue(BuildSeverity.Error, code, t?.Id, i?.Id, detail));
        void Warn(BuildIssueCode code, Timeline? t = null, TimelineItem? i = null) =>
            issues.Add(new BuildIssue(BuildSeverity.Warning, code, t?.Id, i?.Id));

        if (timelines.Count > Edits.MaxTimelinesOf(deviceKind)) Error(BuildIssueCode.TooManyTimelines);
        foreach (var timeline in timelines)
        {
            if (timeline.Kind != kind) Error(BuildIssueCode.TimelineKindMismatch, timeline);
            else if (timeline.Items.Count == 0) Warn(BuildIssueCode.EmptyTimelineSkipped, timeline);
        }
        if (timelines.All(t => t.Items.Count == 0)) Error(BuildIssueCode.NothingToPlay);
        if (issues.Any(i => i.Severity == BuildSeverity.Error)) return;

        foreach (var timeline in timelines.Where(t => t.Items.Count > 0))
        {
            foreach (var item in timeline.Items)
            {
                if (item.Kind is ItemKind.Image or ItemKind.Video or ItemKind.Gradient
                    && (item.ResourceId is null || media is null || !HasMedia(item.ResourceId)))
                    Error(BuildIssueCode.MissingMedia, timeline, item);
                if (deviceKind is DeviceKind.Poi or DeviceKind.Hoop && item.Stretching < Edits.MinPixelStretching)
                    Error(BuildIssueCode.StretchingTooSmall, timeline, item);
                if (item.Kind == ItemKind.Gradient && item.Stretching > Edits.MaxEpicStretching)
                    Error(BuildIssueCode.StretchingTooLarge, timeline, item);
                if (item.Kind == ItemKind.Effect && item.Effect is not (LightEffectKind.LinearTransition or LightEffectKind.SineWave
                    or LightEffectKind.SquareWave or LightEffectKind.SawToothWave))
                    Error(BuildIssueCode.UnknownEffect, timeline, item);
                if (kind == TimelineKind.Light && item.Kind is ItemKind.Solid or ItemKind.Effect
                    && (long)item.Duration.TotalMilliseconds % project.Build.LightRate != 0)
                    Warn(BuildIssueCode.LightStepNotMultiple, timeline, item);
            }
            if (kind == TimelineKind.Video && Layout(timeline) is null) Error(BuildIssueCode.NoLedLayout, timeline);
            if (deviceKind is DeviceKind.Poi or DeviceKind.Hoop)
            {
                int height = ledCount > 0 ? ledCount : Edits.PixelHeight(timelines.First(t => t.Items.Count > 0));
                if (Edits.PixelHeight(timeline) is > 0 and var own && height > 0 && own != height)
                    Error(BuildIssueCode.WrongHeight, timeline, detail: own);
            }
            if (kind == TimelineKind.Video)
            {
                foreach (var group in timeline.Items.Where(i => i.Kind == ItemKind.Video)
                             .GroupBy(i => (i.ResourceId, i.SourcePath?.ToLowerInvariant(), i.Brightness)))
                {
                    var first = group.First();
                    foreach (var other in group.Skip(1).Where(o => (o.OffsetX, o.OffsetY) != (first.OffsetX, first.OffsetY)))
                        Warn(BuildIssueCode.SharedBlockOffsets, timeline, other);
                }
            }
        }

        if (deviceKind == DeviceKind.Video && timelines.Count(t => t.Items.Count > 0) == 2
            && timelines.Where(t => t.Items.Count > 0).Select(t => t.Items[^1].End).Distinct().Count() > 1)
            Error(BuildIssueCode.VideoPairEndsDiffer);
    }

    private LedLayout? Layout(Timeline timeline) =>
        timeline.LedLayoutId is { } id ? project.LedLayouts.FirstOrDefault(l => l.Id == id && l.Leds.Count > 0) : null;

    private readonly Dictionary<string, bool> _hasMedia = new(StringComparer.Ordinal);

    private bool HasMedia(string id)
    {
        if (_hasMedia.TryGetValue(id, out bool has)) return has;
        try
        {
            using var stream = media!.Open(id);
            has = stream is not null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or UnauthorizedAccessException)
        {
            has = false;
        }
        return _hasMedia[id] = has;
    }
}
