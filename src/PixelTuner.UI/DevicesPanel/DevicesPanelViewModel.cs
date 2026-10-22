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

/// <summary>A timeline a project device may play, with its place in the order of the file.</summary>
public sealed class TimelineChoiceViewModel(DeviceRowViewModel owner, Timeline timeline)
{
    public Timeline Timeline { get; } = timeline;

    public string Name => string.IsNullOrEmpty(Timeline.Name) ? Strings.Devices_Unnamed : Timeline.Name;

    /// <summary>The timeline is played by the device.</summary>
    public bool IsChecked
    {
        get => owner.Binding.TimelineIds.Contains(Timeline.Id);
        set => owner.SetPlayed(Timeline, value);
    }

    /// <summary>A poi or a hoop plays pictures as high as it has LEDs; a timeline of another height cannot be ticked.</summary>
    public bool Fits => owner.Fits(Timeline);

    public bool CanCheck => Fits || IsChecked;

    /// <summary>1-based place in the file; empty while it is not played.</summary>
    public string Order => owner.Binding.TimelineIds.IndexOf(Timeline.Id) is var index and >= 0 ? (index + 1).ToString() : "";

    public string Detail
    {
        get
        {
            string items = string.Format(Strings.Devices_ItemCount, Timeline.Items.Count);
            return Timeline.Kind == TimelineKind.Pixel && Edits.PixelHeight(Timeline) is > 0 and var height
                ? string.Format(Strings.Devices_TimelineHeight, items, height) + (Fits ? "" : " · " + string.Format(Strings.Devices_DoesNotFit, height, owner.Binding.LedCount))
                : items;
        }
    }
}

/// <summary>A device of the project (what a .bin is built for): its kind, LEDs and memory, and the timelines it plays in order.</summary>
public sealed partial class DeviceRowViewModel : ObservableObject
{
    private readonly DevicesPanelViewModel _panel;
    private BuildResult? _result;

    public DeviceRowViewModel(DevicesPanelViewModel panel, DeviceBinding binding)
    {
        _panel = panel;
        Binding = binding;
        Refresh();
    }

    public DeviceBinding Binding { get; }

    public IReadOnlyList<TimelineChoiceViewModel> Choices { get; private set; } = [];

    public override string ToString() => Title;

    public string Title => string.IsNullOrEmpty(Binding.Name) ? KindText : Binding.Name;

    public string KindText => DevicesPanelViewModel.KindTitle(Binding.DeviceType);

    public string[] KindTitles { get; } = [.. DevicesPanelViewModel.Kinds.Select(k => k.Title)];

    // ---- what the user edits

    public string Name
    {
        get => Binding.Name;
        set
        {
            if (value == Binding.Name) return;
            _panel.Do(() => Edits.RenameDevice(Binding, value));
        }
    }

    public int KindIndex
    {
        get => Array.FindIndex(DevicesPanelViewModel.Kinds, k => k.Kind == Binding.DeviceType);
        set
        {
            if (value < 0 || value >= DevicesPanelViewModel.Kinds.Length || DevicesPanelViewModel.Kinds[value].Kind == Binding.DeviceType) return;
            _panel.Do(() => Edits.SetDeviceKind(_panel.Project, Binding, DevicesPanelViewModel.Kinds[value].Kind));
        }
    }

    /// <summary>LEDs of the device; 0 when not known (the first timeline then decides).</summary>
    public int LedCount
    {
        get => Binding.LedCount;
        set
        {
            if (value == Binding.LedCount) return;
            _panel.Do(() => Edits.SetDeviceLedCount(_panel.Project, Binding, value));
        }
    }

    public bool HasLedCount => Binding.DeviceType is DeviceKind.Poi or DeviceKind.Hoop;

    /// <summary>What the device can hold in kilobytes; 0 when not known (then the size is not checked).</summary>
    public long MemoryKb
    {
        get => Binding.MemoryBytes / 1024;
        set
        {
            if (value * 1024 == Binding.MemoryBytes || value < 0) return;
            _panel.Do(() => Edits.SetDeviceMemory(Binding, value * 1024));
        }
    }

    public string Tag
    {
        get => Binding.NetworkTag ?? "";
        set
        {
            if ((string.IsNullOrWhiteSpace(value) ? null : value.Trim()) == Binding.NetworkTag) return;
            _panel.Do(() => Edits.SetDeviceTag(Binding, value));
        }
    }

