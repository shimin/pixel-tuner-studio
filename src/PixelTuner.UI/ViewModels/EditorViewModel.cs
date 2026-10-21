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
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(ImagePreview));
        OnPropertyChanged(nameof(HasPreview));
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

    /// <summary>A new image is this long, when there is room for it.</summary>
    public static readonly TimeSpan DefaultImageLength = TimeSpan.FromSeconds(5);

    // The timeline the image goes to: the selected one, or the one the selected item is on.
    private Timeline? ImageTarget => Timelines.TimelineOf(Timelines.Selected) is { Kind: TimelineKind.Pixel } t ? t : null;

    [RelayCommand]
    async Task AddImageAsync()
    {
        if (ImageTarget is not { } timeline)
        {
            Message = Strings.Image_NoTimeline;
            return;
        }
        if (_picker?.Invoke() is not { } picker) return;
        string? path = await picker.PickImagePathAsync();
        if (path is null) return;
        try
        {
            ImageDecoder.DecodeFile(path);   // a file that is not an image is refused before it enters the project
            string id = Document.AddMedia(path);
            Message = Timelines.AddImage(timeline, id, path, DefaultImageLength) is null ? Strings.Image_NoRoom : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Message = string.Format(Strings.Image_AddFailed, Path.GetFileName(path), ex.Message);
        }
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

    public double Brightness
    {
        get => Timelines.Selected is TimelineItem i ? i.Brightness : 0;
        set
        {
            if (Timelines.Selected is TimelineItem i && (byte)Math.Clamp(Math.Round(value), 0, 100) is var percent && percent != i.Brightness)
                Document.Execute(Edits.SetBrightness(i, percent));
        }
    }

    public decimal? Stretching
    {
        get => Timelines.Selected is TimelineItem i ? i.Stretching : null;
        set
        {
            if (Timelines.Selected is TimelineItem i && value is { } v && (int)v >= 1 && (int)v != i.Stretching)
                Document.Execute(Edits.SetStretching(i, (int)v));
        }
    }

    public bool LaserOn
    {
        get => Timelines.Selected is TimelineItem { LaserOn: true };
        set
        {
            if (Timelines.Selected is TimelineItem i && i.LaserOn != value) Document.Execute(Edits.SetLaser(i, value));
        }
    }

    public bool HasImage => Timelines.Selected is TimelineItem { Kind: ItemKind.Image };

    public bool HasPreview => ImagePreview is not null;

    private string? _previewId;
    private Bitmap? _preview;

    /// <summary>The selected image as the device sees its pixels; null when nothing is selected or the file is missing.</summary>
    public Bitmap? ImagePreview
    {
        get
        {
            string? id = Timelines.Selected is TimelineItem { Kind: ItemKind.Image } i ? i.ResourceId : null;
            if (id == _previewId) return _preview;
            _previewId = id;
            _preview = null;
            if (id is null || Document.OpenMedia(id) is not { } stream) return null;
            try
            {
                using (stream) _preview = PixelBitmap.From(ImageDecoder.Decode(stream));
            }
            catch (InvalidDataException)
            {
                // damaged media: no preview
            }
            return _preview;
        }
    }

    public string StartText => Timelines.Selected is TimelineItem i ? TimeFormat.Precise(i.Start) : "";
    public string LengthText => Timelines.Selected is TimelineItem i ? TimeFormat.Precise(i.Duration) : "";
    public string PositionText => Timelines.Selected is Marker m ? TimeFormat.Precise(m.Position) : "";
}
