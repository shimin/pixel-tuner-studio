using System.Threading.Channels;
using LibVLCSharp.Shared;

namespace PixelTuner.Audio.Vlc;

/// <summary>
/// Plays the soundtrack through LibVLC. LibVLC raises its events on its own threads and must never be called
/// from inside them (it deadlocks or crashes), so the handlers only note what happened and everything that
/// calls LibVLC runs elsewhere. Two locks keep that apart: <c>_native</c> serialises the calls into LibVLC
/// (and the disposal), and is never taken by a handler; <c>_sync</c> guards the fields, is held only briefly
/// and never across a call into LibVLC.
/// </summary>
public sealed class VlcAudioPlayer : IAudioPlayer
{
    private readonly VlcLibrary _library;
    private readonly Lock _sync = new();
    private readonly Lock _native = new();
    private MediaPlayer? _player;
    private Media? _media;
    private string? _tempFile;
    private TimeSpan _duration;
    private TimeSpan? _pendingSeek;
    private AudioPlayerState _state;
    private double _volume = 1;
    private bool _disposed;

    // Events for the app are raised from one worker, in order, and never on a LibVLC thread: a handler that
    // reads Position would otherwise wait on a lock held by a call that waits for that very thread.
    private readonly Channel<Action> _events = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _eventPump;

    public VlcAudioPlayer(VlcLibrary library)
    {
        _library = library;
        _eventPump = Task.Run(async () =>
        {
            await foreach (var raise in _events.Reader.ReadAllAsync())
            {
                try { raise(); }
                catch (Exception) { /* a handler's failure must not stop the others */ }
            }
        });
    }

    public TimeSpan Duration => _duration;
    public TimeSpan OutputLatency => TimeSpan.Zero;   // LibVLC does not report it; the sync offers a manual correction
    public AudioPlayerState State => _state;