    // ---- the timelines

    internal bool Fits(Timeline timeline) =>
        Binding.DeviceType is not (DeviceKind.Poi or DeviceKind.Hoop) || Binding.LedCount <= 0 || Edits.FitsHeight(timeline, Binding.LedCount);

    internal void SetPlayed(Timeline timeline, bool played)
    {
        var ids = Binding.TimelineIds.ToList();
        if (played == ids.Contains(timeline.Id)) return;
        if (played) ids.Add(timeline.Id);
        else ids.Remove(timeline.Id);
        _panel.Do(() => Edits.SetDeviceTimelines(_panel.Project, Binding, ids));
    }

    /// <summary>Moves a played timeline one place earlier (-1) or later (1) in the file.</summary>
    internal void Move(TimelineChoiceViewModel choice, int direction)
    {
        var ids = Binding.TimelineIds.ToList();
        int at = ids.IndexOf(choice.Timeline.Id), to = at + direction;
        if (at < 0 || to < 0 || to >= ids.Count) return;
        (ids[at], ids[to]) = (ids[to], ids[at]);
        _panel.Do(() => Edits.SetDeviceTimelines(_panel.Project, Binding, ids));
    }

    [RelayCommand]
    private void MoveUp(TimelineChoiceViewModel? choice)
    {
        if (choice is not null) Move(choice, -1);
    }

    [RelayCommand]
    private void MoveDown(TimelineChoiceViewModel? choice)
    {
        if (choice is not null) Move(choice, 1);
    }

    // ---- the file

    /// <summary>The file built for this device (cached until the project changes); the issues are in it.</summary>
    public BuildResult Result => _result ??= Build();

    private BuildResult Build()
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
            if (result.Bin is { } bin)
            {
                string size = FormatSize(bin.Length);
                string text = Binding.MemoryBytes > 0 ? string.Format(Strings.Devices_FileInfoOf, size, FormatSize(Binding.MemoryBytes)) : string.Format(Strings.Devices_FileInfo, size);
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

    internal static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? string.Format("{0:0.##} MB", bytes / 1024.0 / 1024.0) : string.Format("{0:0.#} KB", bytes / 1024.0);

    /// <summary>The project changed: the file is built again when it is next asked for, and the lists are made again.</summary>
    public void Refresh()
    {
        _result = null;
        Choices = [.. _panel.Project.Timelines.Where(t => t.Kind == Edits.TimelineKindOf(Binding.DeviceType)).Select(t => new TimelineChoiceViewModel(this, t))];
        OnPropertyChanged(string.Empty);   // every property
    }
}

/// <summary>A device that answered on a cable or over Wi-Fi.</summary>
public sealed class ConnectedDeviceViewModel(ShowDevice device)
{
    public ShowDevice Device { get; } = device;

    public string Title => Device.Model.DisplayName;

    public DeviceFamily Family => Device.Model.Family;

    /// <summary>The device is one that plays shows and is in its firmware (not in the bootloader).</summary>
    public bool CanPlayShows => DeviceMapping.KindOf(Family) is not null && Device.Model.IsSupported && !Device.Model.BootMode;

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
            if (DeviceMapping.MemoryBytes(Device.Model, info) is > 0 and var memory) parts.Add(string.Format(Strings.Devices_MemoryOf, DeviceRowViewModel.FormatSize(memory)));
            if (info.Battery is { } battery) parts.Add(string.Format(Strings.Devices_Battery, battery));
            if (!string.IsNullOrEmpty(info.Tag)) parts.Add("“" + info.Tag + "”");
            if (Device.IsBusy) parts.Add(Strings.Devices_Busy);
            return string.Join(" · ", parts);
        }
    }

    public override string ToString() => Title;
}

/// <summary>
/// The "Devices" tab of the editor (PixelTuner 2's devices pane and its "timelines to upload" window in one): the devices of the project with
/// the timelines each plays, the settings the files are built with, and the devices that are connected. A file is built by
/// <see cref="ShowBuilder"/>, which refuses what a device cannot take before anything is sent; it can be saved to a .bin or uploaded to a connected
/// device of the right family, with progress and a way to cancel.
/// </summary>
public sealed partial class DevicesPanelViewModel : ObservableObject, IDisposable
{
    /// <summary>The kinds of project device, in the order of the box.</summary>
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
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private CancellationTokenSource? _uploading;

