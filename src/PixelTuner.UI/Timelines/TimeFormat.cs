using System.Globalization;

namespace PixelTuner.UI.Timelines;

/// <summary>Ruler steps and time labels.</summary>
public static class TimeFormat
{
    private static readonly double[] Steps = [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1800, 3600];

    /// <summary>The smallest nice step (seconds) that keeps labels at least <paramref name="minPixels"/> apart.</summary>
    public static double RulerStep(double pixelsPerSecond, double minPixels = 90)
    {
        foreach (double step in Steps)
            if (step * pixelsPerSecond >= minPixels) return step;
        return Steps[^1];
    }

    /// <summary>"1:05", or "1:05.5" / "1:05.25" when the step is below a second.</summary>
    public static string Format(TimeSpan time, double stepSeconds)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        int minutes = (int)time.TotalMinutes;
        double seconds = time.TotalSeconds - minutes * 60;
        string text = stepSeconds >= 1
            ? ((int)Math.Round(seconds, MidpointRounding.ToZero)).ToString("00", CultureInfo.InvariantCulture)
            : seconds.ToString(stepSeconds >= 0.1 ? "00.0" : "00.00", CultureInfo.InvariantCulture);
        return $"{minutes}:{text}";
    }

    /// <summary>Full precision for the inspector: "1:05.250".</summary>
    public static string Precise(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        int minutes = (int)time.TotalMinutes;
        return $"{minutes}:{(time.TotalSeconds - minutes * 60).ToString("00.000", CultureInfo.InvariantCulture)}";
    }
}
