using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PixelTuner.Devices.Tests;

public class DeviceManagerTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Serial_ports_are_probed_once_and_devices_come_and_go_with_their_port()
    {
        var ports = new List<string> { "COM3", "COM4" };
        var opened = new List<string>();
        var devices = new Dictionary<string, FakeDevice>();
        var options = new DeviceManagerOptions
        {
            ListenPort = null,
            ScanInterval = Fast,
            MonitorInterval = TimeSpan.FromHours(1),
            ListPorts = () => { lock (ports) return ports.ToList(); },
            OpenPort = port =>
            {
                lock (opened) opened.Add(port);
                // COM3 is a Pixel Poi; COM4 is something else that never answers.
                var fake = port == "COM3"
                    ? new FakeDevice("E-PIC_v6FW", FakeDevice.PixelPoi5Info(), port)
                    : new FakeDevice("", [], port) { Silent = true };
                lock (devices) devices[port] = fake;
                return fake;
            },
        };
        var found = new TaskCompletionSource<ShowDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource<ShowDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var manager = new DeviceManager(options);
        manager.DeviceFound += (_, d) => found.TrySetResult(d);
        manager.DeviceLost += (_, d) => lost.TrySetResult(d);
        manager.Start();

        var device = await found.Task.WaitAsync(Wait);
        Assert.Equal("COM3", device.ChannelId);
        Assert.Equal("Шоу 1", device.Info.Tag);
        Assert.Single(manager.Devices);

        // Several scans later: the silent port was closed and not opened again.
        await Task.Delay(Fast * 10);
        lock (opened)
        {
            Assert.Equal(1, opened.Count(p => p == "COM4"));
            Assert.Equal(1, opened.Count(p => p == "COM3"));
        }
        lock (devices) Assert.True(devices["COM4"].Closed);

        // Unplugged: the device is lost; plugged back: both ports are probed again.
        lock (ports) ports.Clear();
        Assert.Same(device, await lost.Task.WaitAsync(Wait));
        Assert.Empty(manager.Devices);
        lock (devices) Assert.True(devices["COM3"].Closed);

        lock (ports) ports.AddRange(["COM3", "COM4"]);
        await WaitUntil(() => manager.Devices.Count == 1);
        await WaitUntil(() => { lock (opened) return opened.Count(p => p == "COM4") == 2; });
    }

    [Fact]
    public async Task A_device_that_stops_answering_is_dropped()
    {
        FakeDevice? fake = null;
        var options = new DeviceManagerOptions
        {
            ListenPort = null,
            ScanInterval = Fast,
            MonitorInterval = Fast,
            ListPorts = () => ["COM9"],
            OpenPort = port => fake = new FakeDevice("LR_v3FW", FakeDevice.SampleInfo, port),
        };
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var manager = new DeviceManager(options);
        manager.DeviceLost += (_, _) => lost.TrySetResult();
        manager.Start();
        await WaitUntil(() => manager.Devices.Count == 1);

        fake!.Silent = true;
        await lost.Task.WaitAsync(Wait);
        Assert.True(fake.Closed);
    }

    [Fact]
    public async Task Devices_on_wifi_connect_over_tcp()
    {
        var options = new DeviceManagerOptions { ScanSerialPorts = false, ListenPort = 0, MonitorInterval = TimeSpan.FromMilliseconds(100) };
        var found = new TaskCompletionSource<ShowDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var manager = new DeviceManager(options);
        manager.DeviceFound += (_, d) => found.TrySetResult(d);
        manager.DeviceLost += (_, _) => lost.TrySetResult();
        manager.Start();

        // The device connects to the app, then answers requests on the same connection.
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, manager.ListeningPort!.Value);
        var serve = Task.Run(() => Serve(client.GetStream(), "PLR_v2FW", FakeDevice.SampleInfo));

        var device = await found.Task.WaitAsync(Wait);
        Assert.Equal(DeviceFamily.Video, device.Model.Family);
        Assert.Equal("spin", device.Info.Tag);
        Assert.False(device.IsCable);
        Assert.StartsWith("127.0.0.1:", device.ChannelId);

        // The upload works the same as over a cable.
        var show = Enumerable.Range(0, 600).Select(i => (byte)i).ToArray();
        await Task.Run(() => device.UploadShow(show));

        client.Close();
        await lost.Task.WaitAsync(Wait);
        var received = await serve.WaitAsync(Wait);
        Assert.Equal(show, received);
    }

    /// <summary>A device on the other end of a TCP connection; returns the show it received.</summary>
    private static List<byte> Serve(NetworkStream stream, string name, byte[] info)
    {
        var show = new List<byte>();
        var header = new byte[2];
        try
        {
            while (true)
            {
                stream.ReadExactly(header);
                var data = new byte[header[1] + 1];
                stream.ReadExactly(data);
                byte[] answer = (DeviceCommand)header[0] switch
                {
                    DeviceCommand.WhoIsIt => Encoding.ASCII.GetBytes(name),
                    DeviceCommand.Info => info,
                    DeviceCommand.WriteDataMode => [1],
                    DeviceCommand.WriteBlock => [0xff],
                    _ => [],
                };
                if (header[0] == (byte)DeviceCommand.WriteBlock) show.AddRange(data[..^1]);
                stream.Write([header[0], (byte)answer.Length, .. answer, DevicePacket.Terminator]);
            }
        }
        catch (Exception e) when (e is IOException or EndOfStreamException or ObjectDisposedException)
        {
            return show;
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("Timed out waiting for the condition.");
            await Task.Delay(Fast);
        }
    }
}
