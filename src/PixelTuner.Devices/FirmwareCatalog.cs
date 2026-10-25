using System.Reflection;

namespace PixelTuner.Devices;

/// <summary>One firmware file. PixelTuner 2 read the firmware version from the byte at offset 52.</summary>
public sealed class FirmwareImage
{
    public const int VersionOffset = 52;

    /// <summary>The biggest file taken as firmware: the biggest shipped is 70 KB, the flash of these devices is far smaller than a megabyte.</summary>
    public const int MaxLength = 512 * 1024;

    public FirmwareImage(string name, byte[] data)
    {
        Name = name;
        Data = data;
    }

    public string Name { get; }

    public byte[] Data { get; }

    public int Version => Data[VersionOffset];

    /// <summary>Whether the bytes can be firmware at all (not empty, not too big, long enough to hold a version).</summary>
    public static bool LooksValid(ReadOnlySpan<byte> data) => data.Length > VersionOffset && data.Length <= MaxLength;
}

/// <summary>
/// The firmware shipped with the app, one file per kind of device and platform, as PixelTuner 2 kept them in its resources
/// (<c>Files/Flash_*.bin</c>). A device is offered the update while its firmware is older than the file's.
/// </summary>
public static class FirmwareCatalog
{
    private static readonly Dictionary<string, FirmwareImage> Cache = [];
    private static readonly Lock Gate = new();

    /// <summary>The file for a device of the family and platform; null for a family or platform that has none (Illuminator, unknown devices).</summary>
    public static FirmwareImage? For(DeviceFamily family, int platformVersion)
    {
        string? file = (family, platformVersion) switch
        {
            (DeviceFamily.PixelPoi, >= 2 and <= 6) => $"Flash_v{platformVersion}.bin",
            (DeviceFamily.PixelHoop, _) => "Flash_Hoop.bin",
            (DeviceFamily.Video, 1) => "Flash_Video.bin",
            (DeviceFamily.Video, 2) => "Flash_Video_v2.bin",
            (DeviceFamily.EpicPoi, _) => "Flash_Epic.bin",
            (DeviceFamily.LightMaster, 2) => "Flash_LR.bin",
            (DeviceFamily.LightMaster, 3) => "Flash_LRv3.bin",
            _ => null,
        };
        return file is null ? null : Load(file);
    }

    /// <summary>The firmware for a connected device, from the platform it reports.</summary>
    public static FirmwareImage? For(DeviceModel model, DeviceInfo info) => For(model.Family, info.PlatformVersion);

    /// <summary>Every file shipped (for tests).</summary>
    public static IReadOnlyList<string> Files { get; } =
        [.. typeof(FirmwareCatalog).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).Select(n => n[Prefix.Length..]).Order()];

    private const string Prefix = "PixelTuner.Devices.Firmware.";

    private static FirmwareImage Load(string file)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(file, out var image)) return image;
            using var stream = typeof(FirmwareCatalog).Assembly.GetManifestResourceStream(Prefix + file)
                ?? throw new InvalidOperationException($"The firmware {file} is missing from the app.");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return Cache[file] = new FirmwareImage(file, bytes.ToArray());
        }
    }
}
