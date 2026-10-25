using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using PixelTuner.Devices;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.DevicesPanel;

/// <summary>A timeline the selected device may be given, with its place in the file.</summary>
public sealed class UploadChoiceViewModel(UploadPlanViewModel plan, Timeline timeline)
{
    public Timeline Timeline { get; } = timeline;

    public string Name => string.IsNullOrEmpty(Timeline.Name) ? Strings.Devices_Unnamed : Timeline.Name;

    /// <summary>The timeline goes into the file.</summary>
    public bool IsChecked
    {
        get => plan.Plays(Timeline);
        set => plan.SetPlayed(Timeline, value);
    }

    /// <summary>A poi or a hoop plays pictures as high as it has LEDs; a timeline of another height cannot be ticked.</summary>
    public bool Fits => plan.Fits(Timeline);

    public bool CanCheck => Fits || IsChecked;

    /// <summary>1-based place in the file; empty while it is not in it.</summary>
    public string Order => plan.OrderOf(Timeline) is var index and >= 0 ? (index + 1).ToString() : "";

    public string Detail
    {
        get
        {
            string items = string.Format(Strings.Devices_ItemCount, Timeline.Items.Count);
            return Timeline.Kind == TimelineKind.Pixel && Edits.PixelHeight(Timeline) is > 0 and var height
                ? string.Format(Strings.Devices_TimelineHeight, items, height) + (Fits ? "" : " · " + string.Format(Strings.Devices_DoesNotFit, height, plan.LedCount))
                : items;
        }
    }
}

/// <summary>
/// What is sent to one connected device, as the user sets it up before the upload: which timelines go into the file and in what order, and (for a video
/// module) whether the file is a video's or a fan's. The file is built from it by <see cref="ShowBuilder"/> for the device as it reports itself (the LEDs
/// and the memory it has), and what the build says is shown here; nothing is sent until the Upload button of the panel is pressed. The choice is not part of
/// the project: the project holds the timelines, the devices are the ones that are connected.
/// </summary>
public sealed partial class UploadPlanViewModel : ObservableObject
{
    private readonly DevicesPanelViewModel _panel;
    private readonly List<Guid> _ids = [];
    private BuildResult? _result;
    private int _kindIndex;

    public UploadPlanViewModel(DevicesPanelViewModel panel, ConnectedDeviceViewModel connected)
    {
        _panel = panel;
        Connected = connected;
        Kinds = DeviceMapping.KindOf(connected.Family) is { } kind
            ? (kind == DeviceKind.Video ? [DeviceKind.Video, DeviceKind.Fan] : [kind])
            : [];
        _ids.AddRange(DefaultSelection());
        Choices = MakeChoices();
    }

    /// <summary>What is ticked to begin with: everything of the kind that has something on it and fits the device, as many as the kind takes.</summary>
    private IEnumerable<Guid> DefaultSelection() =>
        Kinds.Count == 0 ? [] : _panel.Project.Timelines
            .Where(t => t.Kind == Edits.TimelineKindOf(Kind) && t.Items.Count > 0 && Fits(t))
            .Take(Edits.MaxTimelinesOf(Kind)).Select(t => t.Id);

    public ConnectedDeviceViewModel Connected { get; }

    /// <summary>The kinds of file this device takes: one, but a video module takes a video's and a fan's.</summary>
    public IReadOnlyList<string> Kinds { get; }

    public IReadOnlyList<string> KindTitles => [.. Kinds.Select(DevicesPanelViewModel.KindTitle)];

    public bool HasKindChoice => Kinds.Count > 1;

    public int KindIndex
    {
        get => _kindIndex;
        set
        {
            if (value < 0 || value >= Kinds.Count || value == _kindIndex) return;
            _kindIndex = value;
            string timelineKind = Edits.TimelineKindOf(Kind);
            // The timelines that the new kind plays stay (as far as it takes them); the others are dropped.
            var kept = _ids.Where(id => _panel.Project.Timelines.FirstOrDefault(t => t.Id == id)?.Kind == timelineKind).Take(Edits.MaxTimelinesOf(Kind)).ToList();
            _ids.Clear();
            _ids.AddRange(kept.Count > 0 ? kept : DefaultSelection());
            Choices = MakeChoices();
            Changed();
        }
    }

