using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Devices;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;

namespace PixelTuner.UI.DevicesPanel;

/// <summary>A network of this computer that a device can be told to connect to: its name and the address on it.</summary>
public sealed record NetworkChoice(string Name, string Address)
{
    public override string ToString() => $"{Name} · {Address}";

    /// <summary>The wired and wireless networks that are up and have an IPv4 address, as PixelTuner 2 listed them.</summary>
    public static IReadOnlyList<NetworkChoice> Available()
    {
        var found = new List<NetworkChoice>();
        try
        {
            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.OperationalStatus != OperationalStatus.Up
                    || network.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)) continue;
                foreach (var address in network.GetIPProperties().UnicastAddresses)
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                        found.Add(new NetworkChoice(network.Name, address.Address.ToString()));
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
        {
            // No list: the Wi-Fi settings cannot be written without an address.
        }
        return found;
    }
}

/// <summary>
/// The "Device settings" tab: the settings window of PixelTuner 2 (<c>DefaultsWindow</c>) for the selected connected device, apart from the project: the name
/// on the network, the Wi-Fi network the device joins and the address of this computer, the LED strip (LEDs on each channel, chip, colour order), the menu
/// of a baton and the leader flag. PixelTuner 2 wrote each group to every connected device of a class; this writes to one, which is chosen here. The groups
/// that the device takes are shown (<see cref="DeviceSettingsCatalog.SupportFor"/>). What was written last is kept (<see cref="DeviceSettingsStore"/>), because
/// devices only report their name.
/// </summary>
public sealed partial class DeviceSettingsViewModel : ObservableObject
{
    private readonly DeviceSettingsStore _store;

