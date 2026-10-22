using System.Globalization;

namespace PixelTuner.Core.Layout;

/// <summary>
/// SVG path data read into polylines: curves and arcs are cut into short straight pieces. Enough to know where a prop's outline lies
/// (its bounds, whether a point is inside) without a drawing platform; the screen draws the path data itself.
/// </summary>
public static class SvgPath
{
    private const int CurveSteps = 24;

    /// <summary>The subpaths of path data, each a list of points; a closed one ends on its first point. A text that is not path data gives what was read before the fault.</summary>
    public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Flatten(string data)
    {
        var result = new List<IReadOnlyList<(double X, double Y)>>();
        var reader = new Reader(data);
        List<(double X, double Y)>? current = null;
        (double X, double Y) point = (0, 0), start = (0, 0), lastControl = (0, 0);
        char command = ' ', previous = ' ';

        void Begin((double X, double Y) at)
        {
            current = [at];
            result.Add(current);
            start = at;
        }

        while (true)
        {
            reader.SkipSeparators();
            if (reader.AtEnd) break;
            if (char.IsLetter(reader.Peek())) command = reader.Next();
            else if (command is ' ' or 'z' or 'Z') break;   // numbers with no command to take them
            else if (command == 'M') command = 'L';
            else if (command == 'm') command = 'l';

            bool relative = char.IsLower(command);
            char upper = char.ToUpperInvariant(command);
            double ox = relative ? point.X : 0, oy = relative ? point.Y : 0;
            switch (upper)
            {
                case 'M':
                {
                    if (!reader.TryNumbers(out var n, 2)) return result;
                    point = (ox + n[0], oy + n[1]);
                    Begin(point);
                    break;
                }
                case 'Z':
                    if (current is not null) { current.Add(start); point = start; }
                    current = null;
                    break;
                case 'L':
                case 'H':
                case 'V':
                {
                    if (!reader.TryNumbers(out var n, upper == 'L' ? 2 : 1)) return result;
                    point = upper switch
                    {
                        'L' => (ox + n[0], oy + n[1]),
                        'H' => (ox + n[0], point.Y),
                        _ => (point.X, oy + n[0]),
                    };
                    Add(ref current, ref start, result, point, point);
                    break;
                }
                case 'C':
                case 'S':
                {
                    if (!reader.TryNumbers(out var n, upper == 'C' ? 6 : 4)) return result;
                    (double X, double Y) c1, c2, end;
                    if (upper == 'C') { c1 = (ox + n[0], oy + n[1]); c2 = (ox + n[2], oy + n[3]); end = (ox + n[4], oy + n[5]); }
                    else
                    {
                        // The first control point mirrors the last one of a curve before it.
                        c1 = previous is 'C' or 'S' ? (2 * point.X - lastControl.X, 2 * point.Y - lastControl.Y) : point;
                        c2 = (ox + n[0], oy + n[1]);
                        end = (ox + n[2], oy + n[3]);
                    }
                    EnsureStarted(ref current, ref start, result, point);
                    for (int i = 1; i <= CurveSteps; i++)
                    {
                        double t = (double)i / CurveSteps, u = 1 - t;
                        current!.Add((u * u * u * point.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * end.X,
                                      u * u * u * point.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * end.Y));
                    }
                    lastControl = c2;
                    point = end;
                    break;
                }
                case 'Q':
                case 'T':
                {
                    if (!reader.TryNumbers(out var n, upper == 'Q' ? 4 : 2)) return result;
                    (double X, double Y) c, end;
                    if (upper == 'Q') { c = (ox + n[0], oy + n[1]); end = (ox + n[2], oy + n[3]); }
                    else
                    {
                        c = previous is 'Q' or 'T' ? (2 * point.X - lastControl.X, 2 * point.Y - lastControl.Y) : point;
                        end = (ox + n[0], oy + n[1]);
                    }
                    EnsureStarted(ref current, ref start, result, point);
                    for (int i = 1; i <= CurveSteps; i++)
                    {
                        double t = (double)i / CurveSteps, u = 1 - t;
                        current!.Add((u * u * point.X + 2 * u * t * c.X + t * t * end.X, u * u * point.Y + 2 * u * t * c.Y + t * t * end.Y));
                    }
                    lastControl = c;
                    point = end;
                    break;
                }
                case 'A':
                {
                    if (!reader.TryNumbers(out var n, 3) || !reader.TryFlag(out bool large) || !reader.TryFlag(out bool sweep) || !reader.TryNumbers(out var e, 2)) return result;
                    var end = (ox + e[0], oy + e[1]);
                    EnsureStarted(ref current, ref start, result, point);
                    foreach (var p in Arc(point, n[0], n[1], n[2], large, sweep, end)) current!.Add(p);
                    point = end;
                    break;
                }
                default:
                    return result;
            }
            previous = upper;
        }
        return result;
    }

