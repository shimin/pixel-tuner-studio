using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace PixelTuner.Core.Layout;

/// <summary>An SVG transform as its matrix: x' = a·x + c·y + e, y' = b·x + d·y + f.</summary>
public readonly record struct Affine(double A, double B, double C, double D, double E, double F)
{
    public static Affine Identity { get; } = new(1, 0, 0, 1, 0, 0);

    /// <summary>The transform that does <paramref name="inner"/> first and this one after it.</summary>
    public Affine Then(Affine inner) => new(
        A * inner.A + C * inner.B, B * inner.A + D * inner.B,
        A * inner.C + C * inner.D, B * inner.C + D * inner.D,
        A * inner.E + C * inner.F + E, B * inner.E + D * inner.F + F);

    public (double X, double Y) Apply(double x, double y) => (A * x + C * y + E, B * x + D * y + F);
}

/// <summary>One outline of the drawing: a prop the LEDs can be marked up on. Its id names the layout that belongs to it.</summary>
/// <param name="Id">The id of the element in the SVG (a name made of the shape's number when it had none).</param>
/// <param name="PathData">The outline as SVG path data, in the coordinates of the shape itself.</param>
/// <param name="Transform">What the groups around it and its own transform do to those coordinates.</param>
/// <param name="Stroke">The stroke colour as written in the SVG, or null when it has none.</param>
public sealed record CanvasShape(string Id, string PathData, Affine Transform, string? Stroke)
{
    /// <summary>The rectangle around the outline in drawing units, groups' transforms included; empty for an outline with no points.</summary>
    public (double X, double Y, double Width, double Height) Bounds => SvgPath.Bounds(PathData, Transform) ?? default;
}

