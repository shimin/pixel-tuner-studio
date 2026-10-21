using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using SkiaSharp;

namespace PixelTuner.Core.Tests;

public class MediaSizesTests
{
    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    [Fact]
    public void Fills_the_size_of_items_that_have_none_and_leaves_known_sizes_alone()
    {
        var files = new Dictionary<string, byte[]> { ["a"] = Png(12, 36), ["b"] = Png(5, 80), ["bad"] = [1, 2, 3] };
        var line = new Timeline();
        line.Items.Add(new TimelineItem { ResourceId = "a" });
        line.Items.Add(new TimelineItem { ResourceId = "a" });
        line.Items.Add(new TimelineItem { ResourceId = "b", MediaWidth = 99, MediaHeight = 99 });
        line.Items.Add(new TimelineItem { ResourceId = "bad" });
        line.Items.Add(new TimelineItem { ResourceId = "gone" });
        line.Items.Add(new TimelineItem { Kind = ItemKind.Solid, Color = "#FFFFFF" });
        var project = new ShowProject { Timelines = [line] };

        int filled = MediaSizes.Fill(project, id => files.TryGetValue(id, out var bytes) ? new MemoryStream(bytes) : null);

        Assert.Equal(2, filled);
        Assert.Equal([(12, 36), (12, 36), (99, 99), (0, 0), (0, 0), (0, 0)], line.Items.Select(i => (i.MediaWidth, i.MediaHeight)));
    }
}
