using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Export;

/// <summary>Where one timeline item lands in the .bin of the device that plays it, and how big that file is.</summary>
/// <param name="EntryNumber">1-based number of the item's entry on its timeline in the file (pauses are entries too); null for light, which has samples and no entries.</param>
/// <param name="EntryCount">Entries of that timeline in the file, pauses included.</param>
/// <param name="BlockSize">Bytes of the item's picture block; null for light.</param>
/// <param name="SharedWithItems">Other items that play the same block (the same file at the same brightness, not told apart by their offsets).</param>
/// <param name="SharedWithPauses">Pause entries that play the same block (a video pair's pauses).</param>
/// <param name="FileSize">Bytes of the whole file.</param>
/// <param name="MemoryBytes">What the device can hold; 0 when not known.</param>
/// <param name="Issues">What the build says about the item (and, with no item, about the file): errors and warnings.</param>
public sealed record ItemBinInfo(
    int? EntryNumber, int EntryCount, int? BlockSize, IReadOnlyList<Guid> SharedWithItems, int SharedWithPauses,
    long FileSize, long MemoryBytes, IReadOnlyList<BuildIssue> Issues)
{
    public bool Built => FileSize > 0;
}

/// <summary>Builds the file an item is part of and tells where the item is in it. This is what the inspector's ".bin" line shows.</summary>
public static class BinInfo
{
    /// <summary>
    /// The device that plays the timeline: the first device of the project that lists it, else one made up for the kind of
    /// timeline (a poi of the timeline's height, so there is always a file to talk about).
    /// </summary>
    public static DeviceBinding DeviceFor(ShowProject project, Timeline timeline) =>
        project.Devices.FirstOrDefault(d => d.TimelineIds.Contains(timeline.Id))
        ?? new DeviceBinding
        {
            DeviceType = timeline.Kind switch
            {
                TimelineKind.Video => DeviceKind.Video,
                TimelineKind.EpicPoi => DeviceKind.EpicPoi,
                TimelineKind.Light => DeviceKind.Light,
                _ => DeviceKind.Poi,
            },
            LedCount = Edits.PixelHeight(timeline),
            TimelineIds = [timeline.Id],
        };

    public static ItemBinInfo Of(ShowProject project, ShowMedia media, Timeline timeline, TimelineItem item)
    {
        var device = DeviceFor(project, timeline);
        var result = new ShowBuilder(project, media).Build(device);
        var issues = result.Issues.Where(i => i.ItemId == item.Id || (i.ItemId is null && i.TimelineId is null)).ToList();
        if (result.Bin is not { } bin)
            return new ItemBinInfo(null, 0, null, [], 0, 0, device.MemoryBytes, issues);

        if (device.DeviceType == DeviceKind.Light)
            return new ItemBinInfo(null, 0, null, [], 0, bin.Length, device.MemoryBytes, issues);

        var file = BinReader.ReadPpoi(bin);
        var inFile = device.TimelineIds
            .Select(id => project.Timelines.FirstOrDefault(t => t.Id == id))
            .OfType<Timeline>().Where(t => t.Items.Count > 0).ToList();
        // A video pair is one timeline in the file; its entries are cut at every end of either timeline.
        int line = file.Timelines.Count == 1 ? 0 : Math.Max(0, inFile.IndexOf(timeline));
        var entries = file.Timelines[line].Entries;
        uint start = (uint)Math.Round(item.Start.TotalMilliseconds);
        var entry = entries.LastOrDefault(e => e.StartMs <= start) ?? entries[0];

        var sharedItems = new List<Guid>();
        int sharedPauses = 0;
        var lineOf = new Dictionary<Timeline, int>();
        for (int t = 0; t < inFile.Count && t < file.Timelines.Count; t++) lineOf[inFile[t]] = file.Timelines.Count == 1 ? 0 : t;
        foreach (var other in file.Timelines.SelectMany((l, n) => l.Entries.Select(e => (Line: n, Entry: e))))
        {
            if (other.Entry.ImageAddress != entry.ImageAddress || (other.Line == line && other.Entry.Index == entry.Index)) continue;
            var owner = inFile.Where(t => lineOf.TryGetValue(t, out int l) && l == other.Line)
                .SelectMany(t => t.Items).FirstOrDefault(i => !ReferenceEquals(i, item) && (uint)Math.Round(i.Start.TotalMilliseconds) == other.Entry.StartMs);
            if (owner is not null) sharedItems.Add(owner.Id);
            else sharedPauses++;
        }
        return new ItemBinInfo(entry.Index + 1, entries.Count, entry.Image.BlockSize, sharedItems, sharedPauses, bin.Length, device.MemoryBytes, issues);
    }
}
