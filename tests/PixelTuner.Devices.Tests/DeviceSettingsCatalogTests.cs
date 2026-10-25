namespace PixelTuner.Devices.Tests;

public sealed class DeviceSettingsCatalogTests
{
    private static DeviceSettingsSupport Support(string id, int platform) =>
        DeviceSettingsCatalog.SupportFor(DeviceModel.FromId(id), new DeviceInfo { PlatformVersion = platform });

    [Fact]
    public void The_lists_are_PixelTuner_2_s_in_its_order()
    {
        Assert.Equal(["ws2812", "ws2801", "apa102", "lpd6803", "lpd8803", "dmx"], DeviceSettingsCatalog.LedTypes);
        Assert.Equal(["RGB", "RBG", "GRB", "GBR", "BRG", "BGR"], DeviceSettingsCatalog.ColourOrders);
    }

    [Fact]
    public void An_apa102_strip_is_one_channel_of_2048_the_others_two_of_1024()
    {
        Assert.Equal((2048, 0), (DeviceSettingsCatalog.MaxLedsOnChannel(2, 1), DeviceSettingsCatalog.MaxLedsOnChannel(2, 2)));
        Assert.Equal((1024, 1024), (DeviceSettingsCatalog.MaxLedsOnChannel(0, 1), DeviceSettingsCatalog.MaxLedsOnChannel(0, 2)));
    }

    [Fact]
    public void Each_device_takes_the_groups_PixelTuner_2_wrote_to_its_class()
    {
        Assert.Equal(new DeviceSettingsSupport(true, true, true, false, false), Support("PLR_v2FW", 2));      // video module
        Assert.Equal(new DeviceSettingsSupport(true, true, true, true, true), Support("EPIC_PHOOP_v1FW", 1));  // hoop
        Assert.Equal(new DeviceSettingsSupport(true, true, false, true, true), Support("E-PIC_v5FW", 5));      // poi 5
        Assert.Equal(new DeviceSettingsSupport(false, false, false, true, false), Support("E-PIC_v4FW", 4));   // older poi: no Wi-Fi, no name
        Assert.Equal(new DeviceSettingsSupport(true, true, false, false, false), Support("LR_v3FW", 3));
        Assert.False(Support("E-PIC_SFW", 2).Any);                                                              // E-pic poi
    }

    [Fact]
    public void A_device_in_its_bootloader_or_one_that_is_not_supported_takes_no_settings()
    {
        Assert.False(Support("PLR_v2BOOT", 2).Any);
        Assert.False(Support("UDMXFW", 1).Any);
        Assert.False(Support("ILFW", 1).Any);
    }
}
