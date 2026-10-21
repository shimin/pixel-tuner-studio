using PixelTuner.Core.Export;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// The pixel-device .bin of a PixelTuner 2 project, straight from its files: the project is
/// converted with <see cref="PtpConverter"/> and exported with <see cref="PixelExport"/>,
/// the same code that exports an imported package.
/// </summary>
public sealed class PtpPixelExport
{
    private readonly ShowMedia _media = ShowMedia.FromFiles();
    private readonly LegacyTimelines _timelines;

    /// <param name="resolveMedia">
    /// Turns an item's stored path into a readable file path; throws <see cref="FileNotFoundException"/>
    /// when the file is missing. The export throws it too, but only for files it needs.
    /// </param>
    public PtpPixelExport(Func<string, string> resolveMedia) =>
        _timelines = new LegacyTimelines(path =>
        {
            try { return resolveMedia(path); }
            catch (FileNotFoundException) { return null; }
        });

    /// <inheritdoc cref="PixelExport.GapStretching"/>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>Pixel timelines of the project, in project order.</summary>
    public static IReadOnlyList<PtpTimeline> PixelTimelines(PtpDocument document) =>
        document.Timelines.Where(t => t.Type == PtpTimeline.PixelType).ToList();

    /// <inheritdoc cref="PixelExport.TimelineHeight"/>
    public int TimelineHeight(PtpTimeline timeline) => Exporter().TimelineHeight(_timelines[timeline]);

    /// <inheritdoc cref="PixelExport.Export"/>
    public byte[] Export(IEnumerable<PtpTimeline> timelines, ushort height) =>
        Exporter().Export(timelines.Select(t => _timelines[t]), height);

    /// <inheritdoc cref="PixelExport.ExportFan"/>
    public byte[] ExportFan(PtpTimeline timeline)
    {
        if (timeline.Type != PtpTimeline.PixelType)
            throw new ArgumentException("A fan plays a pixel timeline.", nameof(timeline));
        return Exporter().ExportFan(_timelines[timeline]);
    }

    private PixelExport Exporter() => new(_media) { GapStretching = GapStretching, Depth = Depth };
}

/// <summary>Converted timelines of PixelTuner 2 projects, each converted once.</summary>
internal sealed class LegacyTimelines(Func<string, string?> media, PtpDocument? document = null)
{
    private readonly Dictionary<PtpTimeline, Timeline> _converted = new(ReferenceEqualityComparer.Instance);
    private PtpConverter? _converter = document is null ? null : new PtpConverter(document, media);

    public PtpConverter Converter => _converter ??= new PtpConverter(new PtpDocument(), media);

    public Timeline this[PtpTimeline line]
    {
        get
        {
            if (!_converted.TryGetValue(line, out var timeline))
                _converted[line] = timeline = Converter.Convert(line);
            return timeline;
        }
    }
}
