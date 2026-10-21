using PixelTuner.Core.Export;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// The E-Pic poi .bin of a PixelTuner 2 project, straight from its files, through
/// <see cref="PtpConverter"/> and <see cref="EpicExport"/>.
/// </summary>
public sealed class PtpEpicExport
{
    private readonly LegacyTimelines _timelines;

    /// <param name="resolveMedia">Readable path of an item's file, or null when the file is missing.</param>
    public PtpEpicExport(Func<string, string?> resolveMedia) => _timelines = new LegacyTimelines(resolveMedia);

    /// <inheritdoc cref="EpicExport.GapStretching"/>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>E-Pic timelines of the project, in project order.</summary>
    public static IReadOnlyList<PtpTimeline> EpicTimelines(PtpDocument document) =>
        document.Timelines.Where(t => t.Type == PtpTimeline.EpicType).ToList();

    public byte[] Export(IEnumerable<PtpTimeline> timelines) =>
        new EpicExport(ShowMedia.FromFiles()) { GapStretching = GapStretching, Depth = Depth }
            .Export(timelines.Select(t => _timelines[t]));
}
