using System.Globalization;
using PixelTuner.Devices;

namespace PixelTuner.UI.Services;

/// <summary>
/// What was last written to the devices (PixelTuner 2 kept it in its user settings: the Wi-Fi network and password, the strip, the menu), so the boxes of the
/// settings tab start with it. Devices do not report these, only their name. A text file of <c>key=value</c> lines in the user's application data (no serializer,
/// no platform code); without a file the values live in memory (tests, phones). The password is kept as typed, as PixelTuner 2 kept it.
/// </summary>
public sealed class DeviceSettingsStore
{
    private readonly string? _file;

    public DeviceSettingsStore(string? file = null)
    {
        _file = file;
        Load();
    }

    public static string DefaultFile(string appName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "device-settings.txt");

    public string Ssid { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>The network interface the address is taken from, by its name; empty is the first.</summary>
    public string Interface { get; set; } = "";

    public int Port { get; set; } = DeviceManager.NetworkPort;
    public int Channel1 { get; set; } = 255;
    public int Channel2 { get; set; }
    public int LedType { get; set; }

    /// <summary>The place in <see cref="DeviceSettingsCatalog.ColourOrders"/>; PixelTuner 2's box began on GRB.</summary>
    public int ColourOrder { get; set; } = 2;

    public bool ExitOnDoubleClick { get; set; } = true;
    public int MenuSeconds { get; set; }
    public int ShutdownAfterIdle { get; set; } = DeviceSettingsCatalog.DefaultShutdownAfterIdle;

    public void Save()
    {
        if (_file is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllLines(_file,
            [
                $"ssid={Ssid}", $"password={Password}", $"interface={Interface}", $"port={Port}",
                $"channel1={Channel1}", $"channel2={Channel2}", $"ledtype={LedType}", $"colourorder={ColourOrder}",
                $"exitondoubleclick={(ExitOnDoubleClick ? 1 : 0)}", $"menuseconds={MenuSeconds}", $"shutdownafteridle={ShutdownAfterIdle}",
            ]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The values are only a convenience; losing them costs the next typing.
        }
    }

    private void Load()
    {
        if (_file is null || !File.Exists(_file)) return;
        try
        {
            foreach (string line in File.ReadAllLines(_file))
            {
                int at = line.IndexOf('=');
                if (at <= 0) continue;
                string key = line[..at], value = line[(at + 1)..];
                switch (key)
                {
                    case "ssid": Ssid = value; break;
                    case "password": Password = value; break;
                    case "interface": Interface = value; break;
                    case "port": Port = Number(value, Port); break;
                    case "channel1": Channel1 = Number(value, Channel1); break;
                    case "channel2": Channel2 = Number(value, Channel2); break;
                    case "ledtype": LedType = Number(value, LedType); break;
                    case "colourorder": ColourOrder = Number(value, ColourOrder); break;
                    case "exitondoubleclick": ExitOnDoubleClick = Number(value, ExitOnDoubleClick ? 1 : 0) != 0; break;
                    case "menuseconds": MenuSeconds = Number(value, MenuSeconds); break;
                    case "shutdownafteridle": ShutdownAfterIdle = Number(value, ShutdownAfterIdle); break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No file, no memory: the defaults.
        }
    }

    private static int Number(string text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;
}