    public string Kind => Kinds.Count == 0 ? DeviceKind.Poi : Kinds[_kindIndex];

    public string Heading => string.Format(Strings.Devices_UploadHeading, Connected.Title);

    /// <summary>LEDs of the device that a poi's and a hoop's pictures must be as high as; 0 when it does not say or for the other kinds.</summary>
    public int LedCount => Kind is DeviceKind.Poi or DeviceKind.Hoop ? Connected.Device.Info.LedCount ?? 0 : 0;

    public IReadOnlyList<UploadChoiceViewModel> Choices { get; private set; }

    private IReadOnlyList<UploadChoiceViewModel> MakeChoices() =>
        Kinds.Count == 0 ? [] : [.. _panel.Project.Timelines.Where(t => t.Kind == Edits.TimelineKindOf(Kind)).Select(t => new UploadChoiceViewModel(this, t))];

    internal bool Fits(Timeline timeline) => LedCount <= 0 || Edits.FitsHeight(timeline, LedCount);

    internal bool Plays(Timeline timeline) => _ids.Contains(timeline.Id);

    internal int OrderOf(Timeline timeline) => _ids.IndexOf(timeline.Id);

    internal void SetPlayed(Timeline timeline, bool played)
    {
        if (played == _ids.Contains(timeline.Id)) return;
        if (played)
        {
            int most = Edits.MaxTimelinesOf(Kind);
            if (most == 1) _ids.Clear();   // a fan plays one: the ticked one takes its place
            else if (_ids.Count >= most)
            {
                _panel.Message = string.Format(Strings.Devices_TooMany, KindTitles[_kindIndex], most);
                Changed();   // the box shows what is true again
                return;
            }
            _ids.Add(timeline.Id);
        }
        else _ids.Remove(timeline.Id);
        Changed();
    }

    internal void Move(UploadChoiceViewModel choice, int direction)
    {
        int at = _ids.IndexOf(choice.Timeline.Id), to = at + direction;
        if (at < 0 || to < 0 || to >= _ids.Count) return;
        (_ids[at], _ids[to]) = (_ids[to], _ids[at]);
        Changed();
    }

    [RelayCommand]
    private void MoveUp(UploadChoiceViewModel? choice)
    {
        if (choice is not null) Move(choice, -1);
    }

    [RelayCommand]
    private void MoveDown(UploadChoiceViewModel? choice)
    {
        if (choice is not null) Move(choice, 1);
    }

    /// <summary>What the file is built for: the device as it reports itself now, and the timelines chosen.</summary>
    internal DeviceBinding Binding => new()
    {
        DeviceType = Kind,
        TimelineIds = [.. _ids],
        LedCount = LedCount,
        MemoryBytes = DeviceMapping.MemoryBytes(Connected.Device.Model, Connected.Device.Info),
    };

    /// <summary>The file built for this device and choice (kept until either changes); the issues are in it.</summary>
    public BuildResult Result => _result ??= Build();

    internal BuildResult Build()
    {
        try
        {
            return new ShowBuilder(_panel.Project, _panel.Media).Build(Binding);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            return new BuildResult(null, [new BuildIssue(BuildSeverity.Error, BuildIssueCode.MissingMedia)]);
        }
    }

    public bool CanBuild => Result.Succeeded;

