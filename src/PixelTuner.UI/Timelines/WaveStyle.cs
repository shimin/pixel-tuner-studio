using Avalonia;
using Avalonia.Media;

namespace PixelTuner.UI.Timelines;

/// <summary>
/// How the sound lane draws the soundtrack: two outlines of the loudest sample of each channel, the left one rising above the centre line in blue and the
/// right one falling below it in red, each filled with a light gradient that is faint at the centre and stronger towards the peaks. Everything that can be
/// played with is here (colours for each theme, line width and opacity, the opacity of the gradient at the centre and at the peaks); the drawing is
/// <c>TimelineControl.DrawWave</c>. The design guides name this file.
/// </summary>
public static class WaveStyle
{
    // Colours of the two channels on the dark theme (light on dark) and on the light one (darker on white).
    public static readonly Color LeftDark = Color.FromRgb(0x4F, 0xB8, 0xF0);
    public static readonly Color RightDark = Color.FromRgb(0xEF, 0x5B, 0x57);
    public static readonly Color LeftLight = Color.FromRgb(0x1E, 0x88, 0xD8);
    public static readonly Color RightLight = Color.FromRgb(0xD9, 0x3F, 0x3A);

    /// <summary>Width of the outline in pixels.</summary>
    public const double LineWidth = 1.5;

    /// <summary>Opacity of the outline.</summary>
    public const double LineOpacity = 0.95;

    /// <summary>Opacity of the fill on the centre line, and where the peak of the channel could reach (a full-scale sample); the fill goes evenly from one to the other.</summary>
    public const double FillOpacityAtCentre = 0.04;
    public const double FillOpacityAtFullScale = 0.42;

    /// <summary>Space kept above and below the largest sample in the lane, in pixels.</summary>
    public const double Margin = 6;

    public static Color Left(bool dark) => dark ? LeftDark : LeftLight;

    public static Color Right(bool dark) => dark ? RightDark : RightLight;

    public static Pen Outline(Color colour) =>
        new(new SolidColorBrush(colour, LineOpacity), LineWidth, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round);

    /// <summary>
    /// The fill of one channel as a vertical gradient over the half of the lane it fills: <paramref name="up"/> for the half above the centre line (the
    /// brush runs from the bottom, the centre, to the top), else the half below. It is meant for the whole half, so a taller peak has a stronger fill
    /// whatever the outline around it.
    /// </summary>
    public static IBrush Fill(Color colour, bool up) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, up ? 1 : 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, up ? 0 : 1, RelativeUnit.Relative),
        GradientStops =
        [
            new GradientStop(Color.FromArgb((byte)(FillOpacityAtCentre * 255), colour.R, colour.G, colour.B), 0),
            new GradientStop(Color.FromArgb((byte)(FillOpacityAtFullScale * 255), colour.R, colour.G, colour.B), 1),
        ],
    };
}
