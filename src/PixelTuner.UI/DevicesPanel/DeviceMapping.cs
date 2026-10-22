using PixelTuner.Core.Model;
using PixelTuner.Devices;

namespace PixelTuner.UI.DevicesPanel;

/// <summary>How a device that answered on a cable or Wi-Fi is told from the kinds of device a project builds files for.</summary>
public static class DeviceMapping
{
    /// <summary>The kind of device a project device of this kind is when connected; null for devices that play no shows.</summary>
    public static string? KindOf(DeviceFamily family) => family switch
    {
        DeviceFamily.PixelPoi => DeviceKind.Poi,
        DeviceFamily.PixelHoop => DeviceKind.Hoop,
        DeviceFamily.Video => DeviceKind.Video,
        DeviceFamily.EpicPoi => DeviceKind.EpicPoi,
        DeviceFamily.LightMaster => DeviceKind.Light,
        _ => null,
    };

    /// <summary>Whether a connected device can play the file built for a kind of project device (a video module also plays a fan's file).</summary>
    public static bool Plays(DeviceFamily family, string kind) => family switch
    {
        DeviceFamily.PixelPoi => kind == DeviceKind.Poi,
        DeviceFamily.PixelHoop => kind == DeviceKind.Hoop,
        DeviceFamily.Video => kind is DeviceKind.Video or DeviceKind.Fan,
        DeviceFamily.EpicPoi => kind == DeviceKind.EpicPoi,
        DeviceFamily.LightMaster => kind == DeviceKind.Light,
        _ => false,
    };

    /// <summary>
    /// What the device can hold, in bytes; 0 when it does not say. PixelTuner 2 read it as the device reported it (capped at 3,800,000,
    /// in kilobytes on platform 2, see <see cref="DeviceInfo.MaxMemory"/>); the LightMaster reports kilobytes that are free.
    /// </summary>
    // TODO(release): the units of the reported memory are unchecked on devices (docs/before-release.md).
    public static long MemoryBytes(DeviceModel model, DeviceInfo info) => model.Family switch
    {
        DeviceFamily.LightMaster => (info.MemoryAvailable ?? 0) * 1024,
        _ when info.PlatformVersion == 2 => (info.MaxMemory ?? 0) * 1024,
        _ => info.MaxMemory ?? 0,
    };
}
