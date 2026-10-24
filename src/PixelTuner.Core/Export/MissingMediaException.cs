namespace PixelTuner.Core.Export;

/// <summary>An item's media is not in the show, and the device needs it.</summary>
public sealed class MissingMediaException(string? sourceName)
    : Exception($"The file '{sourceName}' is missing from the show.")
{
    /// <summary>The file as the show names it.</summary>
    public string? SourceName { get; } = sourceName;
}
