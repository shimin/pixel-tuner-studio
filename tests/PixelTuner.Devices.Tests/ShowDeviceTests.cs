using System.Buffers.Binary;
using System.Net;

namespace PixelTuner.Devices.Tests;

public class ShowDeviceTests
{
    [Fact]
    public void Packets_are_command_length_data_and_0x0D()
    {
        Assert.Equal([0x53, 0x00, 0x0d], DevicePacket.Encode(DeviceCommand.WhoIsIt));
        Assert.Equal([0x42, 0x03, 1, 2, 3, 0x0d], DevicePacket.Encode(DeviceCommand.WriteBlock, [1, 2, 3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => DevicePacket.Encode(DeviceCommand.WriteBlock, new byte[256]));
    }

    [Fact]
    public void Connect_identifies_the_device_and_reads_its_state()
    {
        var fake = new FakeDevice("E-PIC_v5FW", FakeDevice.PixelPoi5Info());
        using var device = ShowDevice.Connect(fake);

        Assert.Equal(new DeviceModel("E-PIC_v5FW", "Pixel Poi 5", DeviceFamily.PixelPoi), device.Model);
        Assert.False(device.Model.BootMode);
        Assert.Equal([(byte)DeviceCommand.WhoIsIt, (byte)DeviceCommand.Info], fake.Requests.Select(r => r[0]));
        var info = device.Info;
        Assert.Equal((5, 3, 17, 150, 87), (info.PlatformVersion, info.BootVersion, info.FirmwareVersion, info.LedCount, info.Battery));
        Assert.Equal(2_000_000, info.MaxMemory);   // kilobytes
        Assert.Equal("Шоу 1", info.Tag);
        Assert.Equal(new byte[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7 }, info.Mac);
        Assert.Equal(0xB0, info.RemoteMac![0]);
        Assert.Equal(DeviceProtocol.DefaultTimeout, fake.ReadTimeout);
    }

    [Fact]
    public void A_real_video_module_with_a_4_GB_card_reports_kilobytes_that_are_not_cut()
    {
        // The answer to "info" of a PLR_v2FW (firmware 2.4.2, 229 LEDs) read from a device on a cable.
        byte[] answer = Convert.FromHexString("02040200e50009003b2e596c75636b790000000000000074656869626f6f6d0000000000000000c0a858d74d01676c61737365730000000000000000");
        var info = DeviceInfo.Parse(DeviceModel.FromId("PLR_v2FW"), answer);

        Assert.Equal((2, 4, 2, 229), (info.PlatformVersion, info.BootVersion, info.FirmwareVersion, info.LedCount));
        Assert.Equal(3_878_489, info.MaxMemory);          // kilobytes: 3.7 GB, as the card is
    }

    [Fact]
    public void Info_layouts_follow_each_family()
    {
        // Video module: LEDs, memory in KB (also on platform 2, and not cut), tag always.
        var video = DeviceInfo.Parse(DeviceModel.FromId("PLR_v2FW"), FakeDevice.SampleInfo);
        Assert.Equal((2, 4, 2, 24, 0), (video.PlatformVersion, video.BootVersion, video.FirmwareVersion, video.LedCount, video.Battery));
        Assert.Equal(0x39FF98, video.MaxMemory);
        Assert.Equal("spin", video.Tag);
        Assert.Null(video.Mac);

        // Pixel Poi: radio addresses, tag only from platform 5; Pixel Poi 4 reports platform 4.
        var pixel = DeviceInfo.Parse(DeviceModel.FromId("E-PIC_FW"), FakeDevice.SampleInfo);
        Assert.Null(pixel.Tag);
        Assert.Equal("lucky"u8.ToArray(), pixel.Mac![..5]);
        Assert.Equal(4, DeviceInfo.Parse(DeviceModel.FromId("E-PIC_v4FW"), FakeDevice.SampleInfo).PlatformVersion);
        Assert.Equal(0x39FF98, DeviceInfo.Parse(DeviceModel.FromId("E-PIC_v4FW"), FakeDevice.SampleInfo).MaxMemory);   // platform 4: kilobytes

        // E-pic poi: battery and memory one byte earlier, no LEDs.
        var epic = DeviceInfo.Parse(DeviceModel.FromId("E-PIC_SFW"), FakeDevice.SampleInfo);
        Assert.Equal((0x1800, 0x39FF / 1024L), (epic.Battery!.Value, epic.MaxMemory!.Value));
        Assert.Null(epic.LedCount);

        // LightMaster: little endian battery and memory, radio settings, tag from platform 2.
        var light = DeviceInfo.Parse(DeviceModel.FromId("LR_v3FW"), FakeDevice.SampleInfo);
        Assert.Equal(0x0018, light.Battery);
        Assert.Equal(0xFF390000u, light.MemoryAvailable);
        Assert.Equal(0x18, light.Channels);
        Assert.Equal("spin", light.Tag);
        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(FakeDevice.SampleInfo.AsSpan(18)), light.PanId);

        // Illuminator: radio address, network and channel.
        var illuminator = DeviceInfo.Parse(DeviceModel.FromId("ILFW"), FakeDevice.SampleInfo);
        Assert.Equal(FakeDevice.SampleInfo[20], illuminator.RadioChannel);

        Assert.Throws<DeviceProtocolException>(() => DeviceInfo.Parse(DeviceModel.FromId("PLR_v2FW"), new byte[20]));
    }

    [Theory]
    [InlineData("E-PIC_v6FW", DeviceProtocol.LargeBlockSize)]
    [InlineData("EPIC_PHOOP_v1FW", DeviceProtocol.LargeBlockSize)]
    [InlineData("PLR_v2FW", DeviceProtocol.LargeBlockSize)]
    [InlineData("E-PIC_SFW", DeviceProtocol.SmallBlockSize)]
    [InlineData("LR_v3FW", DeviceProtocol.SmallBlockSize)]
    public void Show_upload_announces_the_size_sends_blocks_and_closes(string id, int blockSize)
    {
        var fake = new FakeDevice(id, FakeDevice.SampleInfo);
        using var device = ShowDevice.Connect(fake);
        var show = Enumerable.Range(0, 1000).Select(i => (byte)(i * 7)).ToArray();
        var progress = new List<double>();

        device.UploadShow(show, new SyncProgress(progress.Add));

        Assert.Equal(show.Length, fake.AnnouncedSize);
        Assert.Equal(show, fake.Show);
        Assert.True(fake.ShowClosed);
        var blocks = fake.Requests.Where(r => r[0] == (byte)DeviceCommand.WriteBlock).Select(r => r[1]).ToList();
        Assert.All(blocks[..^1], b => Assert.Equal(blockSize, b));
        Assert.Equal((show.Length - 1) % blockSize + 1, blocks[^1]);
        Assert.Equal(1.0, progress[^1]);
        Assert.All(fake.TimeoutsDuringUpload, t => Assert.Equal(DeviceProtocol.UploadTimeout, t));
        Assert.Equal(DeviceProtocol.DefaultTimeout, fake.ReadTimeout);
        Assert.False(device.IsBusy);
    }

    [Fact]
    public void An_answer_is_read_in_one_call_not_byte_by_byte_because_each_call_costs_a_millisecond_on_a_serial_port()
    {
        var fake = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo);
        using var device = ShowDevice.Connect(fake);
        int before = fake.Reads;

        device.ReadInfo();
        Assert.Equal(1, fake.Reads - before);

        before = fake.Reads;
        device.UploadShow(new byte[2500]);                          // 10 blocks of 250, the announcement and the end
        Assert.Equal(10 + 2, fake.Reads - before);
    }

