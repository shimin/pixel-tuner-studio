namespace PixelTuner.Devices.Tests;

public sealed class FirmwareCatalogTests
{
    private static string Legacy([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "legacy", "PixelTuner2", "PixelTuner", "Files"));

    [Fact]
    public void The_firmware_is_what_PixelTuner_2_shipped_byte_for_byte()
    {
        Assert.Equal(11, FirmwareCatalog.Files.Count);
        foreach (string file in FirmwareCatalog.Files)
        {
            var image = FirmwareFor(file);
            Assert.Equal(File.ReadAllBytes(Path.Combine(Legacy(), file)), image.Data);
            Assert.Equal(image.Data[52], image.Version);
            Assert.True(FirmwareImage.LooksValid(image.Data));
        }
    }

    private static FirmwareImage FirmwareFor(string file) => file switch
    {
        "Flash_Hoop.bin" => FirmwareCatalog.For(DeviceFamily.PixelHoop, 1)!,
        "Flash_Video.bin" => FirmwareCatalog.For(DeviceFamily.Video, 1)!,
        "Flash_Video_v2.bin" => FirmwareCatalog.For(DeviceFamily.Video, 2)!,
        "Flash_Epic.bin" => FirmwareCatalog.For(DeviceFamily.EpicPoi, 2)!,
        "Flash_LR.bin" => FirmwareCatalog.For(DeviceFamily.LightMaster, 2)!,
        "Flash_LRv3.bin" => FirmwareCatalog.For(DeviceFamily.LightMaster, 3)!,
        _ => FirmwareCatalog.For(DeviceFamily.PixelPoi, int.Parse(file[7..^4]))!,
    };

    [Theory]
    [InlineData(DeviceFamily.PixelPoi, 2, "Flash_v2.bin")]
    [InlineData(DeviceFamily.PixelPoi, 6, "Flash_v6.bin")]
    [InlineData(DeviceFamily.PixelHoop, 1, "Flash_Hoop.bin")]
    [InlineData(DeviceFamily.Video, 1, "Flash_Video.bin")]
    [InlineData(DeviceFamily.Video, 2, "Flash_Video_v2.bin")]
    [InlineData(DeviceFamily.EpicPoi, 2, "Flash_Epic.bin")]
    [InlineData(DeviceFamily.LightMaster, 2, "Flash_LR.bin")]
    [InlineData(DeviceFamily.LightMaster, 3, "Flash_LRv3.bin")]
    public void Each_kind_of_device_and_platform_gets_its_own_file(DeviceFamily family, int platform, string file) =>
        Assert.Equal(file, FirmwareCatalog.For(family, platform)!.Name);

    [Theory]
    [InlineData(DeviceFamily.PixelPoi, 1)]
    [InlineData(DeviceFamily.PixelPoi, 7)]
    [InlineData(DeviceFamily.PixelPoi, 0)]
    [InlineData(DeviceFamily.Video, 3)]
    [InlineData(DeviceFamily.LightMaster, 1)]
    [InlineData(DeviceFamily.Illuminator, 1)]
    [InlineData(DeviceFamily.Unsupported, 2)]
    public void A_device_without_a_file_for_it_gets_none(DeviceFamily family, int platform) =>
        Assert.Null(FirmwareCatalog.For(family, platform));

    [Fact]
    public void A_file_is_firmware_when_it_can_hold_a_version_and_is_not_huge()
    {
        Assert.False(FirmwareImage.LooksValid(new byte[0]));
        Assert.False(FirmwareImage.LooksValid(new byte[52]));
        Assert.True(FirmwareImage.LooksValid(new byte[53]));
        Assert.True(FirmwareImage.LooksValid(new byte[FirmwareImage.MaxLength]));
        Assert.False(FirmwareImage.LooksValid(new byte[FirmwareImage.MaxLength + 1]));
    }
}