    public DevicesPanelViewModel(EditorViewModel editor, IDeviceHub? hub, Func<IFilePicker?> picker)
    {
        _editor = editor;
        _hub = hub;
        _picker = picker;
        if (hub is not null)
        {
            hub.DeviceFound += DeviceFound;
            hub.DeviceLost += DeviceLost;
            foreach (var device in hub.Devices) Connected.Add(new ConnectedDeviceViewModel(device));
            SelectedConnected = Connected.FirstOrDefault();
        }
        Refresh();
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

    // ---- the project's devices

    public ObservableCollection<DeviceRowViewModel> Rows { get; } = [];

    [ObservableProperty] private DeviceRowViewModel? _selected;

    public bool HasSelected => Selected is not null;

    partial void OnSelectedChanged(DeviceRowViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelected));
        NotifyCommands();
    }

    [ObservableProperty] private string? _message;

    public bool HasMessage => Message is not null;

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    /// <summary>Runs an edit of a device; what the project refuses is told in a line instead of thrown at the user.</summary>
    internal void Do(Func<Edit?> edit)
    {
        try
        {
            // The factories of the edits check the rules (a poi as high as its pictures), so they are called inside the try.
            if (edit() is { } made) _editor.Document.Execute(made);
            Message = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Message = ex.Message;
            Refresh();   // the boxes show what is true again
        }
    }

    /// <summary>The project changed (any edit, undo or redo): the rows follow the devices of the project.</summary>
    public void Refresh()
    {
        var selected = Selected?.Binding;
        for (int i = Rows.Count - 1; i >= 0; i--)
            if (!Project.Devices.Contains(Rows[i].Binding)) Rows.RemoveAt(i);
        for (int i = 0; i < Project.Devices.Count; i++)
        {
            var binding = Project.Devices[i];
            var row = Rows.FirstOrDefault(r => ReferenceEquals(r.Binding, binding));
            if (row is null) Rows.Insert(i, new DeviceRowViewModel(this, binding));
            else
            {
                int at = Rows.IndexOf(row);
                if (at != i) Rows.Move(at, i);
                row.Refresh();
            }
        }
        Selected = Rows.FirstOrDefault(r => ReferenceEquals(r.Binding, selected)) ?? Rows.FirstOrDefault();
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

    // ---- adding and removing

    /// <summary>A new device of a kind, playing every timeline of its kind that has something on it and fits.</summary>
    [RelayCommand]
    private void AddDevice(string? kind)
    {
        kind ??= DeviceKind.Poi;
        var device = new DeviceBinding { DeviceType = kind, Name = NewName(kind) };
        var suitable = Project.Timelines.Where(t => t.Kind == Edits.TimelineKindOf(kind) && t.Items.Count > 0).Take(Edits.MaxTimelinesOf(kind)).Select(t => t.Id).ToList();
        device.TimelineIds = suitable;
        Do(() => Edits.AddDevice(Project, device));
        Selected = Rows.FirstOrDefault(r => ReferenceEquals(r.Binding, device));
    }

    private string NewName(string kind)
    {
        int n = Project.Devices.Count(d => d.DeviceType == kind) + 1;
        return $"{KindTitle(kind)} {n}";
    }

    [RelayCommand(CanExecute = nameof(HasSelected))]
    private void RemoveDevice()
    {
        if (Selected is { } row) Do(() => Edits.RemoveDevice(Project, row.Binding));
    }

    // ---- the connected devices

    public ObservableCollection<ConnectedDeviceViewModel> Connected { get; } = [];

    [ObservableProperty] private ConnectedDeviceViewModel? _selectedConnected;

    public bool HasConnected => Connected.Count > 0;

    public bool IsSearching => _hub?.IsSearching ?? false;

    partial void OnSelectedConnectedChanged(ConnectedDeviceViewModel? value) => NotifyCommands();

    private void DeviceFound(object? sender, ShowDevice device) => OnUi(() =>
    {
        if (Connected.Any(c => ReferenceEquals(c.Device, device))) return;
        Connected.Add(new ConnectedDeviceViewModel(device));
        SelectedConnected ??= Connected[^1];
        OnPropertyChanged(nameof(HasConnected));
    });

    private void DeviceLost(object? sender, ShowDevice device) => OnUi(() =>
    {
        if (Connected.FirstOrDefault(c => ReferenceEquals(c.Device, device)) is not { } gone) return;
        Connected.Remove(gone);
        if (ReferenceEquals(SelectedConnected, gone)) SelectedConnected = Connected.FirstOrDefault();
        OnPropertyChanged(nameof(HasConnected));
    });

    private void OnUi(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }

    /// <summary>The state of the connected devices is asked for every second by the hub; the lines are read again.</summary>
    public void Poll()
    {
        OnPropertyChanged(nameof(Connected));
        foreach (var device in Connected.ToList())
        {
            int at = Connected.IndexOf(device);
            Connected[at] = new ConnectedDeviceViewModel(device.Device);   // new line, same device
            if (ReferenceEquals(SelectedConnected, device)) SelectedConnected = Connected[at];
        }
    }

    [RelayCommand]
    private async Task ToggleSearch()
    {
        if (_hub is null) return;
        if (_hub.IsSearching) await _hub.StopSearchingAsync();
        else _hub.StartSearching();
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(SearchText));
    }

    public string SearchText => IsSearching ? Strings.Devices_Stop : Strings.Devices_Search;

    /// <summary>A device of the project made from what a connected device is: its kind, LEDs, memory and name, playing what fits it.</summary>
    [RelayCommand(CanExecute = nameof(CanAddFromConnected))]
    private void AddFromConnected()
    {
        if (SelectedConnected is not { } connected || DeviceMapping.KindOf(connected.Family) is not { } kind) return;
        var info = connected.Device.Info;
        var device = new DeviceBinding
        {
            DeviceType = kind,
            Name = string.IsNullOrEmpty(info.Tag) ? NewName(kind) : info.Tag,
            LedCount = kind is DeviceKind.Poi or DeviceKind.Hoop ? info.LedCount ?? 0 : 0,
            MemoryBytes = DeviceMapping.MemoryBytes(connected.Device.Model, info),
            NetworkTag = string.IsNullOrEmpty(info.Tag) ? null : info.Tag,
        };
        device.TimelineIds = [.. Project.Timelines
            .Where(t => t.Kind == Edits.TimelineKindOf(kind) && t.Items.Count > 0 && (device.LedCount <= 0 || kind is not (DeviceKind.Poi or DeviceKind.Hoop) || Edits.FitsHeight(t, device.LedCount)))
            .Take(Edits.MaxTimelinesOf(kind)).Select(t => t.Id)];
        Do(() => Edits.AddDevice(Project, device));
        Selected = Rows.FirstOrDefault(r => ReferenceEquals(r.Binding, device));
    }

    private bool CanAddFromConnected() => SelectedConnected is { CanPlayShows: true };

    /// <summary>The selected project device takes the LEDs, the memory and the name on the network of the selected connected device.</summary>
    [RelayCommand(CanExecute = nameof(CanTake))]
    private void TakeFromConnected()
    {
        if (Selected is not { } row || SelectedConnected is not { } connected) return;
        var info = connected.Device.Info;
        long memory = DeviceMapping.MemoryBytes(connected.Device.Model, info);
        Do(() =>
        {
            var edits = new List<Edit>();
            if (row.HasLedCount && info.LedCount is { } leds && leds != row.Binding.LedCount) edits.Add(Edits.SetDeviceLedCount(Project, row.Binding, leds));
            if (memory > 0 && memory != row.Binding.MemoryBytes) edits.Add(Edits.SetDeviceMemory(row.Binding, memory));
            if (!string.IsNullOrEmpty(info.Tag) && info.Tag != row.Binding.NetworkTag) edits.Add(Edits.SetDeviceTag(row.Binding, info.Tag));
            return edits.Count switch { 0 => null, 1 => edits[0], _ => new GroupEdit("SetDeviceLedCount", edits) };
        });
    }

    private bool CanTake() => Selected is not null && SelectedConnected is { CanPlayShows: true };

    // ---- the file

    /// <summary>Saves the file built for the selected device to a .bin (PixelTuner 2's "export .bin").</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportBin()
    {
        if (Selected is not { } row) return;
        if (row.Result.Bin is not { } bin)
        {
            Message = row.Summary;
            return;
        }
        if (_picker() is not { } picker) return;
        string? path = await picker.PickBinSavePathAsync((string.IsNullOrEmpty(row.Binding.Name) ? row.KindText : row.Binding.Name) + ".bin");
        if (path is null) return;
        try
        {
            await File.WriteAllBytesAsync(path, bin);
            Message = string.Format(Strings.Devices_Exported, Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = string.Format(Strings.Devices_ExportFailed, ex.Message);
        }
    }

    private bool CanExport() => Selected is not null;

    // ---- the upload

    [ObservableProperty] private bool _isUploading;

    /// <summary>Share of the file sent, 0 to 1.</summary>
    [ObservableProperty] private double _progress;

    partial void OnIsUploadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotUploading));
        NotifyCommands();
    }

    public bool IsNotUploading => !IsUploading;

    /// <summary>
    /// Writes the file of the selected project device into the selected connected device. Nothing is sent when the device cannot play it (another
    /// family, in its bootloader, a different number of LEDs, too little memory) or the build refuses; the line says why.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task Upload()
    {
        if (Selected is not { } row) { Message = Strings.Devices_NoProjectDevice; return; }
        if (SelectedConnected is not { } connected) { Message = Strings.Devices_NoConnected; return; }
        var device = connected.Device;

        if (!device.Model.IsSupported) { Message = string.Format(Strings.Devices_Unsupported, connected.Title); return; }
        if (device.Model.BootMode) { Message = string.Format(Strings.Devices_BootModeOnly, connected.Title); return; }
        if (!DeviceMapping.Plays(device.Model.Family, row.Binding.DeviceType)) { Message = string.Format(Strings.Devices_WrongFamily, connected.Title, row.KindText); return; }
        if (row.HasLedCount && device.Info.LedCount is { } leds && row.Binding.LedCount > 0 && row.Binding.LedCount != leds)
        {
            Message = string.Format(Strings.Devices_WrongLeds, leds, row.Binding.LedCount);
            return;
        }

        // The memory the device reports now is what the file has to fit in, whatever the project device remembered.
        var binding = new DeviceBinding
        {
            DeviceType = row.Binding.DeviceType, TimelineIds = row.Binding.TimelineIds,
            LedCount = row.Binding.LedCount > 0 || !row.HasLedCount ? row.Binding.LedCount : device.Info.LedCount ?? 0,
            MemoryBytes = DeviceMapping.MemoryBytes(device.Model, device.Info) is > 0 and var reported ? reported : row.Binding.MemoryBytes,
        };
        var result = new ShowBuilder(Project, Media).Build(binding);
        if (result.Bin is not { } bin)
        {
            Message = result.Errors.FirstOrDefault() is { } error ? EditorViewModel.IssueText(error) : Strings.Devices_NotBuilt;
            return;
        }

        _uploading = new CancellationTokenSource();
        var token = _uploading.Token;
        Progress = 0;
        Message = string.Format(Strings.Devices_Uploading, 0);
        IsUploading = true;
        try
        {
            // Not Progress<T>: its reports are posted and may land after the "uploaded" line below.
            var report = new UiProgress(value => OnUi(() =>
            {
                if (!IsUploading) return;
                Progress = value;
                Message = string.Format(Strings.Devices_Uploading, (int)(value * 100));
            }));
            await Task.Run(() => device.UploadShow(bin, report, token), token);
            Progress = 1;
            Message = string.Format(Strings.Devices_Uploaded, DeviceRowViewModel.FormatSize(bin.Length), connected.Title);
        }
        catch (OperationCanceledException)
        {
            // TODO(release): the device is left in write mode (docs/before-release.md).
            Message = Strings.Devices_UploadCancelled;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException or NotSupportedException or ObjectDisposedException)
        {
            Message = string.Format(Strings.Devices_UploadFailed, ex.Message);
        }
        finally
        {
            IsUploading = false;
            _uploading.Dispose();
            _uploading = null;
        }
    }

    private bool CanUpload() => !IsUploading && Selected is not null && SelectedConnected is not null;

    [RelayCommand(CanExecute = nameof(IsUploading))]
    private void CancelUpload() => _uploading?.Cancel();

    private sealed class UiProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private void NotifyCommands()
    {
        RemoveDeviceCommand.NotifyCanExecuteChanged();
        AddFromConnectedCommand.NotifyCanExecuteChanged();
        TakeFromConnectedCommand.NotifyCanExecuteChanged();
        ExportBinCommand.NotifyCanExecuteChanged();
        UploadCommand.NotifyCanExecuteChanged();
        CancelUploadCommand.NotifyCanExecuteChanged();
    }
}