    public DeviceSettingsViewModel(DevicesPanelViewModel panel, DeviceSettingsStore store, Func<IReadOnlyList<NetworkChoice>>? networks = null)
    {
        Panel = panel;
        _store = store;
        Networks = [.. (networks ?? NetworkChoice.Available)()];

        _ssid = store.Ssid;
        _password = store.Password;
        _port = store.Port;
        _networkIndex = Math.Max(0, Networks.ToList().FindIndex(n => n.Name == store.Interface));
        _typeIndex = Math.Clamp(store.LedType, 0, DeviceSettingsCatalog.LedTypes.Count - 1);
        _orderIndex = Math.Clamp(store.ColourOrder, 0, DeviceSettingsCatalog.ColourOrders.Count - 1);
        _channel1 = Math.Clamp(store.Channel1, 0, DeviceSettingsCatalog.MaxLedsOnChannel(_typeIndex, 1));
        _channel2 = Math.Clamp(store.Channel2, 0, DeviceSettingsCatalog.MaxLedsOnChannel(_typeIndex, 2));
        _menuSeconds = Math.Clamp(store.MenuSeconds, 0, DeviceSettingsCatalog.MaxMenuSeconds);
        _exitOnDoubleClick = store.ExitOnDoubleClick;

        panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DevicesPanelViewModel.SelectedConnected)) SelectionChanged();
            else if (e.PropertyName is nameof(DevicesPanelViewModel.IsUploading)) NotifyCommands();
        };
        panel.Connected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasConnected));
        SelectionChanged();
    }

    /// <summary>The Devices tab's panel: the connected devices and the one chosen.</summary>
    public DevicesPanelViewModel Panel { get; }

    public IReadOnlyList<NetworkChoice> Networks { get; }

    public IReadOnlyList<string> LedTypes => DeviceSettingsCatalog.LedTypes;

    public IReadOnlyList<string> ColourOrders => DeviceSettingsCatalog.ColourOrders;

    public bool HasConnected => Panel.Connected.Count > 0;

    // ---- what the selected device takes

    private DeviceSettingsSupport Support => Panel.SelectedConnected is { } c ? DeviceSettingsCatalog.SupportFor(c.Device.Model, c.Device.Info) : default;

    public bool HasName => Support.Name;
    public bool HasWifi => Support.Wifi;
    public bool HasStrip => Support.Strip;
    public bool HasMenu => Support.Menu;
    public bool HasLeader => Support.Leader;

    /// <summary>A device is chosen and takes none of the groups (an E-pic poi, a bootloader).</summary>
    public bool TakesNothing => Panel.SelectedConnected is not null && !Support.Any;

    private void SelectionChanged()
    {
        foreach (string name in new[] { nameof(HasName), nameof(HasWifi), nameof(HasStrip), nameof(HasMenu), nameof(HasLeader), nameof(TakesNothing) })
            OnPropertyChanged(name);
        // The name is what the device reports; the rest is what was written last.
        Name = Panel.SelectedConnected?.Device.Info.Tag ?? "";
        Message = null;
        NotifyCommands();
    }

    // ---- the boxes

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _ssid;
    [ObservableProperty] private string _password;
    [ObservableProperty] private int _networkIndex;
    [ObservableProperty] private int _port;
    [ObservableProperty] private int _channel1;
    [ObservableProperty] private int _channel2;
    [ObservableProperty] private int _typeIndex;
    [ObservableProperty] private int _orderIndex;
    [ObservableProperty] private bool _exitOnDoubleClick;
    [ObservableProperty] private int _menuSeconds;
    [ObservableProperty] private bool _leader;

    public int MaxChannel1 => DeviceSettingsCatalog.MaxLedsOnChannel(TypeIndex, 1);
    public int MaxChannel2 => DeviceSettingsCatalog.MaxLedsOnChannel(TypeIndex, 2);

    /// <summary>An apa102 strip has one channel.</summary>
    public bool HasChannel2 => MaxChannel2 > 0;

    public int MaxMenuSeconds => DeviceSettingsCatalog.MaxMenuSeconds;
    public int MaxNameLength => DeviceSettingsCatalog.MaxNameLength;
    public int MaxSsidLength => DeviceSettingsCatalog.MaxSsidLength;
    public int MaxPasswordLength => DeviceSettingsCatalog.MaxPasswordLength;

    partial void OnTypeIndexChanged(int value)
    {
        Channel1 = Math.Min(Channel1, MaxChannel1);
        Channel2 = Math.Min(Channel2, MaxChannel2);
        OnPropertyChanged(nameof(MaxChannel1));
        OnPropertyChanged(nameof(MaxChannel2));
        OnPropertyChanged(nameof(HasChannel2));
    }

    /// <summary>"Turn off the device menu after 12 s" or "Don't turn off".</summary>
    public string MenuSecondsText => MenuSeconds == 0 ? Strings.DevSet_MenuNever : string.Format(Strings.DevSet_MenuAfter, MenuSeconds);

    partial void OnMenuSecondsChanged(int value) => OnPropertyChanged(nameof(MenuSecondsText));

    // ---- writing

    [ObservableProperty] private string? _message;

    public bool HasMessage => Message is not null;

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    [ObservableProperty] private bool _isWriting;

    partial void OnIsWritingChanged(bool value) => NotifyCommands();

    private bool CanWrite() => !IsWriting && !Panel.IsUploading && Panel.SelectedConnected is not null;

    private void NotifyCommands()
    {
        WriteNameCommand.NotifyCanExecuteChanged();
        WriteWifiCommand.NotifyCanExecuteChanged();
        WriteStripCommand.NotifyCanExecuteChanged();
        WriteOrderCommand.NotifyCanExecuteChanged();
        WriteMenuCommand.NotifyCanExecuteChanged();
        WriteLeaderCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteName()
    {
        string name = (Name ?? "").Trim();
        if (name.Length > MaxNameLength) { Message = string.Format(Strings.DevSet_TooLong, Strings.DevSet_Name, MaxNameLength); return; }
        await WriteAsync([DeviceSetting.Tag(name)], save: null);
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteWifi()
    {
        if (!IsPlain(Ssid, MaxSsidLength)) { Message = string.Format(Strings.DevSet_NotPlain, Strings.DevSet_Ssid, MaxSsidLength); return; }
        if (!IsPlain(Password, MaxPasswordLength)) { Message = string.Format(Strings.DevSet_NotPlain, Strings.DevSet_Password, MaxPasswordLength); return; }
        if (NetworkIndex < 0 || NetworkIndex >= Networks.Count || !IPAddress.TryParse(Networks[NetworkIndex].Address, out var address)) { Message = Strings.DevSet_NoAddress; return; }
        if (Port is < 1 or > ushort.MaxValue) { Message = string.Format(Strings.Devices_OutOfRange, 1, ushort.MaxValue); return; }
        await WriteAsync(
            [DeviceSetting.WifiNetwork(Ssid ?? ""), DeviceSetting.WifiPassword(Password ?? ""), DeviceSetting.WifiAddress(address), DeviceSetting.WifiPort((ushort)Port)],
            save: () => { _store.Ssid = Ssid ?? ""; _store.Password = Password ?? ""; _store.Interface = Networks[NetworkIndex].Name; _store.Port = Port; });
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteStrip()
    {
        if (Channel1 < 0 || Channel1 > MaxChannel1) { Message = string.Format(Strings.Devices_OutOfRange, 0, MaxChannel1); return; }
        int second = HasChannel2 ? Channel2 : 0;
        if (second < 0 || second > MaxChannel2) { Message = string.Format(Strings.Devices_OutOfRange, 0, MaxChannel2); return; }
        await WriteAsync(
            [DeviceSetting.StripLengths((ushort)Channel1, (ushort)second), DeviceSetting.LedType((ushort)(TypeIndex + 1))],
            save: () => { _store.Channel1 = Channel1; _store.Channel2 = second; _store.LedType = TypeIndex; });
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteOrder() =>
        await WriteAsync([DeviceSetting.LedOrder((byte)(OrderIndex + 1))], save: () => _store.ColourOrder = OrderIndex);

    /// <summary>The menu of a baton: PixelTuner 2 wrote the three together, with the idle shutdown it kept in its settings (30) as no box showed it.</summary>
    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteMenu() =>
        await WriteAsync(
            [DeviceSetting.FadeDownMenuAfter((ushort)MenuSeconds), DeviceSetting.ShutdownAfterIdle((ushort)_store.ShutdownAfterIdle), DeviceSetting.ExitMenuOnDoubleClick(ExitOnDoubleClick)],
            save: () => { _store.MenuSeconds = MenuSeconds; _store.ExitOnDoubleClick = ExitOnDoubleClick; });

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteLeader() => await WriteAsync([DeviceSetting.Leader(Leader)], save: null);

    private static bool IsPlain(string? text, int max) => (text ?? "").Length <= max && (text ?? "").All(c => c is >= ' ' and <= '~');

    private async Task WriteAsync(DeviceSetting[] settings, Action? save)
    {
        if (Panel.SelectedConnected is not { } connected) { Message = Strings.Devices_NoConnected; return; }
        var device = connected.Device;
        if (!DeviceSettingsCatalog.SupportFor(device.Model, device.Info).Any) { Message = string.Format(Strings.DevSet_TakesNothing, connected.Title); return; }

        IsWriting = true;
        Message = string.Format(Strings.DevSet_Writing, connected.Title);
        try
        {
            await Task.Run(() =>
            {
                device.WriteSettings(settings);
                device.ReadInfo();   // the name the device reports now
            });
            save?.Invoke();
            _store.Save();
            connected.Refresh();
            Message = string.Format(Strings.DevSet_Written, connected.Title);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException or NotSupportedException or ObjectDisposedException)
        {
            Message = string.Format(Strings.DevSet_Failed, ex.Message);
        }
        finally
        {
            IsWriting = false;
        }
    }
}
