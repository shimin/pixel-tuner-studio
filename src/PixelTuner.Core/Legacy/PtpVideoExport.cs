using PixelTuner.Core.Export;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// The video-device .bin of a PixelTuner 2 project, straight from its files, through
/// <see cref="PtpConverter"/> and <see cref="VideoExport"/>.
/// </summary>
public sealed class PtpVideoExport
{
    private readonly LegacyTimelines _timelines;

    /// <param name="resolveMedia">Readable path of an item's file, or null when the file is missing.</param>
    public PtpVideoExport(PtpDocument document, Func<string, string?> resolveMedia) =>
        _timelines = new LegacyTimelines(resolveMedia, document);

    /// <inheritdoc cref="VideoExport.GapStretching"/>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>Video timelines of the project, in project order.</summary>
    public static IReadOnlyList<PtpTimeline> VideoTimelines(PtpDocument document) =>
        document.Timelines.Where(t => t.Type == PtpTimeline.VideoType).ToList();

    /// <summary>LED positions of a timeline's canvas element in wiring order; empty without markup.</summary>
    public IReadOnlyList<(int X, int Y)> Leds(PtpTimeline timeline) =>
        timeline.Attributes.TryGetValue("MetaDataPath", out var path)
        && _timelines.Converter.Document.CanvasElements.TryGetValue(path, out var element)
            ? element.OrderedLeds()
            : [];

    /// <inheritdoc cref="VideoExport.Export"/>
    public byte[] Export(IReadOnlyList<PtpTimeline> timelines)
    {
        var layouts = _timelines.Converter.LedLayouts;
        return new VideoExport(id => layouts.FirstOrDefault(l => l.Id == id), ShowMedia.FromFiles())
            { GapStretching = GapStretching, Depth = Depth }
            .Export(timelines.Select(t => _timelines[t]).ToList());
    }
}
