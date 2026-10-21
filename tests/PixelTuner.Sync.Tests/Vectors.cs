using System.Text.Json;

namespace PixelTuner.Sync.Tests;

static class Vectors
{
    public static JsonElement Load(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    public static byte[] Hex(string hex) =>
        hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(h => Convert.ToByte(h, 16)).ToArray();

    public static string ToHex(byte[] bytes) => string.Join(' ', bytes.Select(b => b.ToString("X2")));
}