    [Fact]
    public void An_answer_that_comes_in_pieces_is_put_together_and_one_without_the_closing_byte_is_still_taken()
    {
        var split = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo) { ChunkSize = 7 };
        using (var device = ShowDevice.Connect(split))
            Assert.Equal(2, device.ReadInfo().PlatformVersion);

        var bare = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo) { NoTerminator = true };
        using (var device = ShowDevice.Connect(bare))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            device.UploadShow(new byte[500]);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1));     // a missing 0x0D costs a few milliseconds, not the timeout
        }
    }

    [Fact]
    public void Upload_errors_are_reported()
    {
        var full = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo) { AcceptShow = 0 };
        using (var device = ShowDevice.Connect(full))
            Assert.Throws<DeviceProtocolException>(() => device.UploadShow(new byte[100]));

        var broken = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo) { RejectBlock = 1 };
        using (var device = ShowDevice.Connect(broken))
        {
            var error = Assert.Throws<DeviceProtocolException>(() => device.UploadShow(new byte[600]));
            Assert.Contains("block", error.Message);
            Assert.False(device.IsBusy);
        }

        var boot = new FakeDevice("E-PIC_v5BOOT", FakeDevice.SampleInfo);
        using (var device = ShowDevice.Connect(boot))
            Assert.Throws<InvalidOperationException>(() => device.UploadShow(new byte[10]));

        var cancelled = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo);
        using (var device = ShowDevice.Connect(cancelled))
            Assert.Throws<OperationCanceledException>(() => device.UploadShow(new byte[600], cancel: new CancellationToken(true)));
    }

    [Fact]
    public void Firmware_goes_in_small_blocks_and_the_last_packet_is_not_answered()
    {
        var fake = new FakeDevice("E-PIC_v5BOOT", FakeDevice.SampleInfo);
        using var device = ShowDevice.Connect(fake);
        var firmware = Enumerable.Range(0, 130).Select(i => (byte)i).ToArray();

        device.UploadFirmware(firmware);

        Assert.Equal(firmware, fake.Firmware);
        Assert.True(fake.FirmwareClosed);
        Assert.Equal([60, 60, 10], fake.Requests.Where(r => r[0] == (byte)DeviceCommand.WriteBlock).Select(r => (int)r[1]));
        Assert.Equal((byte)DeviceCommand.EndWriteFlash, fake.Requests[^1][0]);
    }

    [Fact]
    public void Settings_are_batched_as_PixelTuner_2_did()
    {
        var fake = new FakeDevice("PLR_v2FW", FakeDevice.SampleInfo);
        using var device = ShowDevice.Connect(fake);

        device.WriteSettings(
        [
            DeviceSetting.WifiNetwork("stage-network-5g"),
            DeviceSetting.WifiPassword("a-long-password-1234"),
            DeviceSetting.WifiAddress(IPAddress.Parse("192.168.1.10")),
            DeviceSetting.WifiPort(333),
            DeviceSetting.Tag("Шоу"),
        ]);

        // 4 + 17, 4 + 21, 4 + 4 = 54 < 60, + 4 + 2 = 60 → first packet; the tag goes alone.
        Assert.Equal(2, fake.Settings.Count);
        Assert.Equal(60, fake.Settings[0].Length);
        Assert.Equal([5, 0, 17, 0], fake.Settings[0][..4]);
        Assert.Equal([11, 0, 4, 0, 0xD8, 0xEE, 0xF3, 0], fake.Settings[1]);
        Assert.Equal([0x0d, 0, 2, 0, 1, 0], DeviceSetting.Leader(true).Encode());
        Assert.Equal([9, 0, 4, 0, 0x2c, 0x01, 0x68, 0x01], DeviceSetting.StripLengths(300, 360).Encode());
    }

    [Fact]
    public void Answers_are_checked()
    {
        var wrong = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo);
        using (var device = ShowDevice.Connect(wrong))
        {
            wrong.WrongAnswer = 0x49;
            var error = Assert.Throws<DeviceProtocolException>(() => device.WriteSettings([DeviceSetting.WifiPort(1)]));
            Assert.Contains("Wrong answer 49 on request 73", error.Message);
        }

        // The closing 0x0D is optional, as in PixelTuner 2.
        var loose = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo) { NoTerminator = true };
        using (var device = ShowDevice.Connect(loose))
            Assert.Equal(24, device.Info.LedCount);

        var silent = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo) { Silent = true };
        Assert.Throws<TimeoutException>(() => ShowDevice.Connect(silent));
    }

    [Fact]
    public void Unknown_devices_are_listed_but_not_used()
    {
        var fake = new FakeDevice("STM32FW", [1, 2, 3]);
        using var device = ShowDevice.Connect(fake);
        Assert.Equal("Pixel Poi 2 (Not supported)", device.Model.DisplayName);
        Assert.False(device.Model.IsSupported);
        Assert.Equal(3, device.Info.FirmwareVersion);
        Assert.Throws<NotSupportedException>(() => device.UploadShow(new byte[1]));
        Assert.Equal("Unknown Device", DeviceModel.FromId("XYZ").DisplayName);
    }

    [Fact]
    public void Illuminator_relays_playback_commands()
    {
        var fake = new FakeDevice("ILFW", FakeDevice.SampleInfo);
        using var device = ShowDevice.Connect(fake);
        Assert.True(device.SendPlayback(RadioPlayback.Play, TimeSpan.FromMilliseconds(1000)));
        Assert.Equal([0x50, 5, 2, 0xE8, 0x03, 0, 0, 0x0d], fake.Requests[^1]);
        Assert.Throws<InvalidOperationException>(() => device.UploadShow(new byte[1]));
    }

    [Fact]
    public void Switch_to_boot_does_not_wait_for_an_answer_that_never_comes()
    {
        var fake = new FakeDevice("E-PIC_v5FW", FakeDevice.SampleInfo);
        using var device = ShowDevice.Connect(fake);
        device.SwitchToBoot();
        Assert.Equal((byte)DeviceCommand.SwitchToBoot, fake.Requests[^1][0]);
    }

    /// <summary>Progress&lt;T&gt; posts to the thread pool; tests need the reports in order.</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