    /// <summary>One line: the size of the file against the memory of the device, or what stops the build.</summary>
    public string Summary
    {
        get
        {
            var result = Result;
            long memory = Binding.MemoryBytes;
            if (result.Bin is { } bin)
            {
                string size = DevicesPanelViewModel.FormatSize(bin.Length);
                string text = memory > 0 ? string.Format(Strings.Devices_FileInfoOf, size, DevicesPanelViewModel.FormatSize(memory)) : string.Format(Strings.Devices_FileInfo, size);
                int warnings = result.Warnings.Count();
                return warnings == 0 ? text : text + " · " + string.Format(Strings.Devices_Warnings, warnings);
            }
            var error = result.Errors.FirstOrDefault();
            return error is null ? Strings.Devices_NotBuilt : EditorViewModel.IssueText(error);
        }
    }

    /// <summary>What the build says: the errors first, then the warnings, in words.</summary>
    public IReadOnlyList<string> IssueLines =>
        [.. Result.Issues.OrderByDescending(i => i.Severity).Select(EditorViewModel.IssueText).Distinct()];

    private void Changed()
    {
        _result = null;
        OnPropertyChanged(string.Empty);   // every property
        _panel.PlanChanged();
    }

    /// <summary>The project changed (an edit, undo or redo): the timelines are listed again and the ones that are gone are dropped.</summary>
    public void Refresh()
    {
        _ids.RemoveAll(id => _panel.Project.Timelines.FirstOrDefault(t => t.Id == id) is not { } t || Kinds.Count == 0 || t.Kind != Edits.TimelineKindOf(Kind));
        _result = null;
        Choices = MakeChoices();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>The device reported something new (its LEDs): the file is built again when it is next asked for.</summary>
    public void DeviceChanged()
    {
        _result = null;
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IssueLines));
    }
}

/// <summary>
/// A device that answered on a cable or over Wi-Fi. It is one object for as long as the device is connected: the state the device reports every second
/// (battery, busy) is read again into the same object, so the list does not rebuild its line and the selection and focus stay on it.
/// </summary>
public sealed class ConnectedDeviceViewModel : ObservableObject
{
    private string _shown;

    public ConnectedDeviceViewModel(ShowDevice device)
    {
        Device = device;
        _shown = Snapshot();
    }

    public ShowDevice Device { get; }

    public string Title => Device.Model.DisplayName;

    /// <summary>"Pixel Poi 5 · COM3 · “left”": what tells one connected device from another in a box.</summary>
    public string Label => string.IsNullOrEmpty(Device.Info.Tag) ? $"{Title} · {Device.ChannelId}" : $"{Title} · {Device.ChannelId} · “{Device.Info.Tag}”";

    public DeviceFamily Family => Device.Model.Family;

    /// <summary>The device is one that plays shows and is in its firmware (not in the bootloader).</summary>
    public bool CanPlayShows => DeviceMapping.KindOf(Family) is not null && Device.Model.IsSupported && !Device.Model.BootMode;

    /// <summary>The bootloader runs instead of the firmware: the device takes only firmware.</summary>
    public bool IsBootMode => Device.Model.BootMode;

    /// <summary>The firmware shipped with the app for this device; null when there is none for its family and platform (or the bootloader did not say which it is).</summary>
    public FirmwareImage? Firmware => FirmwareCatalog.For(Device.Model, Device.Info);

    /// <summary>The device is in its firmware and the app has a newer one.</summary>
    public bool UpdateAvailable => !IsBootMode && Firmware is { } image && image.Version > Device.Info.FirmwareVersion;

    /// <summary>The device can be given the firmware of the app: it has one for it.</summary>
    public bool CanUpdateFirmware => Device.Model.IsSupported && Firmware is not null;

    /// <summary>A device that is in its firmware is updated; one in the bootloader is given its firmware.</summary>
    public string FirmwareButtonText => IsBootMode ? Strings.Devices_InstallFirmware : Strings.Devices_UpdateFirmware;

    /// <summary>What to do about the firmware, if anything.</summary>
    public string FirmwareNote =>
        IsBootMode ? Strings.Devices_FirmwareBootNote
        : UpdateAvailable ? string.Format(Strings.Devices_FirmwareAvailable, Device.Info.FirmwareVersion, Firmware!.Version)
        : "";