    public event EventHandler<AudioPlayerState>? StateChanged;
    public event EventHandler? Ended;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            lock (_native)
            {
                if (_player is { } player) player.Volume = (int)Math.Round(_volume * 100);
            }
        }
    }

    public TimeSpan Position
    {
        get
        {
            lock (_native)
            {
                MediaPlayer? player;
                TimeSpan? pending;
                AudioPlayerState state;
                lock (_sync) { player = _player; pending = _pendingSeek; state = _state; }
                if (player is null) return TimeSpan.Zero;
                if (pending is { } seek && state != AudioPlayerState.Playing) return seek;
                long ms = player.Time;
                return ms < 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ms);
            }
        }
    }

    public async Task OpenAsync(Func<Stream> openStream, string fileName, CancellationToken cancel = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Release();

        // LibVLC parses (length, tracks) only files, not streams, and a package entry is not a file:
        // the sound is copied to a temporary one, which keeps its extension so the format is recognised.
        string extension = Path.GetExtension(fileName);
        string temp = Path.Combine(Path.GetTempPath(), $"pixeltuner-{Guid.NewGuid():N}{extension}");
        Media? media = null;
        try
        {
            await using (var source = openStream())
            await using (var target = File.Create(temp))
                await source.CopyToAsync(target, cancel);

            media = new Media(_library.Engine, temp, FromType.FromPath);
            var status = await media.Parse(MediaParseOptions.ParseLocal, timeout: 15000, cancellationToken: cancel);
            if (status != MediaParsedStatus.Done || media.Duration <= 0 || !HasAudio(media))
                throw new AudioFormatException($"“{fileName}” is not a sound that can be played.");
        }
        catch
        {
            // TODO(release): a stopgap, see docs/before-release.md. Not at once: LibVLC's parser thread is still
            // delivering the "parsed" event to this media for a moment after Parse returns, and releasing the
            // media under it crashes the process. A timer is a guess; find a real fix before shipping.
            if (media is not null) DisposeLater(media);
            DeleteQuietly(temp);
            throw;
        }

        var player = new MediaPlayer(media) { Volume = (int)Math.Round(_volume * 100) };
        player.Playing += (_, _) => OnPlaying(player);
        player.Paused += (_, _) => SetState(AudioPlayerState.Paused);
        player.Stopped += (_, _) => SetState(AudioPlayerState.Stopped);
        player.EndReached += (_, _) => ThreadPool.QueueUserWorkItem(_ => OnEnded());
        player.EncounteredError += (_, _) => ThreadPool.QueueUserWorkItem(_ => OnError());

        lock (_sync)
        {
            _tempFile = temp;
            _media = media;
            _player = player;
            _duration = TimeSpan.FromMilliseconds(media.Duration);
            _pendingSeek = null;
            _state = AudioPlayerState.Stopped;
        }
    }

    private static bool HasAudio(Media media) => media.Tracks.Any(t => t.TrackType == TrackType.Audio);

    public void Play()
    {
        MediaPlayer? player;
        lock (_sync) player = _player;
        if (player is null) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            lock (_native)
            {
                if (!IsCurrent(player)) return;
                // After the end LibVLC needs a stop before it plays from the start again.
                if (player.State == VLCState.Ended) player.Stop();
                player.Play();
            }
        });
    }

    public void Pause()
    {
        MediaPlayer? player;
        lock (_sync)
        {
            player = _player;
            if (_state != AudioPlayerState.Playing) return;
        }
        if (player is null) return;
        ThreadPool.QueueUserWorkItem(_ => { lock (_native) { if (IsCurrent(player)) player.SetPause(true); } });
    }

    public void Stop()
    {
        MediaPlayer? player;
        lock (_sync)
        {
            player = _player;
            _pendingSeek = null;
        }
        if (player is null) return;
        ThreadPool.QueueUserWorkItem(_ => { lock (_native) { if (IsCurrent(player)) player.Stop(); } });
    }

    public void Seek(TimeSpan position)
    {
        lock (_native)
        {
            MediaPlayer? player;
            TimeSpan clamped;
            bool stopped;
            lock (_sync)
            {
                player = _player;
                if (player is null) return;
                clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position > _duration ? _duration : position;
                stopped = _state == AudioPlayerState.Stopped;
                // LibVLC cannot seek before it plays; the position is applied when playing starts.
                if (stopped) _pendingSeek = clamped;
            }
            if (!stopped) player.Time = (long)clamped.TotalMilliseconds;
        }
    }

    private bool IsCurrent(MediaPlayer player)
    {
        lock (_sync) return ReferenceEquals(_player, player);
    }

    private void OnPlaying(MediaPlayer player)
    {
        TimeSpan? seek;
        lock (_sync)
        {
            seek = _pendingSeek;
            _pendingSeek = null;
        }
        SetState(AudioPlayerState.Playing);
        // Not here: this is LibVLC's thread, and a call back into it would deadlock.
        if (seek is { } target)
            ThreadPool.QueueUserWorkItem(_ => { lock (_native) { if (IsCurrent(player)) player.Time = (long)target.TotalMilliseconds; } });
    }

    private void OnEnded()
    {
        lock (_sync) _pendingSeek = null;
        SetState(AudioPlayerState.Stopped);
        _events.Writer.TryWrite(() => Ended?.Invoke(this, EventArgs.Empty));
    }

    private void OnError() => SetState(AudioPlayerState.Stopped);

    private void SetState(AudioPlayerState state)
    {
        lock (_sync)
        {
            if (_state == state) return;
            _state = state;
        }
        _events.Writer.TryWrite(() => StateChanged?.Invoke(this, state));
    }

    private void Release()
    {
        MediaPlayer? player;
        Media? media;
        string? temp;
        lock (_sync)
        {
            player = _player; media = _media; temp = _tempFile;
            _player = null; _media = null; _tempFile = null;
            _duration = TimeSpan.Zero;
            _pendingSeek = null;
            _state = AudioPlayerState.Stopped;
        }
        // Stopping and disposing from a LibVLC event thread would deadlock; this runs on the caller's thread,
        // and waits for any call into LibVLC that is still running.
        lock (_native)
        {
            player?.Stop();
            player?.Dispose();
            media?.Dispose();
        }
        if (temp is not null) DeleteQuietly(temp);
    }

    private static void DisposeLater(Media media) =>
        _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ => media.Dispose(), TaskScheduler.Default);

    private static void DeleteQuietly(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await Task.Run(Release);
        _events.Writer.TryComplete();
        await _eventPump;
    }
}
