using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Audio;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;
using PixelTuner.UI.Timelines;

namespace PixelTuner.UI.ViewModels;

/// <summary>The editor of one open project: the timelines, their commands and the inspector.</summary>
public sealed partial class EditorViewModel : ObservableObject, IDisposable
{
    // Saving reports from a pool thread; bindings may only be touched on the UI thread.
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    private readonly IAudioEngine? _engine;
    private readonly Func<IFilePicker?>? _picker;
    private readonly IAudioPlayer? _player;
    private readonly IAudioDecoder? _decoder;
    private string? _loadedAudioId;
    private int _loadVersion;

    /// <param name="engine">Plays and reads sound; null on a computer without one (the editor works, silently).</param>
    /// <param name="picker">Asks the user for the soundtrack file.</param>
    public EditorViewModel(ProjectDocument document, IAudioEngine? engine = null, Func<IFilePicker?>? picker = null)
    {
        Document = document;
        _engine = engine;
        _picker = picker;
        Timelines = new TimelineEditor(document);
        if (engine is not null)
        {
            _player = engine.CreatePlayer();
            _decoder = engine.CreateDecoder();
            Timelines.Player = _player;
        }
        Timelines.Changed += (_, _) => OnUi(Refresh);
        Timelines.SelectionChanged += (_, _) => OnUi(Refresh);
        document.Changed += DocumentChanged;
        _ = LoadAudioAsync();
    }

    public ProjectDocument Document { get; }

    public TimelineEditor Timelines { get; }

    private void DocumentChanged(object? sender, EventArgs e) =>
        OnUi(() =>
        {
            if (Document.Project.AudioResourceId != _loadedAudioId) _ = LoadAudioAsync();
        });

    /// <summary>Opens the project's soundtrack in the player and reads its waveform.</summary>
    private async Task LoadAudioAsync()
    {
        string? id = Document.Project.AudioResourceId;
        int version = ++_loadVersion;
        _loadedAudioId = id;
        Timelines.Wave = null;
        Timelines.AudioDuration = TimeSpan.Zero;
        Message = null;
        _player?.Stop();
        Refresh();
        if (id is null) return;

        string name = Document.MediaName(id) ?? Path.GetFileName(Document.Project.AudioSourcePath ?? "") ?? "";
        if (!Document.HasMedia(id))
        {
            Message = string.Format(Strings.Audio_Missing, name);   // an imported project whose sound file was not found
            return;
        }
        if (_player is null || _decoder is null)
        {
            Message = Strings.Audio_NoEngine;
            return;
        }

        try
        {
            await _player.OpenAsync(() => Document.OpenMedia(id)!, name);
            if (version != _loadVersion) return;
            Timelines.AudioDuration = _player.Duration;
            await using var stream = Document.OpenMedia(id)!;
            var wave = await _decoder.ReadWaveformAsync(stream, name);
            if (version != _loadVersion) return;
            Timelines.Wave = wave;
        }
        catch (AudioFormatException ex)
        {
            if (version == _loadVersion) Message = string.Format(Strings.Audio_CannotPlay, ex.Message);
        }
        catch (ObjectDisposedException) when (version != _loadVersion)
        {
            return;   // the editor was closed while the sound was loading
        }
        Timelines.RaiseChanged();
        Refresh();
    }

    public void Dispose()
    {
        Document.Changed -= DocumentChanged;
        _loadVersion++;
        if (_player is not null) _ = _player.DisposeAsync().AsTask();
    }

    private void OnUi(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }

