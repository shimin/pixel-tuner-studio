namespace PixelTuner.Devices;

/// <summary>Device families. Each reads its state differently and takes its own .bin.</summary>
public enum DeviceFamily
{
    /// <summary>Answers, but is not one PixelTuner 2 worked with (or an old Pixel Poi 2): shown, not used.</summary>
    Unsupported,

    /// <summary>Pixel Poi 3–6 and staves: the pixel .bin.</summary>
    PixelPoi,

    /// <summary>Pixel Hoop: the pixel .bin; it splits each column into its two LED chains itself.</summary>
    PixelHoop,

    /// <summary>Video module (matrices, shields, suits): the video .bin.</summary>
    Video,

    /// <summary>E-pic poi: the E-Pic .bin.</summary>
    EpicPoi,

    /// <summary>LightMaster: the light .bin.</summary>
    LightMaster,

    /// <summary>Illuminator: the radio module that relays playback commands to the props.</summary>
    Illuminator,
}

/// <summary>What a device is, from the name it reports to "who is it".</summary>
/// <param name="Id">The name as reported, for example "E-PIC_v5FW" or "LRBOOT".</param>
/// <param name="DisplayName">The name PixelTuner 2 showed, for example "Pixel Poi 5 (Boot mode)".</param>
public sealed record DeviceModel(string Id, string DisplayName, DeviceFamily Family)
{
    /// <summary>The bootloader runs instead of the firmware: only a firmware update is possible.</summary>
    public bool BootMode => Id.Contains("boot", StringComparison.OrdinalIgnoreCase);

    public bool IsSupported => Family != DeviceFamily.Unsupported;

    /// <summary>Data bytes per block of a show upload, as PixelTuner 2 sent them.</summary>
    public int ShowBlockSize => Family is DeviceFamily.PixelPoi or DeviceFamily.PixelHoop or DeviceFamily.Video
        ? DeviceProtocol.LargeBlockSize
        : DeviceProtocol.SmallBlockSize;

    /// <summary>PixelTuner 2's SupportedDevices, without reflection.</summary>
    public static DeviceModel FromId(string id)
    {
        var (name, family) = id switch
        {
            "\0\r" => ("Pixel Poi (Very First Version)", DeviceFamily.Unsupported),
            "STM32BOOT" or "STM32FW" => ("Pixel Poi 2 (Not supported)", DeviceFamily.Unsupported),
            "E-PIC_FW" => ("Pixel Poi 3", DeviceFamily.PixelPoi),
            "E-PICBOOT" => ("Pixel Poi 3 (Boot mode)", DeviceFamily.PixelPoi),
            "E-PIC_v4FW" => ("Pixel Poi 4", DeviceFamily.PixelPoi),
            "E-PIC_v4BOOT" => ("Pixel Poi 4 (Boot mode)", DeviceFamily.PixelPoi),
            "E-PIC_v5FW" => ("Pixel Poi 5", DeviceFamily.PixelPoi),
            "E-PIC_v5BOOT" => ("Pixel Poi 5 (Boot mode)", DeviceFamily.PixelPoi),
            "E-PIC_v6FW" => ("Pixel Poi 6", DeviceFamily.PixelPoi),
            "E-PIC_v6BOOT" => ("Pixel Poi 6 (Boot mode)", DeviceFamily.PixelPoi),
            "EPIC_PHOOP_v1FW" => ("Pixel Hoop", DeviceFamily.PixelHoop),
            "EPIC_PHOOP_v1BOOT" => ("Pixel Hoop (Boot mode)", DeviceFamily.PixelHoop),
            "E-PIC_SFW" => ("E-pic poi", DeviceFamily.EpicPoi),
            "E-PIC_SBOOT" or "E-PIC_SBO" => ("E-pic poi (Boot mode)", DeviceFamily.EpicPoi),
            "LRBOOT" or "LR_v3BOOT" => ("Light master (Boot mode)", DeviceFamily.LightMaster),
            "LRFW" or "LR_v3FW" => ("Light master", DeviceFamily.LightMaster),
            "ILBOOT" => ("Illuminator (Boot mode)", DeviceFamily.Illuminator),
            "ILFW" => ("Illuminator", DeviceFamily.Illuminator),
            "PLR_v1BOOT" or "PLR_v2BOOT" => ("Video module (Boot mode)", DeviceFamily.Video),
            "PLR_v1FW" or "PLR_v2FW" => ("Video module", DeviceFamily.Video),
            "UDMXBOOT" => ("uDmx master (Boot mode)", DeviceFamily.Unsupported),
            "UDMXFW" => ("uDmx master", DeviceFamily.Unsupported),
            "PP2BOOT" => ("Pixel Poi 2 (Boot mode)", DeviceFamily.Unsupported),
            "PP2FW" => ("Pixel Poi 2", DeviceFamily.Unsupported),
            _ => ("Unknown Device", DeviceFamily.Unsupported),
        };
        return new DeviceModel(id, name, family);
    }
}
