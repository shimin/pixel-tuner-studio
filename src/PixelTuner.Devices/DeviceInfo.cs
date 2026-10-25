using System.Buffers.Binary;
using System.Text;

namespace PixelTuner.Devices;

/// <summary>
/// What a device reports to "info". Each family lays the answer out differently; fields a
/// family does not report stay null. Offsets are PixelTuner 2's.
/// </summary>
public sealed record DeviceInfo
{
    public int PlatformVersion { get; init; }
    public int BootVersion { get; init; }
    public int FirmwareVersion { get; init; }

    /// <summary>LEDs in the device (pixel devices, hoops, video modules).</summary>
    public int? LedCount { get; init; }

    /// <summary>Battery level as the device reports it.</summary>
    public int? Battery { get; init; }

    /// <summary>
    /// Memory for shows, in kilobytes. Devices report kilobytes (a video module with a 4 GB card reports 3,878,489), except the old
    /// pixel and E-pic poi of platform 2, which reported bytes. PixelTuner 2 did the same, but cut the value at 3,800,000, which
    /// is less than that card holds; there is no cut here.
    /// </summary>
    public long? MaxMemory { get; init; }

    /// <summary>The name the user gave the device (Wi-Fi settings); also what network lookup reports.</summary>
    public string? Tag { get; init; }

    /// <summary>8-byte radio address of the device (Pixel Poi, LightMaster, Illuminator).</summary>
    public byte[]? Mac { get; init; }

    /// <summary>Pixel Poi: radio address of the device it follows; LightMaster: of its server.</summary>
    public byte[]? RemoteMac { get; init; }

    public int? PanId { get; init; }
    public int? RadioChannel { get; init; }

    /// <summary>LightMaster: output channels.</summary>
    public int? Channels { get; init; }

    /// <summary>LightMaster: free memory in kilobytes.</summary>
    public long? MemoryAvailable { get; init; }

    private static readonly Encoding Cp1251 = CreateCp1251();

    private static Encoding CreateCp1251()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1251);
    }

    /// <summary>Reads the data of an "info" answer.</summary>
    /// <exception cref="DeviceProtocolException">The answer is shorter than the family's layout.</exception>
    public static DeviceInfo Parse(DeviceModel model, ReadOnlySpan<byte> data)
    {
        try
        {
            return model.Family switch
            {
                DeviceFamily.PixelPoi => Pixel(model, data),
                DeviceFamily.PixelHoop or DeviceFamily.Video => Hoop(model, data),
                DeviceFamily.EpicPoi => EpicPoi(model, data),
                DeviceFamily.LightMaster => LightMaster(data),
                DeviceFamily.Illuminator => Illuminator(data),
                _ => Versions(data),
            };
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            throw new DeviceProtocolException($"The device's info answer is too short ({data.Length} bytes).");
        }
    }

    private static DeviceInfo Versions(ReadOnlySpan<byte> d) =>
        new() { PlatformVersion = d[0], BootVersion = d[1], FirmwareVersion = d[2] };

    /// <summary>PixelDevice.GetInfo.</summary>
    private static DeviceInfo Pixel(DeviceModel model, ReadOnlySpan<byte> d)
    {
        // Pixel Poi 4 reports an older platform number.
        int platform = model.Id.Contains("E-PIC_v4", StringComparison.Ordinal) ? 4 : d[0];
        return Versions(d) with
        {
            PlatformVersion = platform,
            LedCount = BinaryPrimitives.ReadUInt16BigEndian(d[3..]),
            Battery = BinaryPrimitives.ReadUInt16BigEndian(d[5..]),
            MaxMemory = Memory(BinaryPrimitives.ReadUInt32BigEndian(d[7..]), model.Family, platform),
            Mac = d.Slice(11, 8).ToArray(),
            RemoteMac = d.Slice(22, 8).ToArray(),
            Tag = platform >= 5 ? ReadTag(d) : null,
        };
    }

    /// <summary>PixelHoop.GetInfo and VideoDevice.GetInfo: the pixel layout without radio addresses.</summary>
    private static DeviceInfo Hoop(DeviceModel model, ReadOnlySpan<byte> d) => Versions(d) with
    {
        LedCount = BinaryPrimitives.ReadUInt16BigEndian(d[3..]),
        Battery = BinaryPrimitives.ReadUInt16BigEndian(d[5..]),
        MaxMemory = Memory(BinaryPrimitives.ReadUInt32BigEndian(d[7..]), model.Family, d[0]),
        Tag = ReadTag(d),
    };

    /// <summary>SimplePoiDevice.GetInfo.</summary>
    private static DeviceInfo EpicPoi(DeviceModel model, ReadOnlySpan<byte> d) => Versions(d) with
    {
        Battery = BinaryPrimitives.ReadUInt16BigEndian(d[4..]),
        MaxMemory = Memory(BinaryPrimitives.ReadUInt32BigEndian(d[6..]), model.Family, d[0]),
    };

    /// <summary>LightMasterDevice.GetInfo. Battery and memory are little endian here.</summary>
    private static DeviceInfo LightMaster(ReadOnlySpan<byte> d) => Versions(d) with
    {
        Channels = d[4],
        Battery = BinaryPrimitives.ReadUInt16LittleEndian(d[4..]),
        MemoryAvailable = BinaryPrimitives.ReadUInt32LittleEndian(d[6..]),
        Mac = d.Slice(10, 8).ToArray(),
        PanId = BinaryPrimitives.ReadUInt16LittleEndian(d[18..]),
        RadioChannel = d[19],
        RemoteMac = d.Slice(20, 8).ToArray(),
        Tag = d[0] >= 2 ? ReadTag(d) : null,
    };

    /// <summary>IlluminatorDevice.GetInfo.</summary>
    private static DeviceInfo Illuminator(ReadOnlySpan<byte> d) => Versions(d) with
    {
        Mac = d.Slice(11, 8).ToArray(),
        PanId = BinaryPrimitives.ReadUInt16LittleEndian(d[19..]),
        RadioChannel = d[20],
    };

    private static long Memory(uint reported, DeviceFamily family, int platform) =>
        platform == 2 && family is DeviceFamily.PixelPoi or DeviceFamily.EpicPoi ? reported / 1024 : reported;

    /// <summary>12 bytes of Windows-1251 at offset 45, padded with zeros.</summary>
    private static string ReadTag(ReadOnlySpan<byte> d) => Cp1251.GetString(d.Slice(45, 12)).TrimEnd('\0');
}
