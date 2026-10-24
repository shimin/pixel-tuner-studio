using System.Globalization;
using System.Text;
using PixelTuner.Core.Media;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Legacy;

/// <summary>A PixelTuner 2 project turned into a <see cref="ShowProject"/> and its media.</summary>
public sealed class PtpImportResult
{
    public required ShowProject Project { get; init; }

    /// <summary>The media the project refers to, ready to be packed.</summary>
    public required MediaCollection Media { get; init; }

    /// <summary>Files the project names that were not found, as the project names them, each once.</summary>
    public required IReadOnlyList<string> Missing { get; init; }

    /// <summary>Things that could not be imported, in words for the user.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>
/// Imports a PixelTuner 2 project (.ptp): timelines, items, LED markup, the canvas drawing and
/// the soundtrack. Media that is not found stays in the project by name (item SourceName,
/// project AudioSourceName) and is listed in <see cref="PtpImportResult.Missing"/>, so the
/// user can be asked once and the show imported again.
/// </summary>
public static class PtpImport
{
    private static readonly HashSet<string> TimelineAttributes =
        ["type", "LineTag", "LineColorTagXml", "IsFlippedVertical", "IsMonochrome", "MetaDataPath"];

    private static readonly HashSet<string> ItemAttributes =
        ["type", "Start", "End", "FullName", "Stretching", "Brightness", "LaserOn", "XOffset", "YOffset", "Color", "EffectID"];

    /// <param name="name">Name of the show, usually the file name without extension.</param>
    /// <param name="resolveMedia">Readable path of a file the project names, or null when it is not found.</param>
    public static PtpImportResult Import(PtpDocument document, string name, Func<string, string?> resolveMedia)
    {
        var media = new MediaCollection();
        var missing = new List<string>();
        var warnings = new List<string>();
        var ids = new Dictionary<string, string?>(StringComparer.Ordinal);

        string? Add(string sourceName)
        {
            if (ids.TryGetValue(sourceName, out var known)) return known;
            string? path = resolveMedia(sourceName);
            string? id = path is null ? null : media.AddFile(path);
            if (id is null && !missing.Contains(sourceName, StringComparer.OrdinalIgnoreCase)) missing.Add(sourceName);
            return ids[sourceName] = id;
        }

        var project = new ShowProject { Name = name };
        if (document.AudioPath is { } audio)
        {
            project.AudioResourceId = Add(audio);
            project.AudioSourceName = audio;
        }
        project.Extra = Extra(document.Attributes, ["AudioPath"]);

        if (document.CanvasDrawing is not null || document.CanvasElements.Count > 0)
        {
            project.Canvas = new ShowCanvas
            {
                DrawingResourceId = document.CanvasDrawing is { } svg
                    ? media.AddContent(Encoding.UTF8.GetBytes(svg), "canvas.svg")
                    : null,
                Layouts = document.CanvasElements.Values.Select(e => new LedLayout
                {
                    Id = e.Path,
                    Columns = e.Columns,
                    Rows = e.Rows,
                    TileMargin = e.TilesMargin,
                    Tiles = e.Tiles.Select(t => new LedTile { Index = t.Index, Number = t.LedNumber, Selected = t.Selected }).ToList(),
                }).ToList(),
            };
        }

        foreach (var line in document.Timelines)
        {
            string? kind = line.Type switch
            {
                PtpTimeline.PixelType => TimelineKind.Pixel,
                PtpTimeline.VideoType => TimelineKind.Video,
                PtpTimeline.EpicType => TimelineKind.Epic,
                PtpTimeline.LightType => TimelineKind.Light,
                _ => null,
            };
            if (kind is null)
            {
                warnings.Add($"Timeline of unknown type '{line.Type}' skipped.");
                continue;
            }

            var (_, r, g, b) = line.Color;
            var timeline = new Timeline
            {
                Name = line.Attributes.GetValueOrDefault("LineTag") ?? "",
                Kind = kind,
                Color = ColorText.Format(new(r, g, b)),
                FlipVertical = line.IsFlippedVertical,
                Monochrome = line.Attributes.TryGetValue("IsMonochrome", out var mono) && bool.TryParse(mono, out bool m) && m,
                LayoutId = line.Attributes.GetValueOrDefault("MetaDataPath") is { Length: > 0 } layout ? layout : null,
                PreviewVisible = line.PreviewVisible,
                Extra = Extra(line.Attributes, TimelineAttributes),
            };

            foreach (var source in line.Items)
            {
                var item = new TimelineItem
                {
                    Start = source.Start,
                    Duration = source.End - source.Start,
                    Stretching = source.Stretching,
                    Brightness = source.Brightness,
                    LaserOn = source.LaserOn,
                    Extra = Extra(source.Attributes, ItemAttributes),
                };
                switch (source.Type)
                {
                    case "" or "TimeLineItem" when kind == TimelineKind.Pixel:
                        item.Kind = ItemKind.Image;
                        break;
                    case "TimeLineItemVideo":
                        item.Kind = ItemKind.Video;
                        item.OffsetX = IntAttribute(source, "XOffset");
                        item.OffsetY = IntAttribute(source, "YOffset");
                        break;
                    case "TimeLineItemGradient":
                        item.Kind = ItemKind.Gradient;
                        break;
                    case PtpItem.SolidType:
                        item.Kind = ItemKind.Solid;
                        item.Color = ColorText.Format(ColorText.Parse(source.Attributes.GetValueOrDefault("Color")));
                        break;
                    case PtpItem.EffectType:
                        item.Kind = ItemKind.Effect;
                        item.Effect = source.Attributes.GetValueOrDefault("EffectID") ?? "";
                        item.Color = ColorText.Format(ColorText.Parse(source.Data.GetValueOrDefault("ColorFirst")));
                        item.ColorLast = ColorText.Format(ColorText.Parse(source.Data.GetValueOrDefault("ColorLast")));
                        item.T1 = int.TryParse(source.Data.GetValueOrDefault("T1"), CultureInfo.InvariantCulture, out int t1) ? t1 : 0;
                        item.T2 = int.TryParse(source.Data.GetValueOrDefault("T2"), CultureInfo.InvariantCulture, out int t2) ? t2 : 0;
                        if (item.Effect is not (EffectKind.LinearTransition or EffectKind.SineWave
                            or EffectKind.SquareWave or EffectKind.SawToothWave))
                            warnings.Add($"Unknown light effect '{item.Effect}' at {item.Start}.");
                        break;
                    default:
                        warnings.Add($"Item of type '{source.Type}' on a {kind} timeline at {source.Start} skipped.");
                        continue;
                }
                if (item.Kind is ItemKind.Image or ItemKind.Video or ItemKind.Gradient)
                {
                    item.SourceName = source.FullName;
                    item.ResourceId = Add(source.FullName);
                }
                timeline.Items.Add(item);
            }
            project.Timelines.Add(timeline);
        }

        return new PtpImportResult { Project = project, Media = media, Missing = missing, Warnings = warnings };
    }

    private static int IntAttribute(PtpItem item, string name) =>
        int.TryParse(item.Attributes.GetValueOrDefault(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static Dictionary<string, string>? Extra(IReadOnlyDictionary<string, string> attributes, IReadOnlyCollection<string> known)
    {
        var extra = attributes.Where(a => !known.Contains(a.Key)).ToDictionary(a => a.Key, a => a.Value);
        return extra.Count == 0 ? null : extra;
    }
}
