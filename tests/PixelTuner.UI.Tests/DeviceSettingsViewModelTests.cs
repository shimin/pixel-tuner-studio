using System.Text;
using PixelTuner.Devices;
using PixelTuner.Package;
using PixelTuner.UI.DevicesPanel;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public sealed class DeviceSettingsViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-devset").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakeDeviceHub _hub = new();
    private EditorViewModel? _editor;

    public void Dispose()
    {
        _editor?.Dispose();
        _doc.Dispose();
        Directory.Delete(_dir, true);
    }

    private string File_ => Path.Combine(_dir, "settings", "device-settings.txt");

    private DeviceSettingsViewModel Settings => (_editor ??= new EditorViewModel(_doc, null, null, _hub, null, File_)).DeviceSettings;

    private ShowDevice Plug(string id, byte[] info, out FakeShowTransport transport, string channel = "COM7")
    {
        _ = Settings;
        var device = FakeShowTransport.Connect(id, info, out transport, channel);
        _hub.Plug(device);
        return device;
    }

    private static byte[] PoiInfo(int platform, string tag = "left")
    {
        var info = FakeShowTransport.PoiInfo(36, 2_000_000, tag);
        info[0] = (byte)platform;
        return info;
    }

    [Fact]
    public void The_groups_shown_are_the_ones_the_selected_device_takes()
    {
        Plug("PLR_v2FW", FakeShowTransport.VideoInfo(64), out _);
        Assert.True(Settings.HasConnected);
        Assert.Equal((true, true, true, false, false, false), (Settings.HasName, Settings.HasWifi, Settings.HasStrip, Settings.HasMenu, Settings.HasLeader, Settings.TakesNothing));

        Plug("E-PIC_v5FW", PoiInfo(5), out _, "COM8");
        Settings.Panel.SelectedConnected = Settings.Panel.Connected[1];
        Assert.Equal((true, true, false, true, true, false), (Settings.HasName, Settings.HasWifi, Settings.HasStrip, Settings.HasMenu, Settings.HasLeader, Settings.TakesNothing));
        Assert.Equal("left", Settings.Name);                          // the name is the one the device reports

        Plug("E-PIC_SFW", FakeShowTransport.EpicInfo(), out _, "COM9");
        Settings.Panel.SelectedConnected = Settings.Panel.Connected[2];
        Assert.True(Settings.TakesNothing);
        Assert.False(Settings.HasName);
    }

    [Fact]
    public async Task The_name_is_written_in_Windows_1251_and_a_name_that_is_too_long_is_refused()
    {
        Plug("PLR_v2FW", FakeShowTransport.VideoInfo(64), out var transport);

        Settings.Name = "Шоу 1";
        await Settings.WriteNameCommand.ExecuteAsync(null);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Assert.Equal(Encoding.GetEncoding(1251).GetBytes("Шоу 1\0"), transport.Settings[11]);
        Assert.Equal(string.Format(Strings.DevSet_Written, "Video module"), Settings.Message);

        transport.Settings.Clear();
        Settings.Name = new string('x', DeviceSettingsCatalog.MaxNameLength + 1);
        await Settings.WriteNameCommand.ExecuteAsync(null);
        Assert.Empty(transport.Settings);
        Assert.Contains(DeviceSettingsCatalog.MaxNameLength.ToString(), Settings.Message);
    }

    [Fact]
    public async Task Wi_Fi_goes_as_the_network_the_password_this_computers_address_and_the_port_and_is_remembered()
    {
        var networks = new[] { new NetworkChoice("en0", "192.168.1.20"), new NetworkChoice("en5", "10.0.0.7") };
        _editor = new EditorViewModel(_doc, null, null, _hub, null, File_);
        var settings = new DeviceSettingsViewModel(_editor.Devices, new DeviceSettingsStore(File_), () => networks);
        _hub.Plug(FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(64), out var transport));

        settings.Ssid = "poi-net"; settings.Password = "secret"; settings.NetworkIndex = 1; settings.Port = 3331;
        await settings.WriteWifiCommand.ExecuteAsync(null);

        Assert.Equal(Encoding.ASCII.GetBytes("poi-net\0"), transport.Settings[5]);
        Assert.Equal(Encoding.ASCII.GetBytes("secret\0"), transport.Settings[6]);
        Assert.Equal(new byte[] { 10, 0, 0, 7 }, transport.Settings[7]);
        Assert.Equal(new byte[] { 0x03, 0x0D }, transport.Settings[8]);   // 3331, little endian
        Assert.False(transport.Settings.ContainsKey(11));                   // the name is its own group

        var again = new DeviceSettingsViewModel(_editor.Devices, new DeviceSettingsStore(File_), () => networks);
        Assert.Equal(("poi-net", "secret", 1, 3331), (again.Ssid, again.Password, again.NetworkIndex, again.Port));
    }

    [Fact]
    public async Task Wi_Fi_with_a_long_or_accented_text_or_no_address_is_not_sent()
    {
        _editor = new EditorViewModel(_doc, null, null, _hub, null, null);
        var none = new DeviceSettingsViewModel(_editor.Devices, new DeviceSettingsStore(), () => []);
        _hub.Plug(FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(64), out var transport));

        none.Ssid = "net"; none.Password = "pw";
        await none.WriteWifiCommand.ExecuteAsync(null);
        Assert.Equal(Strings.DevSet_NoAddress, none.Message);

        var some = new DeviceSettingsViewModel(_editor.Devices, new DeviceSettingsStore(), () => [new NetworkChoice("en0", "192.168.1.2")]);
        some.Ssid = "sé"; some.Password = "pw";
        await some.WriteWifiCommand.ExecuteAsync(null);
        some.Ssid = "net"; some.Password = new string('p', DeviceSettingsCatalog.MaxPasswordLength + 1);
        await some.WriteWifiCommand.ExecuteAsync(null);
        Assert.Empty(transport.Settings);
    }

    [Fact]
    public async Task The_strip_goes_as_the_lengths_of_the_channels_and_the_chip_and_the_order_on_its_own()
    {
        Plug("PLR_v2FW", FakeShowTransport.VideoInfo(64), out var transport);

        Settings.TypeIndex = 1; Settings.Channel1 = 300; Settings.Channel2 = 200;     // ws2801
        await Settings.WriteStripCommand.ExecuteAsync(null);
        Assert.Equal(new byte[] { 0x2C, 0x01, 0xC8, 0x00 }, transport.Settings[9]);
        Assert.Equal(new byte[] { 2, 0 }, transport.Settings[10]);                       // the second of the list
        Assert.False(transport.Settings.ContainsKey(12));

        Settings.OrderIndex = 5;                                                          // BGR
        await Settings.WriteOrderCommand.ExecuteAsync(null);
        Assert.Equal(new byte[] { 6 }, transport.Settings[12]);
    }

    [Fact]
    public async Task An_apa102_strip_is_one_channel_of_up_to_2048_and_the_limits_follow_the_chip()
    {
        Plug("PLR_v2FW", FakeShowTransport.VideoInfo(64), out var transport);
        Settings.Channel1 = 1000; Settings.Channel2 = 800;

        Settings.TypeIndex = 2;                                                           // apa102
        Assert.Equal((2048, 0, false, 1000, 0), (Settings.MaxChannel1, Settings.MaxChannel2, Settings.HasChannel2, Settings.Channel1, Settings.Channel2));
        Settings.Channel1 = 2000;
        await Settings.WriteStripCommand.ExecuteAsync(null);
        Assert.Equal(new byte[] { 0xD0, 0x07, 0x00, 0x00 }, transport.Settings[9]);
        Assert.Equal(new byte[] { 3, 0 }, transport.Settings[10]);

        Settings.TypeIndex = 0;                                                           // back to ws2812: 1024 on each
        Assert.Equal((1024, 1024, true, 1024), (Settings.MaxChannel1, Settings.MaxChannel2, Settings.HasChannel2, Settings.Channel1));
        transport.Settings.Clear();
        Settings.Channel1 = 2000;
        await Settings.WriteStripCommand.ExecuteAsync(null);
        Assert.Empty(transport.Settings);                                                 // more than the chip takes: not sent
    }

    [Fact]
    public async Task The_menu_goes_with_the_idle_shutdown_PixelTuner_2_kept_and_the_leader_flag_on_its_own()
    {
        Plug("E-PIC_v5FW", PoiInfo(5), out var transport);

        Settings.ExitOnDoubleClick = false; Settings.MenuSeconds = 12;
        Assert.Contains("12", Settings.MenuSecondsText);
        await Settings.WriteMenuCommand.ExecuteAsync(null);
        Assert.Equal(new byte[] { 12, 0 }, transport.Settings[1]);
        Assert.Equal(new byte[] { 30, 0 }, transport.Settings[2]);
        Assert.Equal(new byte[] { 0, 0 }, transport.Settings[3]);

        Settings.MenuSeconds = 0;
        Assert.Equal(Strings.DevSet_MenuNever, Settings.MenuSecondsText);

        Settings.Leader = true;
        await Settings.WriteLeaderCommand.ExecuteAsync(null);
        Assert.Equal(new byte[] { 1, 0 }, transport.Settings[0x0d]);
    }

    [Fact]
    public async Task A_device_that_takes_no_settings_gets_nothing()
    {
        Plug("E-PIC_SFW", FakeShowTransport.EpicInfo(), out var transport);
        Settings.Name = "x";
        await Settings.WriteNameCommand.ExecuteAsync(null);
        Assert.Empty(transport.Settings);
        Assert.Equal(string.Format(Strings.DevSet_TakesNothing, "E-pic poi"), Settings.Message);
    }

    [Fact]
    public void What_was_written_last_is_kept_in_a_file_and_a_damaged_one_is_ignored()
    {
        var store = new DeviceSettingsStore(File_) { Ssid = "a", Password = "b", Interface = "en0", Port = 4000, Channel1 = 10, Channel2 = 20, LedType = 3, ColourOrder = 4, ExitOnDoubleClick = false, MenuSeconds = 7 };
        store.Save();
        var back = new DeviceSettingsStore(File_);
        Assert.Equal(("a", "b", "en0", 4000, 10, 20, 3, 4, false, 7), (back.Ssid, back.Password, back.Interface, back.Port, back.Channel1, back.Channel2, back.LedType, back.ColourOrder, back.ExitOnDoubleClick, back.MenuSeconds));

        File.WriteAllText(File_, "port=not a number\nnonsense\n=\nchannel1=5");
        var damaged = new DeviceSettingsStore(File_);
        Assert.Equal((DeviceManager.NetworkPort, 5), (damaged.Port, damaged.Channel1));
    }
}
