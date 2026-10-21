using PixelTuner.Core.Export;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// The LightMaster .bin of a PixelTuner 2 project, through <see cref="PtpConverter"/>
/// and <see cref="LightExport"/>.
/// </summary>
public static class PtpLightExport
{
    /// <summary>Light timelines of the project, in project order.</summary>
    public static IReadOnlyList<PtpTimeline> LightTimelines(PtpDocument document) =>
        document.Timelines.Where(t => t.Type == PtpTimeline.LightType).ToList();

    /// <param name="rate">Sampling step in milliseconds, PixelTuner 2's "discreteness" setting.</param>
    public static byte[] Export(IEnumerable<PtpTimeline> timelines, int rate = 5)
    {
        // Light items have no media files.
        var converted = new LegacyTimelines(_ => null);
        return LightExport.Export(timelines.Select(t => converted[t]), rate);
    }
}