    private static void Add(ref List<(double X, double Y)>? current, ref (double X, double Y) start, List<IReadOnlyList<(double X, double Y)>> result, (double X, double Y) to, (double X, double Y) from)
    {
        // A drawing command after a closed subpath starts a new one where the old one began.
        if (current is null)
        {
            current = [start];
            result.Add(current);
        }
        current.Add(to);
    }

    private static void EnsureStarted(ref List<(double X, double Y)>? current, ref (double X, double Y) start, List<IReadOnlyList<(double X, double Y)>> result, (double X, double Y) point)
    {
        if (current is not null) return;
        current = [point];
        result.Add(current);
        start = point;
    }

    // Endpoint to centre parameterisation of an elliptical arc (SVG implementation notes F.6.5), then points along it.
    private static IEnumerable<(double X, double Y)> Arc((double X, double Y) from, double rx, double ry, double degrees, bool large, bool sweep, (double X, double Y) to)
    {
        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (from == to) yield break;
        if (rx == 0 || ry == 0)
        {
            yield return to;
            yield break;
        }
        double phi = degrees * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
        double dx = (from.X - to.X) / 2, dy = (from.Y - to.Y) / 2;
        double x1 = cos * dx + sin * dy, y1 = -sin * dx + cos * dy;
        double scale = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
        if (scale > 1)
        {
            rx *= Math.Sqrt(scale);
            ry *= Math.Sqrt(scale);
        }
        double numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        double denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        double factor = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, numerator / denominator));
        double cx1 = factor * rx * y1 / ry, cy1 = -factor * ry * x1 / rx;
        double cx = cos * cx1 - sin * cy1 + (from.X + to.X) / 2, cy = sin * cx1 + cos * cy1 + (from.Y + to.Y) / 2;
        double start = Math.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx);
        double end = Math.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx);
        double delta = end - start;
        if (sweep && delta < 0) delta += 2 * Math.PI;
        else if (!sweep && delta > 0) delta -= 2 * Math.PI;

        int steps = Math.Max(CurveSteps, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2) * CurveSteps));
        for (int i = 1; i < steps; i++)
        {
            double a = start + delta * i / steps;
            yield return (cos * rx * Math.Cos(a) - sin * ry * Math.Sin(a) + cx, sin * rx * Math.Cos(a) + cos * ry * Math.Sin(a) + cy);
        }
        yield return to;
    }

    /// <summary>The smallest rectangle around the path once <paramref name="transform"/> has been applied; null for a path with no points.</summary>
    public static (double X, double Y, double Width, double Height)? Bounds(string data, Affine transform)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var subpath in Flatten(data))
            foreach (var (x, y) in subpath)
            {
                var (tx, ty) = transform.Apply(x, y);
                minX = Math.Min(minX, tx);
                minY = Math.Min(minY, ty);
                maxX = Math.Max(maxX, tx);
                maxY = Math.Max(maxY, ty);
            }
        return minX > maxX ? null : (minX, minY, maxX - minX, maxY - minY);
    }

    private ref struct Reader(string text)
    {
        private int _i;

        public bool AtEnd => _i >= text.Length;
        public char Peek() => text[_i];
        public char Next() => text[_i++];

        public void SkipSeparators()
        {
            while (_i < text.Length && (char.IsWhiteSpace(text[_i]) || text[_i] == ',')) _i++;
        }

        public bool TryNumbers(out double[] numbers, int count)
        {
            numbers = new double[count];
            for (int k = 0; k < count; k++)
                if (!TryNumber(out numbers[k])) return false;
            return true;
        }

        // A flag of an arc is one character, so "a1 1 0 00 1 1" is fine without spaces.
        public bool TryFlag(out bool flag)
        {
            SkipSeparators();
            flag = false;
            if (_i >= text.Length || text[_i] is not ('0' or '1')) return false;
            flag = text[_i++] == '1';
            return true;
        }

        public bool TryNumber(out double value)
        {
            SkipSeparators();
            value = 0;
            int begin = _i;
            if (_i < text.Length && text[_i] is '+' or '-') _i++;
            bool digits = false, dot = false;
            while (_i < text.Length)
            {
                char c = text[_i];
                if (char.IsAsciiDigit(c)) digits = true;
                else if (c == '.' && !dot) dot = true;
                else break;
                _i++;
            }
            if (digits && _i < text.Length && text[_i] is 'e' or 'E')
            {
                int exponent = _i + 1;
                if (exponent < text.Length && text[exponent] is '+' or '-') exponent++;
                if (exponent < text.Length && char.IsAsciiDigit(text[exponent]))
                {
                    _i = exponent;
                    while (_i < text.Length && char.IsAsciiDigit(text[_i])) _i++;
                }
            }
            return digits && double.TryParse(text.AsSpan(begin, _i - begin), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