/// <summary>
/// The drawing of the props: an SVG, read as the outlines PixelTuner 2's canvas showed (its shapes, each with an id, under groups with
/// transforms). Paths, rectangles, circles, ellipses, lines, polylines and polygons are read; text, images, gradients, clipping and
/// <c>use</c> are not, and a shape that is hidden is left out. No drawing here: the UI turns the path data into geometry.
/// </summary>
public sealed class CanvasDrawing
{
    private CanvasDrawing(double x, double y, double width, double height, IReadOnlyList<CanvasShape> shapes)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Shapes = shapes;
    }

    /// <summary>The page in drawing units (the view box, or the width and height); empty when the SVG says neither.</summary>
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public IReadOnlyList<CanvasShape> Shapes { get; }

    public CanvasShape? Find(string id) => Shapes.FirstOrDefault(s => s.Id == id);

    /// <summary>Reads an SVG; null when the text is not an SVG document.</summary>
    public static CanvasDrawing? TryParse(string svg)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(svg, LoadOptions.None);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
        if (document.Root is not { } root || root.Name.LocalName != "svg") return null;

        double x = 0, y = 0, width = 0, height = 0;
        if (Numbers((string?)root.Attribute("viewBox")) is [var vx, var vy, var vw, var vh])
            (x, y, width, height) = (vx, vy, vw, vh);
        else if (Length((string?)root.Attribute("width")) is { } w && Length((string?)root.Attribute("height")) is { } h)
            (width, height) = (w, h);

        var shapes = new List<CanvasShape>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Walk(root, Affine.Identity, shapes, ids);
        return new CanvasDrawing(x, y, width, height, shapes);
    }

    private static void Walk(XElement parent, Affine transform, List<CanvasShape> shapes, HashSet<string> ids)
    {
        foreach (var element in parent.Elements())
        {
            string name = element.Name.LocalName;
            if (name is "defs" or "metadata" or "namedview" or "clipPath" or "mask" or "symbol" or "style" or "title" or "desc" or "marker" or "pattern") continue;
            if (IsHidden(element)) continue;

            var own = Parse(Style(element, "transform") ?? (string?)element.Attribute("transform"));
            var combined = transform.Then(own);
            if (name is "g" or "a" or "switch")
            {
                Walk(element, combined, shapes, ids);
                continue;
            }

            string? data = name switch
            {
                "path" => ((string?)element.Attribute("d"))?.Trim(),
                "rect" => Rect(element),
                "circle" => Round(element, Length((string?)element.Attribute("r")) ?? 0, Length((string?)element.Attribute("r")) ?? 0),
                "ellipse" => Round(element, Length((string?)element.Attribute("rx")) ?? 0, Length((string?)element.Attribute("ry")) ?? 0),
                "line" => Line(element),
                "polyline" => Points(element, close: false),
                "polygon" => Points(element, close: true),
                _ => null,
            };
            if (string.IsNullOrEmpty(data)) continue;

            string id = (string?)element.Attribute("id") is { Length: > 0 } given ? given : "shape" + (shapes.Count + 1).ToString(CultureInfo.InvariantCulture);
            if (!ids.Add(id)) continue;   // PixelTuner 2 kept one element per id too
            string? stroke = Style(element, "stroke") ?? (string?)element.Attribute("stroke");
            shapes.Add(new CanvasShape(id, data, combined, stroke is null or "none" ? null : stroke));
        }
    }

    // ---- attributes

    private static string? Style(XElement element, string property)
    {
        if ((string?)element.Attribute("style") is not { } style) return null;
        foreach (string declaration in style.Split(';'))
        {
            int colon = declaration.IndexOf(':');
            if (colon > 0 && declaration[..colon].Trim() == property) return declaration[(colon + 1)..].Trim();
        }
        return null;
    }

    private static bool IsHidden(XElement element) =>
        ((string?)element.Attribute("display") ?? Style(element, "display")) == "none"
        || ((string?)element.Attribute("visibility") ?? Style(element, "visibility")) is "hidden" or "collapse";

    /// <summary>A length in drawing units: user units, px, and the print units at 96 to the inch. Null when it is not a length.</summary>
    internal static double? Length(string? text)
    {
        if (text is null) return null;
        text = text.Trim();
        int end = text.Length;
        while (end > 0 && char.IsLetter(text[end - 1]) || end > 0 && text[end - 1] == '%') end--;
        if (!double.TryParse(text[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return null;
        return text[end..] switch
        {
            "" or "px" => value,
            "mm" => value * 96 / 25.4,
            "cm" => value * 96 / 2.54,
            "in" => value * 96,
            "pt" => value * 96 / 72,
            "pc" => value * 16,
            _ => null,
        };
    }

    private static double Number(XElement element, string attribute) => Length((string?)element.Attribute(attribute)) ?? 0;

    internal static double[] Numbers(string? text)
    {
        if (text is null) return [];
        var numbers = new List<double>();
        foreach (string part in text.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return [];
            numbers.Add(value);
        }
        return [.. numbers];
    }

    private static string F(double value) => value.ToString("0.#######", CultureInfo.InvariantCulture);

    // ---- shapes as path data

    private static string Rect(XElement e)
    {
        double x = Number(e, "x"), y = Number(e, "y"), w = Number(e, "width"), h = Number(e, "height");
        if (w <= 0 || h <= 0) return "";
        double? rx = Length((string?)e.Attribute("rx")), ry = Length((string?)e.Attribute("ry"));
        double radiusX = Math.Min(rx ?? ry ?? 0, w / 2), radiusY = Math.Min(ry ?? rx ?? 0, h / 2);
        if (radiusX <= 0 || radiusY <= 0) return $"M{F(x)},{F(y)}H{F(x + w)}V{F(y + h)}H{F(x)}Z";
        return $"M{F(x + radiusX)},{F(y)}H{F(x + w - radiusX)}A{F(radiusX)},{F(radiusY)} 0 0 1 {F(x + w)},{F(y + radiusY)}"
             + $"V{F(y + h - radiusY)}A{F(radiusX)},{F(radiusY)} 0 0 1 {F(x + w - radiusX)},{F(y + h)}"
             + $"H{F(x + radiusX)}A{F(radiusX)},{F(radiusY)} 0 0 1 {F(x)},{F(y + h - radiusY)}"
             + $"V{F(y + radiusY)}A{F(radiusX)},{F(radiusY)} 0 0 1 {F(x + radiusX)},{F(y)}Z";
    }

    private static string Round(XElement e, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0) return "";
        double cx = Number(e, "cx"), cy = Number(e, "cy");
        return $"M{F(cx - rx)},{F(cy)}A{F(rx)},{F(ry)} 0 1 0 {F(cx + rx)},{F(cy)}A{F(rx)},{F(ry)} 0 1 0 {F(cx - rx)},{F(cy)}Z";
    }

    private static string Line(XElement e) => $"M{F(Number(e, "x1"))},{F(Number(e, "y1"))}L{F(Number(e, "x2"))},{F(Number(e, "y2"))}";

    private static string Points(XElement e, bool close)
    {
        var numbers = Numbers((string?)e.Attribute("points"));
        if (numbers.Length < 4) return "";
        var builder = new StringBuilder();
        for (int i = 0; i + 1 < numbers.Length; i += 2)
            builder.Append(i == 0 ? 'M' : 'L').Append(F(numbers[i])).Append(',').Append(F(numbers[i + 1]));
        if (close) builder.Append('Z');
        return builder.ToString();
    }

    // ---- transforms

    /// <summary>A transform attribute (<c>translate(1 2) rotate(30)</c>) as one matrix; the identity for a text that is not one.</summary>
    public static Affine Parse(string? text)
    {
        var result = Affine.Identity;
        if (string.IsNullOrWhiteSpace(text)) return result;
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ',')) i++;
            int open = text.IndexOf('(', i), close = open < 0 ? -1 : text.IndexOf(')', open);
            if (open < 0 || close < 0) break;
            string function = text[i..open].Trim();
            var n = Numbers(text[(open + 1)..close]);
            i = close + 1;

            Affine? step = (function, n.Length) switch
            {
                ("matrix", 6) => new Affine(n[0], n[1], n[2], n[3], n[4], n[5]),
                ("translate", 1) => new Affine(1, 0, 0, 1, n[0], 0),
                ("translate", 2) => new Affine(1, 0, 0, 1, n[0], n[1]),
                ("scale", 1) => new Affine(n[0], 0, 0, n[0], 0, 0),
                ("scale", 2) => new Affine(n[0], 0, 0, n[1], 0, 0),
                ("rotate", 1) => Rotate(n[0]),
                ("rotate", 3) => new Affine(1, 0, 0, 1, n[1], n[2]).Then(Rotate(n[0])).Then(new Affine(1, 0, 0, 1, -n[1], -n[2])),
                ("skewX", 1) => new Affine(1, 0, Math.Tan(n[0] * Math.PI / 180), 1, 0, 0),
                ("skewY", 1) => new Affine(1, Math.Tan(n[0] * Math.PI / 180), 0, 1, 0, 0),
                _ => null,
            };
            if (step is { } s) result = result.Then(s);
        }
        return result;
    }

    private static Affine Rotate(double degrees)
    {
        double a = degrees * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        return new Affine(cos, sin, -sin, cos, 0, 0);
    }
}

/// <summary>The drawings PixelTuner 2 came with.</summary>
public static class CanvasSchemas
{
    /// <summary>File names (without the extension) and titles of the built-in drawings.</summary>
    public static IReadOnlyList<(string Name, string Title)> All { get; } = [("human", "Human"), ("human_1", "Human (simple)")];

    public static string Read(string name)
    {
        using var stream = typeof(CanvasSchemas).Assembly.GetManifestResourceStream($"PixelTuner.Core.Schemas.{name}.svg")
            ?? throw new ArgumentException($"There is no schema {name}.", nameof(name));
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