    public bool HasFirmwareNote => FirmwareNote.Length > 0;

    public string Detail
    {
        get
        {
            var info = Device.Info;
            var parts = new List<string> { Device.ChannelId };
            if (Device.Model.BootMode) parts.Add(Strings.Devices_BootMode);
            parts.Add(string.Format(Strings.Devices_Firmware, info.PlatformVersion, info.BootVersion, info.FirmwareVersion));
            if (info.LedCount is { } leds) parts.Add(string.Format(Strings.Devices_LedsOf, leds));
            if (info.Channels is { } channels && Family == DeviceFamily.LightMaster) parts.Add(string.Format(Strings.Devices_Channels, channels));
            if (DeviceMapping.MemoryBytes(Device.Model, info) is > 0 and var memory) parts.Add(string.Format(Strings.Devices_MemoryOf, DevicesPanelViewModel.FormatSize(memory)));
            if (info.Battery is { } battery) parts.Add(string.Format(Strings.Devices_Battery, battery));
            if (!string.IsNullOrEmpty(info.Tag)) parts.Add("“" + info.Tag + "”");
            if (Device.IsBusy) parts.Add(Strings.Devices_Busy);
            return string.Join(" · ", parts);
        }
    }

    private string Snapshot() => Detail + "\n" + FirmwareNote + "\n" + FirmwareButtonText;

    /// <summary>Reads the state of the device again. True when anything the line shows changed (and only then is anything raised).</summary>
    public bool Refresh()
    {
        string now = Snapshot();
        if (now == _shown) return false;
        _shown = now;
        OnPropertyChanged(string.Empty);   // every property
        return true;
    }

    public override string ToString() => Title;
}

/// <summary>
/// The "Devices" tab of the editor: the devices that are connected (cable or Wi-Fi, found by the hub while the app runs) and what is done with the selected one:
/// the project is set up for it (<see cref="UploadPlanViewModel"/>: the timelines, the settings the files are built with) and then uploaded when the user presses
/// Upload; its firmware is updated. Only physical devices: nothing of this is a "device of the project". A file is built by <see cref="ShowBuilder"/>, which
/// refuses what a device cannot take before anything is sent; the user cannot save it as a file (the show is for the device, not for being passed around).
/// </summary>
public sealed partial class DevicesPanelViewModel : ObservableObject, IDisposable
{
    /// <summary>The kinds of file, in words.</summary>
    public static (string Kind, string Title)[] Kinds =>
    [
        (DeviceKind.Poi, Strings.Devices_KindPoi),
        (DeviceKind.Hoop, Strings.Devices_KindHoop),
        (DeviceKind.Fan, Strings.Devices_KindFan),
        (DeviceKind.Video, Strings.Devices_KindVideo),
        (DeviceKind.EpicPoi, Strings.Devices_KindEpic),
        (DeviceKind.Light, Strings.Devices_KindLight),
    ];

    private readonly EditorViewModel _editor;
    private readonly IDeviceHub? _hub;
    private readonly Func<IFilePicker?> _picker;
    private readonly Func<IDialogService?> _dialogs;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly Dictionary<ShowDevice, UploadPlanViewModel> _plans = [];
    private CancellationTokenSource? _uploading;
    private AfterBoot? _afterBoot;

    /// <summary>A device was asked to restart into its bootloader; the firmware is written when it shows up there.</summary>
    private sealed record AfterBoot(DeviceFamily Family, FirmwareImage Image, DateTime Deadline);

    /// <summary>How long a device is given to come back in its bootloader.</summary>
    internal static readonly TimeSpan BootWait = TimeSpan.FromSeconds(60);

    public DevicesPanelViewModel(EditorViewModel editor, IDeviceHub? hub, Func<IFilePicker?> picker, Func<IDialogService?>? dialogs = null)
    {
        _editor = editor;
        _hub = hub;
        _picker = picker;
        _dialogs = dialogs ?? (() => null);
        if (hub is not null)
        {
            hub.DeviceFound += DeviceFound;
            hub.DeviceLost += DeviceLost;
            foreach (var device in hub.Devices) Connected.Add(new ConnectedDeviceViewModel(device));
            SelectedConnected = Connected.FirstOrDefault();
        }
    }

