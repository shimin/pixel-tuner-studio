using System.Globalization;
using System.Text;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Layout;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// Turns a PixelTuner 2 project into the stored model: timelines, items and LED markup
/// with everything the .bin export needs. Media is handed to the caller, which decides
/// what a resource id is (a hash in a package, or simply the file path).
/// </summary>
public sealed class PtpConverter
{
    private readonly Func<string, string?> _media;
    private readonly Dictionary<string, string?> _resolved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _layoutIds = new(StringComparer.Ordinal);

    /// <param name="document">The project; its LED markup becomes the layouts video timelines refer to.</param>
    /// <param name="media">
    /// Resource id for a stored media path (relative or an absolute Windows path), or null when
    /// the file is missing. Called once per distinct path.
    /// </param>
    public PtpConverter(PtpDocument document, Func<string, string?> media)
    {
        Document = document;
        _media = media;
        foreach (var element in document.CanvasElements.Values)
        {
            var layout = new LedLayout
            {
                Name = element.Path,
                Columns = element.Columns,
                Rows = element.Rows,
                Leds = element.OrderedLeds().Select(p => p.Y * element.Columns + p.X).ToList(),
                TilesMargin = element.TilesMargin,
            };
            // What the editor of PixelTuner 2 reported when it opened the element: gaps and repeats of the stored numbers.
            if (element.Columns > 0 && element.Rows > 0)
            {
                LedMarkup.FromNumbers(element.Columns, element.Rows, element.Tiles.Where(t => t.Selected).Select(t => (t.Index, t.LedNumber)), out var issues);
                if (!issues.IsClean) layout.SourceIssues = issues;
            }
            LedLayouts.Add(layout);
            _layoutIds[element.Path] = layout.Id;
        }
    }

    public PtpDocument Document { get; }

    /// <summary>One layout per canvas element, in file order.</summary>
    public List<LedLayout> LedLayouts { get; } = [];

    /// <summary>
    /// The whole project. <paramref name="addContent"/> stores data that is not a file of
    /// its own (the canvas drawing) and returns its resource id; without it the drawing is left out.
    /// </summary>
    public ShowProject ToProject(string name, Func<byte[], string, string>? addContent = null)
    {
        var project = new ShowProject
        {
            Name = name,
            AudioSourcePath = Document.AudioPath,
            AudioResourceId = Document.AudioPath is { } audio ? Resolve(audio) : null,
            Timelines = Document.Timelines.Select(Convert).ToList(),
            LedLayouts = LedLayouts,
        };
        if (addContent is not null && Document.CanvasContent.Length > 0)
            project.CanvasResourceId = addContent(Encoding.UTF8.GetBytes(Document.CanvasContent), "canvas.svg");
        return project;
    }

    /// <summary>The paths that <see cref="Resolve"/> could not find, in the order they were asked for.</summary>
    public IEnumerable<string> MissingMedia => _resolved.Where(p => p.Value is null).Select(p => p.Key);

    public Timeline Convert(PtpTimeline line)
    {
        var timeline = new Timeline
        {
            Name = line.Attributes.GetValueOrDefault("LineTag") ?? "",
            Kind = line.Type switch
            {
                PtpTimeline.PixelType => TimelineKind.Pixel,
                PtpTimeline.VideoType => TimelineKind.Video,
                PtpTimeline.EpicType => TimelineKind.EpicPoi,
                PtpTimeline.LightType => TimelineKind.Light,
                _ => line.Type,
            },
            Color = ColorText.FormatArgb(line.Color),
            FlipVertical = line.IsFlippedVertical,
            Monochrome = Bool(line.Attributes.GetValueOrDefault("IsMonochrome")),
            LedLayoutId = line.Attributes.TryGetValue("MetaDataPath", out var path) && _layoutIds.TryGetValue(path, out var id)
                ? id
                : null,
        };
        foreach (var item in line.Items) timeline.Items.Add(Convert(item));
        return timeline;
    }

    private TimelineItem Convert(PtpItem item)
    {
        string kind = item.Type switch
        {
            "TimeLineItemVideo" => ItemKind.Video,
            "TimeLineItemGradient" => ItemKind.Gradient,
            PtpItem.SolidType => ItemKind.Solid,
            PtpItem.EffectType => ItemKind.Effect,
            _ => ItemKind.Image,
        };
        var result = new TimelineItem
        {
            Kind = kind,
            Start = item.Start,
            Duration = item.End - item.Start,
            Stretching = item.Stretching,
            Brightness = item.Brightness,
            LaserOn = item.LaserOn,
            OffsetX = Int(item.Attributes.GetValueOrDefault("XOffset")),
            OffsetY = Int(item.Attributes.GetValueOrDefault("YOffset")),
        };
        switch (kind)
        {
            case ItemKind.Solid:
                result.Color = item.Attributes.GetValueOrDefault("Color");
                break;
            case ItemKind.Effect:
                string effect = item.Attributes.GetValueOrDefault("EffectID") ?? "";
                result.Effect = effect switch
                {
                    "Linear Transition" => LightEffectKind.LinearTransition,
                    "Sine Wave" => LightEffectKind.SineWave,
                    "Square Wave" => LightEffectKind.SquareWave,
                    "Saw-tooth Wave" => LightEffectKind.SawToothWave,
                    _ => effect,
                };
                result.Color = item.Data.GetValueOrDefault("ColorFirst");
                result.ColorLast = item.Data.GetValueOrDefault("ColorLast");
                result.T1 = Int(item.Data.GetValueOrDefault("T1"));
                result.T2 = Int(item.Data.GetValueOrDefault("T2"));
                break;
            default:
                // Light items point at the app's own thumbnails (pack://...); only media items have files.
                if (item.FullName.Length > 0)
                {
                    result.SourcePath = item.FullName;
                    result.ResourceId = Resolve(item.FullName);
                }
                break;
        }
        return result;
    }

    private string? Resolve(string path)
    {
        if (path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)) return null;
        if (!_resolved.TryGetValue(path, out var id))
            _resolved[path] = id = _media(path);
        return id;
    }

    private static bool Bool(string? value) => value is not null && bool.TryParse(value, out bool b) && b;

    private static int Int(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
}
