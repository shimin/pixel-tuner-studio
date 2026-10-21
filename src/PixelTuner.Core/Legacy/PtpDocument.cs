using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// A PixelTuner 2 project (.ptp): the XML that PixelTuner 2 wrote with XmlSerializer.
/// Read with LINQ to XML, so it works under AOT on iOS. Only what the new app needs is
/// typed; everything else stays available as raw attributes.
/// </summary>
public sealed partial class PtpDocument
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public List<PtpTimeline> Timelines { get; } = [];

    /// <summary>Canvas elements (device outlines with their LED markup), by their Path id.</summary>
    public Dictionary<string, PtpCanvasElement> CanvasElements { get; } = new(StringComparer.Ordinal);

    public static PtpDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static PtpDocument Load(Stream stream)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(stream, settings);
        var root = XDocument.Load(reader).Root
            ?? throw new InvalidDataException("The project file is empty.");
        if (root.Name.LocalName != "FileViewModel")
            throw new InvalidDataException("This is not a PixelTuner 2 project.");

        var document = new PtpDocument();
        foreach (var element in root.Element("Canvas")?.Element("Elements")?.Elements("CanvasElementViewModel") ?? [])
        {
            var canvasElement = new PtpCanvasElement
            {
                Path = (string?)element.Attribute("Path") ?? "",
                Columns = ParseInt((string?)element.Attribute("ColumnsCount"), 0),
                Rows = ParseInt((string?)element.Attribute("RowsCount"), 0),
            };
            foreach (var tile in element.Element("LedTiles")?.Elements("LedTile") ?? [])
            {
                canvasElement.Tiles.Add(new PtpLedTile(
                    ParseInt((string?)tile.Attribute("Index"), 0),
                    ParseInt((string?)tile.Attribute("LedNumber"), 0),
                    ParseBool((string?)tile.Attribute("Selected"))));
            }
            // XmlSerializer kept the last one when a path repeated; so do we.
            document.CanvasElements[canvasElement.Path] = canvasElement;
        }
        foreach (var line in root.Elements("Line"))
        {
            var timeline = new PtpTimeline
            {
                Type = (string?)line.Attribute(Xsi + "type") ?? "",
                Color = ParseArgb((string?)line.Attribute("LineColorTagXml")),
                IsFlippedVertical = ParseBool((string?)line.Attribute("IsFlippedVertical")),
                Attributes = line.Attributes().Where(a => !a.IsNamespaceDeclaration)
                    .ToDictionary(a => a.Name.LocalName, a => a.Value),
            };
            foreach (var item in line.Elements("Item"))
            {
                timeline.Items.Add(new PtpItem
                {
                    Start = ParseTime((string?)item.Attribute("Start")),
                    End = ParseTime((string?)item.Attribute("End")),
                    FullName = (string?)item.Attribute("FullName") ?? "",
                    Stretching = ParseInt((string?)item.Attribute("Stretching"), 1),
                    Brightness = (byte)ParseInt((string?)item.Attribute("Brightness"), 100),
                    LaserOn = ParseBool((string?)item.Attribute("LaserOn")),
                    Attributes = item.Attributes().Where(a => !a.IsNamespaceDeclaration)
                        .ToDictionary(a => a.Name.LocalName, a => a.Value),
                });
            }
            document.Timelines.Add(timeline);
        }
        return document;
    }

    /// <summary>PixelTuner 2 wrote times as HH:mm:ss.fff from the start of the show.</summary>
    internal static TimeSpan ParseTime(string? value)
    {
        if (string.IsNullOrEmpty(value)) return TimeSpan.Zero;
        var m = TimeRegex().Match(value);
        if (!m.Success) throw new InvalidDataException($"Bad time value '{value}'.");
        return new TimeSpan(0, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>"#AARRGGBB" as WPF wrote colours. PixelTuner 2 defaulted to blue.</summary>
    internal static (byte A, byte R, byte G, byte B) ParseArgb(string? value)
    {
        if (value is { Length: 9 } && value[0] == '#'
            && uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            return ((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return (255, 0, 0, 255);
    }

    private static bool ParseBool(string? value) =>
        value is not null && bool.TryParse(value, out bool b) && b;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    [GeneratedRegex(@"(\d{2}):(\d{2}):(\d{2})\.(\d{3})")]
    private static partial Regex TimeRegex();
}

public sealed class PtpTimeline
{
    public const string PixelType = "PixelTimeLineViewModel";
    public const string VideoType = "VideoTimeLineViewModel";

    /// <summary>The view model type PixelTuner 2 stored, for example PixelTimeLineViewModel.</summary>
    public string Type { get; init; } = "";

    public (byte A, byte R, byte G, byte B) Color { get; init; }

    public bool IsFlippedVertical { get; init; }

    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();

    public List<PtpItem> Items { get; } = [];
}

/// <summary>
/// A device outline on the PixelTuner 2 canvas with its LED markup: a grid of tiles,
/// some of them LEDs with a number in the order the device wires them.
/// </summary>
public sealed class PtpCanvasElement
{
    public string Path { get; init; } = "";
    public int Columns { get; init; }
    public int Rows { get; init; }
    public List<PtpLedTile> Tiles { get; } = [];

    /// <summary>
    /// Grid positions (column, row) of the LEDs in device order, as PixelTuner 2's
    /// OrderedLeds: selected tiles sorted by number, ties kept in tile order.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> OrderedLeds()
    {
        if (Columns <= 0) return [];
        return Tiles.OrderBy(t => t.LedNumber)            // OrderBy is stable, like LINQ in PixelTuner 2
            .Where(t => t.Selected)
            .Select(t => (t.Index % Columns, t.Index / Columns))
            .ToList();
    }
}

public readonly record struct PtpLedTile(int Index, int LedNumber, bool Selected);

public sealed class PtpItem
{
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }

    /// <summary>Media path as stored: relative ("./a.bmp") or an absolute Windows path.</summary>
    public string FullName { get; init; } = "";

    public int Stretching { get; init; } = 1;
    public byte Brightness { get; init; } = 100;
    public bool LaserOn { get; init; }

    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
}