    public void Dispose()
    {
        if (_hub is null) return;
        _hub.DeviceFound -= DeviceFound;
        _hub.DeviceLost -= DeviceLost;
        _uploading?.Cancel();
    }

    public static string KindTitle(string kind) => Kinds.FirstOrDefault(k => k.Kind == kind).Title ?? kind;

    internal ShowProject Project => _editor.Document.Project;
    internal ShowMedia Media => _editor.BinMedia;

    /// <summary>Whether this app has devices to talk to at all (Studio does).</summary>
    public bool HasDevices => _hub is not null;

    [ObservableProperty] private string? _message;

    public bool HasMessage => Message is not null;

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    internal static string FormatSize(long bytes) =>
        bytes >= 1024L * 1024 * 1024 ? string.Format("{0:0.##} GB", bytes / 1024.0 / 1024.0 / 1024.0)
        : bytes >= 1024 * 1024 ? string.Format("{0:0.##} MB", bytes / 1024.0 / 1024.0)
        : string.Format("{0:0.#} KB", bytes / 1024.0);

    /// <summary>Runs an edit of the build settings; what the project refuses is told in a line instead of thrown at the user.</summary>
    internal void Do(Func<Edit?> edit)
    {
        try
        {
            if (edit() is { } made) _editor.Document.Execute(made);
            Message = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Message = ex.Message;
            Refresh();   // the boxes show what is true again
        }
    }

    /// <summary>The project changed (any edit, undo or redo): the plans list the timelines again, and the build settings are read again.</summary>
    public void Refresh()
    {
        foreach (var plan in _plans.Values) plan.Refresh();
        OnPropertyChanged(nameof(DepthIndex));
        OnPropertyChanged(nameof(GapStretching));
        OnPropertyChanged(nameof(LightRate));
        NotifyCommands();
    }

    // ---- what the files are built with (the settings of PixelTuner 2 that reach the file)

    public string[] Depths { get; } = ["12 bit", "24 bit", "36 bit"];

    public int DepthIndex
    {
        get => (int)Project.Build.Depth;
        set
        {
            if (value < 0 || value >= Depths.Length || value == DepthIndex) return;
            Do(() => Edits.SetColorDepth(Project, (ColorDepth)value));
        }
    }

    /// <summary>Stretching of the black entries that fill the pauses.</summary>
    public int GapStretching
    {
        get => Project.Build.GapStretching;
        set
        {
            if (value == Project.Build.GapStretching) return;
            if (value < Edits.MinStretching || value > Edits.MaxEpicStretching) { Message = string.Format(Strings.Devices_OutOfRange, Edits.MinStretching, Edits.MaxEpicStretching); OnPropertyChanged(); return; }
            Do(() => Edits.SetGapStretching(Project, value));
        }
    }

    /// <summary>LightMaster: milliseconds between samples.</summary>
    public int LightRate
    {
        get => Project.Build.LightRate;
        set
        {
            if (value == Project.Build.LightRate) return;
            if (value < 1 || value > Edits.MaxLightRate) { Message = string.Format(Strings.Devices_OutOfRange, 1, Edits.MaxLightRate); OnPropertyChanged(); return; }
            Do(() => Edits.SetLightRate(Project, value));
        }
    }

    // ---- the connected devices

    public ObservableCollection<ConnectedDeviceViewModel> Connected { get; } = [];

    [ObservableProperty] private ConnectedDeviceViewModel? _selectedConnected;

    public bool HasConnected => Connected.Count > 0;

