namespace PixelTuner.Devices;

/// <summary>Which groups of settings a device takes. PixelTuner 2 wrote them to every connected device of a class; here to one device, so the groups follow what that device is.</summary>
public readonly record struct DeviceSettingsSupport(bool Name, bool Wifi, bool Strip, bool Menu, bool Leader)
{
    public bool Any => Name || Wifi || Strip || Menu || Leader;
}

/// <summary>
/// The settings window of PixelTuner 2 (<c>DefaultsWindow</c>: the Wi-Fi, device and "Pixel hoop/Video Module" tabs) as data: the lists of its boxes,
/// their limits, and which device takes what. Not the "PixelTuner settings" tab, which was the app's own defaults.
/// </summary>
public static class DeviceSettingsCatalog
{
    /// <summary>LED chips, in the order of PixelTuner 2's list; the number sent is the place in it, from 1.</summary>
    public static IReadOnlyList<string> LedTypes { get; } = ["ws2812", "ws2801", "apa102", "lpd6803", "lpd8803", "dmx"];

    /// <summary>Colour orders, in the order of PixelTuner 2's list; the number sent is the place in it, from 1.</summary>
    public static IReadOnlyList<string> ColourOrders { get; } = ["RGB", "RBG", "GRB", "GBR", "BRG", "BGR"];

    /// <summary>PixelTuner 2's boxes took 10 characters for the network and the name, 14 for the password.</summary>
    public const int MaxSsidLength = 10;
    public const int MaxPasswordLength = 14;
    public const int MaxNameLength = 10;

    /// <summary>The device's menu goes off after this many seconds at most; 0 is never.</summary>
    public const int MaxMenuSeconds = 30;

    /// <summary>What PixelTuner 2 wrote as the idle shutdown whenever it wrote the menu settings (it had no box for it).</summary>
    public const int DefaultShutdownAfterIdle = 30;

    public static bool IsApa102(int typeIndex) => typeIndex >= 0 && typeIndex < LedTypes.Count && LedTypes[typeIndex] == "apa102";

    /// <summary>LEDs on one channel: an apa102 strip is one channel of up to 2048, the others two of up to 1024.</summary>
    public static int MaxLedsOnChannel(int typeIndex, int channel) =>
        IsApa102(typeIndex) ? (channel == 1 ? 2048 : 0) : 1024;

    public static DeviceSettingsSupport SupportFor(DeviceModel model, DeviceInfo info)
    {
        if (!model.IsSupported || model.BootMode) return default;
        int platform = info.PlatformVersion;
        return model.Family switch
        {
            // A Pixel Poi has Wi-Fi (and reports its name) from platform 5; the menu is the baton's; the leader flag as PixelTuner 2 set it.
            DeviceFamily.PixelPoi => new(Name: platform >= 5, Wifi: platform >= 5, Strip: false, Menu: true, Leader: platform is 5 or 6),
            DeviceFamily.PixelHoop => new(Name: true, Wifi: true, Strip: true, Menu: true, Leader: platform is 1 or 2),
            DeviceFamily.Video => new(Name: true, Wifi: true, Strip: true, Menu: false, Leader: false),
            DeviceFamily.LightMaster => new(Name: platform >= 2, Wifi: true, Strip: false, Menu: false, Leader: false),
            _ => default,
        };
    }
}