    private static readonly string[] InspectorProperties =
    [
        nameof(ShowBrightness), nameof(ShowStretching), nameof(StretchingLabel), nameof(ShowLaser), nameof(IsVideoSelected), nameof(IsLightItem), nameof(IsEffectSelected),
        nameof(OffsetX), nameof(OffsetY), nameof(T1), nameof(T2), nameof(ItemColor), nameof(ItemColorTo), nameof(ItemColorBrush), nameof(ItemColorToBrush),
        nameof(EffectIndex), nameof(HasImage), nameof(ItemPreviewBitmap), nameof(HasPreview), nameof(PlaybackText),
    ];

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(InspectorTitle));
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(SelectionName));
        OnPropertyChanged(nameof(StartText));
        OnPropertyChanged(nameof(LengthText));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(IsItemSelected));
        OnPropertyChanged(nameof(IsMarkerSelected));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Stretching));
        OnPropertyChanged(nameof(LaserOn));
        foreach (string name in InspectorProperties) OnPropertyChanged(name);
        OnPropertyChanged(nameof(HasSoundtrack));
        OnPropertyChanged(nameof(SoundtrackName));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(HasMessage));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(CanPlay));
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        PlayPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RemoveSoundtrackCommand.NotifyCanExecuteChanged();
    }

    // ---- commands

    [RelayCommand] void AddPixelTimeline() => Add(TimelineKind.Pixel, "#FF2D7DD2");
    [RelayCommand] void AddVideoTimeline() => Add(TimelineKind.Video, "#FF2FA84F");
    [RelayCommand] void AddEpicTimeline() => Add(TimelineKind.EpicPoi, "#FFE07A1F");
    [RelayCommand] void AddLightTimeline() => Add(TimelineKind.Light, "#FFC9A227");

    private void Add(string kind, string color) =>
        Timelines.AddTimeline(string.Format(Strings.Timeline_DefaultName, Timelines.Project.Timelines.Count + 1), kind, color);

    [RelayCommand]
    void AddMarker() =>
        Timelines.AddMarkerAtPlayhead(string.Format(Strings.Marker_DefaultName, Timelines.Project.Markers.Count + 1));

    /// <summary>A new item is this long, when there is room for it.</summary>
    public static readonly TimeSpan DefaultItemLength = TimeSpan.FromSeconds(5);

    // The timeline a new item goes to: the selected one, or the one the selected item is on.
    private Timeline? Target => Timelines.TimelineOf(Timelines.Selected);

    /// <summary>Adds a picture or animation to the selected pixel, video or E-Pic timeline.</summary>
    [RelayCommand]
    async Task AddItemAsync()
    {
        if (Target is not { Kind: not TimelineKind.Light } timeline || _picker?.Invoke() is not { } picker)
        {
            if (Target is not { Kind: not TimelineKind.Light }) Message = Strings.Item_NoTimeline;
            return;
        }
        string? path = timeline.Kind == TimelineKind.Video ? await picker.PickGifPathAsync() : await picker.PickImagePathAsync();
        if (path is null) return;
        try
        {
            string kind = timeline.Kind switch { TimelineKind.Video => ItemKind.Video, TimelineKind.EpicPoi => ItemKind.Gradient, _ => ItemKind.Image };
            // A file that is not a picture is refused before it enters the project.
            if (kind == ItemKind.Video)
            {
                if (GifFrames.DecodeFile(path).Count == 0) throw new InvalidDataException(string.Format(Strings.Item_BadMedia, Path.GetFileName(path)));
            }
            else
            {
                ImageDecoder.DecodeFile(path);
            }
            string id = Document.AddMedia(path);
            // PixelTuner 2's defaults: 4 intervals of 50 us for a picture, 40 ms (25 frames a second) for a video.
            var item = new TimelineItem { Kind = kind, ResourceId = id, SourcePath = path, Stretching = kind == ItemKind.Video ? 40 : 4 };
            Message = Timelines.AddItem(timeline, item, DefaultItemLength) is null ? Strings.Item_NoRoom : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Message = string.Format(Strings.Item_AddFailed, Path.GetFileName(path), ex.Message);
        }
    }

    [RelayCommand]
    void AddColor() => AddLight(new TimelineItem { Kind = ItemKind.Solid, Color = "#FFFFFF" });

    [RelayCommand]
    void AddEffect() =>
        AddLight(new TimelineItem { Kind = ItemKind.Effect, Color = "#000000", ColorLast = "#FFFFFF", Effect = LightEffectKind.LinearTransition, T1 = 1000, T2 = 1000 });

    private void AddLight(TimelineItem item)
    {
        if (Target is not { Kind: TimelineKind.Light } timeline) Message = Strings.Item_NoTimeline;
        else Message = Timelines.AddItem(timeline, item, DefaultItemLength) is null ? Strings.Item_NoRoom : null;
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    void DeleteSelected() => Timelines.DeleteSelected();

    private bool CanDelete => Timelines.CanDelete;

    // The view is the centre of the time area when a command zooms.
    private double Centre => TimelineEditor.HeaderWidth + Timelines.TimeAreaWidth / 2;

    [RelayCommand] void ZoomIn() => Timelines.ZoomAt(1.5, Centre);
    [RelayCommand] void ZoomOut() => Timelines.ZoomAt(1 / 1.5, Centre);
    [RelayCommand] void ZoomFit() => Timelines.ZoomToFit();

    // ---- soundtrack and playback

    private string? _message;

    /// <summary>What the user should know about the soundtrack (missing, cannot be played); null when all is well.</summary>
    public string? Message
    {
        get => _message;
        private set
        {
            _message = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => _message is not null;

    public bool HasSoundtrack => Document.Project.AudioResourceId is not null;

    /// <summary>"song.mp3": the file the user chose.</summary>
    public string SoundtrackName =>
        Document.MediaName(Document.Project.AudioResourceId) ?? Path.GetFileName(Document.Project.AudioSourcePath ?? "") ?? "";

    /// <summary>Whether play does anything: a soundtrack is loaded and there is a player for it.</summary>
    public bool CanPlay => Timelines.Wave is not null && _player is not null;

    public string PlayPauseGlyph => Timelines.IsPlaying ? "⏸" : "▶";

    /// <summary>"0:12.345 / 3:45.000"</summary>
    public string TimeText => $"{TimeFormat.Precise(Timelines.Playhead)} / {TimeFormat.Precise(Timelines.AudioDuration)}";

    [RelayCommand(CanExecute = nameof(CanPlay))]
    void PlayPause() => Timelines.PlayPause();

    [RelayCommand(CanExecute = nameof(CanPlay))]
    void Stop() => Timelines.Stop();

    [RelayCommand]
    void ToStart()
    {
        Timelines.SetPlayhead(TimeSpan.Zero);
        Timelines.ScrollTo(TimeSpan.Zero);
    }

    [RelayCommand]
    async Task SetSoundtrackAsync()
    {
        if (_picker?.Invoke() is not { } picker) return;
        string? path = await picker.PickAudioPathAsync();
        if (path is null) return;
        try
        {
            Document.SetAudio(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = string.Format(Strings.Audio_AddFailed, ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSoundtrack))]
    void RemoveSoundtrack() => Document.RemoveAudio();

    // ---- inspector

    public bool HasSelection => Timelines.Selected is not null;
    public bool IsItemSelected => Timelines.Selected is TimelineItem;
    public bool IsMarkerSelected => Timelines.Selected is Marker;
    public bool CanRename => Timelines.Selected is Timeline or Marker;

    public string InspectorTitle => Timelines.Selected switch
    {
        Timeline => Strings.Inspector_Timeline,
        TimelineItem => Strings.Inspector_Item,
        Marker => Strings.Inspector_Marker,
        _ => "",
    };

    public string SelectionName
    {
        get => Timelines.Selected switch { Timeline t => t.Name, Marker m => m.Name, _ => "" };
        set
        {
            switch (Timelines.Selected)
            {
                case Timeline t when t.Name != value:
                    Document.Execute(Edits.RenameTimeline(t, value));
                    break;
                case Marker m when m.Name != value:
                    Document.Execute(Edits.RenameMarker(m, value));
                    break;
            }
        }
    }

    private TimelineItem? Item => Timelines.Selected as TimelineItem;

    public bool ShowBrightness => Item is { Kind: ItemKind.Image or ItemKind.Video or ItemKind.Gradient };
    public bool ShowStretching => Item is { Kind: ItemKind.Image or ItemKind.Gradient or ItemKind.Video };

    /// <summary>Video devices take milliseconds per frame; the others take the speed of a column.</summary>
    public string StretchingLabel => Item is { Kind: ItemKind.Video } ? Strings.Inspector_FrameTime : Strings.Inspector_Stretching;
    public bool ShowLaser => Item is { Kind: ItemKind.Image };
    public bool IsVideoSelected => Item is { Kind: ItemKind.Video };
    public bool IsLightItem => Item is { Kind: ItemKind.Solid or ItemKind.Effect };
    public bool IsEffectSelected => Item is { Kind: ItemKind.Effect };

    public double Brightness
    {
        get => Item?.Brightness ?? 0;
        set
        {
            if (Item is { } i && (byte)Math.Clamp(Math.Round(value), 0, 100) is var percent && percent != i.Brightness)
                Document.Execute(Edits.SetBrightness(i, percent));
        }
    }

    public decimal? Stretching
    {
        get => Item?.Stretching;
        set
        {
            if (Item is { } i && value is { } v && (int)v >= 1 && (int)v != i.Stretching)
                Document.Execute(Edits.SetStretching(i, (int)v));
        }
    }

    public bool LaserOn
    {
        get => Item is { LaserOn: true };
        set
        {
            if (Item is { } i && i.LaserOn != value) Document.Execute(Edits.SetLaser(i, value));
        }
    }

    public decimal? OffsetX
    {
        get => Item?.OffsetX;
        set
        {
            if (Item is { } i && value is { } v && (int)v != i.OffsetX) Document.Execute(Edits.SetOffset(i, (int)v, i.OffsetY));
        }
    }

    public decimal? OffsetY
    {
        get => Item?.OffsetY;
        set
        {
            if (Item is { } i && value is { } v && (int)v != i.OffsetY) Document.Execute(Edits.SetOffset(i, i.OffsetX, (int)v));
        }
    }

    public decimal? T1
    {
        get => Item?.T1;
        set
        {
            if (Item is { } i && value is { } v && v >= 0 && (int)v != i.T1) Document.Execute(Edits.SetEffectTimes(i, (int)v, i.T2));
        }
    }

    public decimal? T2
    {
        get => Item?.T2;
        set
        {
            if (Item is { } i && value is { } v && v >= 0 && (int)v != i.T2) Document.Execute(Edits.SetEffectTimes(i, i.T1, (int)v));
        }
    }

    /// <summary>Colours are typed as #RRGGBB; a text that is not a colour is refused and the old colour comes back.</summary>
    public string ItemColor
    {
        get => Item?.Color ?? "";
        set => TrySet(value, i => i.Color, Edits.SetItemColor);
    }

    public string ItemColorTo
    {
        get => Item?.ColorLast ?? "";
        set => TrySet(value, i => i.ColorLast, Edits.SetItemColorLast);
    }

    private void TrySet(string value, Func<TimelineItem, string?> current, Func<TimelineItem, string, Edit> edit)
    {
        if (Item is not { } i || value == current(i)) return;
        try
        {
            Document.Execute(edit(i, value));
        }
        catch (InvalidDataException)
        {
            Message = string.Format(Strings.Inspector_BadValue, value);
            Refresh();
        }
    }

    public IBrush ItemColorBrush => SwatchBrush(Item?.Color);
    public IBrush ItemColorToBrush => SwatchBrush(Item?.ColorLast);

    private static IBrush SwatchBrush(string? color)
    {
        try
        {
            var c = ColorText.ParseRgb(color);
            return new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
        }
        catch (InvalidDataException)
        {
            return Brushes.Transparent;
        }
    }

    public IReadOnlyList<string> EffectNames { get; } =
        [Strings.Effect_LinearTransition, Strings.Effect_SineWave, Strings.Effect_SquareWave, Strings.Effect_SawToothWave];

    private static readonly string[] EffectKinds =
        [LightEffectKind.LinearTransition, LightEffectKind.SineWave, LightEffectKind.SquareWave, LightEffectKind.SawToothWave];

    public int EffectIndex
    {
        get => Item?.Effect is { } e ? Array.IndexOf(EffectKinds, e) : -1;
        set
        {
            if (Item is { } i && value >= 0 && value < EffectKinds.Length && EffectKinds[value] != i.Effect)
                Document.Execute(Edits.SetEffect(i, EffectKinds[value]));
        }
    }

    public bool HasImage => Item is { Kind: ItemKind.Image };

    /// <summary>
    /// How long one pass of the picture (pixel items) or of the animation (video items) takes at this stretching, and
    /// how often it plays in the item. E-Pic poi use PixelTuner 2's formula from its Repeat window.
    /// </summary>
    public string PlaybackText
    {
        get
        {
            if (Item is not { Kind: ItemKind.Image or ItemKind.Video or ItemKind.Gradient } i || ItemPreviewBitmap is null) return "";
            var pass = i.Kind switch
            {
                ItemKind.Video => ItemPreview.VideoPassDuration(_previewColumns, i.Stretching),
                ItemKind.Gradient => ItemPreview.EpicPassDuration(_previewColumns, i.Stretching),
                _ => ItemPreview.PassDuration(_previewColumns, i.Stretching),
            };
            double repeats = ItemPreview.Repeats(i.Duration, pass);
            return string.Format(i.Kind == ItemKind.Video ? Strings.Inspector_PlaybackVideo : Strings.Inspector_Playback,
                $"{pass.TotalMilliseconds:0.##} ms", repeats.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture));
        }
    }

    // Columns of the device image: the width of a picture, the number of frames of a video.
    private int _previewColumns;

    public bool HasPreview => ItemPreviewBitmap is not null;

    private (string? Id, string Kind, byte Brightness, bool Flip) _previewKey;
    private Bitmap? _preview;

    /// <summary>The selected picture as the device would show it (brightness, flip); stretching repeats columns in time, so it is not drawn. Null when there is none or the file is missing.</summary>
    public Bitmap? ItemPreviewBitmap
    {
        get
        {
            if (Item is not { Kind: ItemKind.Image or ItemKind.Video or ItemKind.Gradient } i || Timelines.TimelineOf(i) is not { } timeline)
            {
                _previewKey = default;
                return _preview = null;
            }
            (string? Id, string Kind, byte Brightness, bool Flip) key = (i.ResourceId, i.Kind, i.Brightness, i.Kind != ItemKind.Gradient && timeline.FlipVertical);
            if (key == _previewKey) return _preview;
            _previewKey = key;
            _preview = null;
            if (i.ResourceId is null || Document.OpenMedia(i.ResourceId) is not { } stream) return null;
            try
            {
                using (stream)
                {
                    // A video shows its first frame; the animation itself is the device's business.
                    PixelImage source;
                    if (i.Kind == ItemKind.Video)
                    {
                        var frames = GifFrames.Decode(((MemoryStream)stream).ToArray());
                        source = frames[0];
                        _previewColumns = frames.Count;
                    }
                    else
                    {
                        source = ImageDecoder.Decode(stream);
                        _previewColumns = source.Width;
                    }
                    _preview = PixelBitmap.From(ItemPreview.Render(source, key.Brightness, 1, key.Flip));
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                // damaged media: no preview
            }
            return _preview;
        }
    }

    public string StartText
    {
        get => Item is { } i ? TimeFormat.Precise(i.Start) : "";
        set => SetTime(value, (timeline, i, time) => Edits.MoveItem(timeline, i, time), Item?.Start);
    }

    public string LengthText
    {
        get => Item is { } i ? TimeFormat.Precise(i.Duration) : "";
        set => SetTime(value, (timeline, i, time) => Edits.ResizeItem(timeline, i, time), Item?.Duration);
    }

    public string PositionText
    {
        get => Timelines.Selected is Marker m ? TimeFormat.Precise(m.Position) : "";
        set
        {
            if (Timelines.Selected is not Marker m) return;
            if (TimeFormat.TryParse(value, out var time) && time != m.Position) Document.Execute(Edits.MoveMarker(Document.Project, m, time));
            else if (time == m.Position) return;
            else Reject(value);
        }
    }

    // Edits refuse times that overlap a neighbour; the user is told and the old time comes back.
    private void SetTime(string text, Func<Timeline, TimelineItem, TimeSpan, Edit> edit, TimeSpan? current)
    {
        if (Item is not { } i || Timelines.TimelineOf(i) is not { } timeline) return;
        if (!TimeFormat.TryParse(text, out var time)) { Reject(text); return; }
        if (time == current) return;
        try
        {
            Document.Execute(edit(timeline, i, time));
        }
        catch (InvalidOperationException)
        {
            Reject(text);
        }
    }

    private void Reject(string text)
    {
        Message = string.Format(Strings.Inspector_BadValue, text);
        Refresh();
    }
}