    /// <summary>What is set up to be sent to the selected device; null when none is selected or it takes no show (a bootloader, an unknown device).</summary>
    public UploadPlanViewModel? SelectedPlan =>
        SelectedConnected is { CanPlayShows: true } connected
            ? _plans.TryGetValue(connected.Device, out var plan) ? plan : _plans[connected.Device] = new UploadPlanViewModel(this, connected)
            : null;

    public bool HasPlan => SelectedPlan is not null;

    /// <summary>A device is selected but takes no show (it is in its bootloader, or it is not one that plays shows).</summary>
    public bool HasNoShowDevice => SelectedConnected is { CanPlayShows: false };

    /// <summary>Why part of the search does not work (the Wi-Fi port is taken), or empty.</summary>
    public string SearchProblem => _hub?.Problem is { } problem ? string.Format(Strings.Devices_WifiPortBusy, DeviceManager.NetworkPort) + " (" + problem + ")" : "";

    public bool HasSearchProblem => SearchProblem.Length > 0;

    private string _shownProblem = "";

    partial void OnSelectedConnectedChanged(ConnectedDeviceViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedPlan));
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(HasNoShowDevice));
        NotifyCommands();
    }

    /// <summary>A plan changed: the buttons that depend on what is chosen are asked again.</summary>
    internal void PlanChanged() => NotifyCommands();

    private void DeviceFound(object? sender, ShowDevice device) => OnUi(() =>
    {
        if (Connected.Any(c => ReferenceEquals(c.Device, device))) return;
        var added = new ConnectedDeviceViewModel(device);
        Connected.Add(added);
        SelectedConnected ??= added;
        OnPropertyChanged(nameof(HasConnected));
        ContinueFirmware(added);
    });

    private void DeviceLost(object? sender, ShowDevice device) => OnUi(() =>
    {
        if (Connected.FirstOrDefault(c => ReferenceEquals(c.Device, device)) is not { } gone) return;
        _plans.Remove(device);
        Connected.Remove(gone);
        if (ReferenceEquals(SelectedConnected, gone)) SelectedConnected = Connected.FirstOrDefault();
        OnPropertyChanged(nameof(HasConnected));
    });

    private void OnUi(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }

    /// <summary>The state of the connected devices is asked for every second by the hub; the lines are read again, in place.</summary>
    public void Poll()
    {
        bool changed = false;
        foreach (var device in Connected)
        {
            if (!device.Refresh()) continue;
            changed = true;
            if (_plans.TryGetValue(device.Device, out var plan)) plan.DeviceChanged();
        }
        if (SearchProblem != _shownProblem)
        {
            _shownProblem = SearchProblem;
            OnPropertyChanged(nameof(SearchProblem));
            OnPropertyChanged(nameof(HasSearchProblem));
        }
        if (_afterBoot is { } pending && DateTime.UtcNow > pending.Deadline)
        {
            _afterBoot = null;
            Message = Strings.Devices_BootTimeout;
        }
        if (changed) NotifyCommands();
    }

    // ---- the transfers (a show or firmware)

    [ObservableProperty] private bool _isUploading;

    /// <summary>Share of the file sent, 0 to 1.</summary>
    [ObservableProperty] private double _progress;

    /// <summary>The transfer that runs can be stopped: a show can (between blocks), firmware cannot (a half-written flash is not a state to stop in).</summary>
    [ObservableProperty] private bool _canCancel;

    partial void OnCanCancelChanged(bool value) => CancelUploadCommand.NotifyCanExecuteChanged();

    partial void OnIsUploadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotUploading));
        NotifyCommands();
    }

    public bool IsNotUploading => !IsUploading;

    /// <summary>
    /// Runs a transfer off the UI thread with progress shown, the line of the result told afterwards, and nothing else started meanwhile.
    /// True when it went through.
    /// </summary>
    private async Task<bool> TransferAsync(string progressFormat, bool cancellable, Action<IProgress<double>, CancellationToken> send, Func<string> done, string failedFormat)
    {
        _uploading = new CancellationTokenSource();
        var token = cancellable ? _uploading.Token : CancellationToken.None;
        Progress = 0;
        Message = string.Format(progressFormat, 0);
        CanCancel = cancellable;
        IsUploading = true;
        try
        {
            // Not Progress<T>: its reports are posted and may land after the "uploaded" line below.
            // A show of 16 MB is some 67,000 blocks: the screen is told when the percent changes, not after every block.
            int shown = -1;
            var report = new UiProgress(value =>
            {
                int percent = (int)(value * 100);
                if (percent == Volatile.Read(ref shown)) return;
                Volatile.Write(ref shown, percent);
                OnUi(() =>
                {
                    if (!IsUploading) return;
                    Progress = value;
                    Message = string.Format(progressFormat, percent);
                });
            });
            await Task.Run(() => send(report, token), token);
            Progress = 1;
            Message = done();
            return true;
        }
        catch (OperationCanceledException)
        {
            // TODO(release): the device is left in write mode (docs/before-release.md).
            Message = Strings.Devices_UploadCancelled;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException or NotSupportedException or ObjectDisposedException)
        {
            Message = string.Format(failedFormat, ex.Message);
        }
        finally
        {
            CanCancel = false;
            IsUploading = false;
            _uploading.Dispose();
            _uploading = null;
        }
        return false;
    }

    // ---- the upload of a show

    /// <summary>
    /// Sends the file of the plan of the selected device. Pressed by the user after the timelines and the settings are set; nothing is sent when the device
    /// cannot play the file (a bootloader, other LEDs, too little memory) or the build refuses, and the line says why.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task Upload()
    {
        if (SelectedConnected is not { } connected) { Message = Strings.Devices_NoConnected; return; }
        var device = connected.Device;

        if (!device.Model.IsSupported) { Message = string.Format(Strings.Devices_Unsupported, connected.Title); return; }
        if (device.Model.BootMode) { Message = string.Format(Strings.Devices_BootModeOnly, connected.Title); return; }
        if (SelectedPlan is not { } plan) { Message = string.Format(Strings.Devices_WrongFamily, connected.Title, KindTitle(DeviceKind.Poi)); return; }

        // The device is asked again what it has: the file has to fit in the memory it reports now.
        var result = plan.Build();
        if (result.Bin is not { } bin)
        {
            Message = result.Errors.FirstOrDefault() is { } error ? EditorViewModel.IssueText(error) : Strings.Devices_NotBuilt;
            return;
        }

        string names = string.Join(", ", plan.Binding.TimelineIds.Select(id => Project.Timelines.FirstOrDefault(t => t.Id == id)).OfType<Timeline>()
            .Select(t => string.IsNullOrEmpty(t.Name) ? Strings.Devices_Unnamed : t.Name));
        await TransferAsync(Strings.Devices_Uploading, cancellable: true,
            (report, token) => device.UploadShow(bin, report, token),
            () => string.Format(Strings.Devices_Uploaded, FormatSize(bin.Length), connected.Title, names),
            Strings.Devices_UploadFailed);
    }

    private bool CanUpload() => !IsUploading && SelectedConnected is { CanPlayShows: true };

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelUpload() => _uploading?.Cancel();

    private sealed class UiProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    // ---- firmware

    /// <summary>
    /// Gives the selected connected device the firmware shipped with the app (PixelTuner 2's "refresh brain"): a device in its firmware is asked
    /// to restart into the bootloader, and the firmware is written when it shows up there (PixelTuner 2 asked "continue update?" then; the answer
    /// here is the one given to the first question); a device that is in the bootloader already is written at once.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdateFirmware))]
    private async Task UpdateFirmware()
    {
        if (SelectedConnected is not { } connected) { Message = Strings.Devices_NoConnected; return; }
        if (connected.Firmware is not { } image) { Message = string.Format(Strings.Devices_NoFirmware, connected.Title); return; }
        await WriteFirmwareAsync(connected, image);
    }

    private bool CanUpdateFirmware() => !IsUploading && _afterBoot is null && SelectedConnected is { CanUpdateFirmware: true };

    /// <summary>Gives the device a firmware file chosen by the user (PixelTuner 2's firmware updater, to which a file was dropped).</summary>
    [RelayCommand(CanExecute = nameof(CanUpdateFromFile))]
    private async Task UpdateFirmwareFromFile()
    {
        if (SelectedConnected is not { } connected) { Message = Strings.Devices_NoConnected; return; }
        if (_picker() is not { } picker || await picker.PickFirmwarePathAsync() is not { } path) return;
        byte[] data;
        try
        {
            data = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = string.Format(Strings.Devices_FirmwareFailed, ex.Message);
            return;
        }
        if (!FirmwareImage.LooksValid(data)) { Message = string.Format(Strings.Devices_FirmwareBadFile, Path.GetFileName(path)); return; }
        await WriteFirmwareAsync(connected, new FirmwareImage(Path.GetFileName(path), data), fromFile: true);
    }

    private bool CanUpdateFromFile() => !IsUploading && _afterBoot is null && SelectedConnected is { Device.Model.IsSupported: true };

    private async Task WriteFirmwareAsync(ConnectedDeviceViewModel connected, FirmwareImage image, bool fromFile = false)
    {
        var device = connected.Device;
        if (_dialogs() is { } dialogs)
        {
            string detail = fromFile
                ? string.Format(Strings.Devices_FirmwareFileConfirm, image.Name, connected.Title)
                : string.Format(connected.IsBootMode ? Strings.Devices_FirmwareInstallConfirm : Strings.Devices_FirmwareConfirm, image.Version, connected.Title, device.Info.FirmwareVersion);
            if (!await dialogs.ConfirmAsync(Strings.Devices_FirmwareHeading, detail, connected.FirmwareButtonText)) return;
        }

        if (device.Model.BootMode)
        {
            await InstallFirmwareAsync(device, image);
            return;
        }

        // The device restarts into its bootloader and goes away from the list; it comes back there, and then the firmware is written.
        _afterBoot = new AfterBoot(device.Model.Family, image, DateTime.UtcNow + BootWait);
        Message = string.Format(Strings.Devices_RestartingToBoot, connected.Title);
        NotifyCommands();
        try
        {
            await Task.Run(device.SwitchToBoot);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException or NotSupportedException or ObjectDisposedException)
        {
            _afterBoot = null;
            Message = string.Format(Strings.Devices_FirmwareFailed, ex.Message);
            NotifyCommands();
        }
    }

    /// <summary>A device came up; if it is the one that was asked to restart into its bootloader, its firmware goes in.</summary>
    private void ContinueFirmware(ConnectedDeviceViewModel found)
    {
        if (_afterBoot is not { } pending || !found.IsBootMode || found.Family != pending.Family || DateTime.UtcNow > pending.Deadline) return;
        _afterBoot = null;
        Writing = InstallFirmwareAsync(found.Device, pending.Image);
    }

    /// <summary>The firmware write that started by itself when the bootloader came up (so a test can wait for it).</summary>
    internal Task? Writing { get; private set; }

    /// <summary>For tests: the wait for the bootloader has run out.</summary>
    internal void ExpireBootWait() => _afterBoot = _afterBoot! with { Deadline = DateTime.UtcNow.AddSeconds(-1) };

    private async Task InstallFirmwareAsync(ShowDevice device, FirmwareImage image)
    {
        await TransferAsync(Strings.Devices_WritingFirmware, cancellable: false,
            (report, _) => device.UploadFirmware(image.Data, report),
            () => string.Format(Strings.Devices_FirmwareWritten, device.Model.DisplayName),
            Strings.Devices_FirmwareFailed);
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        UploadCommand.NotifyCanExecuteChanged();
        UpdateFirmwareCommand.NotifyCanExecuteChanged();
        UpdateFirmwareFromFileCommand.NotifyCanExecuteChanged();
        CancelUploadCommand.NotifyCanExecuteChanged();
    }
}
