namespace PixelTuner.Audio;

/// <summary>
/// The loudness outline of a soundtrack: per point, the lowest and highest sample (-1 to 1)
/// over 1 / <see cref="PointsPerSecond"/> of a second, all channels mixed. Small enough to
/// keep in the package cache (cache/waveform/&lt;id&gt;.bin) and to draw at any zoom.
/// </summary>
public sealed class Waveform
{
    /// <summary>PixelTuner 2 zoomed to about 100 pixels per second; this leaves room to zoom in.</summary>
    public const int DefaultPointsPerSecond = 200;

    public Waveform(int pointsPerSecond, TimeSpan duration, float[] minimum, float[] maximum)
    {
        if (minimum.Length != maximum.Length) throw new ArgumentException("Minimum and maximum differ in length.");
        PointsPerSecond = pointsPerSecond;
        Duration = duration;
        Minimum = minimum;
        Maximum = maximum;
    }

    public int PointsPerSecond { get; }
    public TimeSpan Duration { get; }
    public float[] Minimum { get; }
    public float[] Maximum { get; }
    public int Count => Minimum.Length;

    /// <summary>
    /// Lowest and highest sample between two times, for drawing one pixel column at any zoom.
    /// (0, 0) outside the soundtrack.
    /// </summary>
    public (float Min, float Max) Range(TimeSpan from, TimeSpan to)
    {
        int first = Math.Max(0, (int)Math.Floor(from.TotalSeconds * PointsPerSecond));
        int last = Math.Min(Count, Math.Max(first + 1, (int)Math.Ceiling(to.TotalSeconds * PointsPerSecond)));
        if (first >= last) return (0, 0);
        float min = float.MaxValue, max = float.MinValue;
        for (int i = first; i < last; i++)
        {
            min = Math.Min(min, Minimum[i]);
            max = Math.Max(max, Maximum[i]);
        }
        return (min, max);
    }
}

/// <summary>
/// Builds a <see cref="Waveform"/> from decoded samples as they come, so a decoder never
/// holds the whole track. Each platform's decoder feeds it.
/// </summary>
public sealed class WaveformBuilder
{
    private readonly int _channels;
    private readonly int _framesPerPoint;
    private readonly int _pointsPerSecond;
    private readonly int _sampleRate;
    private readonly List<float> _min = [];
    private readonly List<float> _max = [];
    private float _pointMin = float.MaxValue, _pointMax = float.MinValue;
    private int _framesInPoint;
    private int _channelInFrame;
    private float _frameSum;
    private long _frames;

    public WaveformBuilder(int sampleRate, int channels, int pointsPerSecond = Waveform.DefaultPointsPerSecond)
    {
        if (sampleRate <= 0 || channels <= 0 || pointsPerSecond <= 0 || pointsPerSecond > sampleRate)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate, channels and points per second must be positive, at most one point per sample.");
        _sampleRate = sampleRate;
        _channels = channels;
        _pointsPerSecond = pointsPerSecond;
        _framesPerPoint = sampleRate / pointsPerSecond;
    }

    /// <summary>Interleaved samples, -1 to 1. A frame may be split across calls.</summary>
    public void Add(ReadOnlySpan<float> samples)
    {
        foreach (float sample in samples)
        {
            _frameSum += sample;
            if (++_channelInFrame < _channels) continue;
            float mixed = _frameSum / _channels;
            _channelInFrame = 0;
            _frameSum = 0;
            _frames++;
            _pointMin = Math.Min(_pointMin, mixed);
            _pointMax = Math.Max(_pointMax, mixed);
            if (++_framesInPoint == _framesPerPoint) Flush();
        }
    }

    /// <summary>16-bit PCM, as WAV files and most decoders deliver it.</summary>
    public void Add(ReadOnlySpan<short> samples)
    {
        Span<float> chunk = stackalloc float[256];
        while (!samples.IsEmpty)
        {
            int n = Math.Min(chunk.Length, samples.Length);
            for (int i = 0; i < n; i++) chunk[i] = samples[i] / 32768f;
            Add(chunk[..n]);
            samples = samples[n..];
        }
    }

    public Waveform Build()
    {
        if (_framesInPoint > 0) Flush();
        return new Waveform(_pointsPerSecond, TimeSpan.FromSeconds((double)_frames / _sampleRate), [.. _min], [.. _max]);
    }

    private void Flush()
    {
        _min.Add(_pointMin);
        _max.Add(_pointMax);
        _pointMin = float.MaxValue;
        _pointMax = float.MinValue;
        _framesInPoint = 0;
    }
}
