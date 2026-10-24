using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.UI.Timelines;
using SkiaSharp;

namespace PixelTuner.UI.Tests;

/// <summary>What a lane draws of an item: the picture repeated over the item as the device repeats it, and the pauses between items.</summary>
public sealed class ItemThumbnailsTests
{
    private readonly Dictionary<string, byte[]> _files = [];

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private void Png(string name, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        _files[name] = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private ItemThumbnails Thumbnails() => new(id => _files.TryGetValue(id, out var bytes) ? new MemoryStream(bytes) : null);

    [Fact]
    public void A_picture_thumbnail_is_the_picture_as_the_device_shows_it_with_brightness_and_flip()
    {
        Png("a", 4, 3, new SKColor(200, 100, 50));
        var item = new TimelineItem { Kind = ItemKind.Image, ResourceId = "a", Brightness = 50, Stretching = 4 };
        var timeline = new Timeline { FlipVertical = true, Items = [item] };

        var thumb = Thumbnails().Get(item, timeline)!;

        Assert.Equal(4, thumb.Columns);
        Assert.Equal(new Rgb(100, 50, 25), thumb.Image[0, 0]);   // half brightness
        Assert.Equal(new Rgb(100, 50, 25), thumb.Average);
        // One pass: 4 columns held for 4 x 50 µs each.
        Assert.Equal(TimeSpan.FromTicks(4 * 4 * 500), thumb.Pass(item));
    }

    [Fact]
    public void The_pass_of_each_kind_uses_the_unit_of_its_device()
    {
        Png("g", 10, 1, new SKColor(1, 2, 3));
        var gradient = new TimelineItem { Kind = ItemKind.Gradient, ResourceId = "g", Stretching = 5 };
        var thumb = Thumbnails().Get(gradient, new Timeline { Kind = TimelineKind.EpicPoi })!;

        Assert.Equal(1, thumb.Image.Height);   // an E-Pic plays the top row only
        Assert.Equal(ItemPreview.EpicPassDuration(10, 5), thumb.Pass(gradient));
    }

    [Fact]
    public void A_video_thumbnail_is_the_first_frame_and_a_pass_is_frames_times_the_frame_time()
    {
        var gif = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.gif"));
        _files["gif"] = gif;
        int frames = GifFrames.Decode(gif).Count;
        var item = new TimelineItem { Kind = ItemKind.Video, ResourceId = "gif", Stretching = 40 };

        var thumb = Thumbnails().Get(item, new Timeline { Kind = TimelineKind.Video })!;

        var first = GifFrames.Decode(gif)[0];
        Assert.Equal((first.Width, first.Height), (thumb.Image.Width, thumb.Image.Height));   // the picture is the first frame
        Assert.Equal(first[0, 0], thumb.Image[0, 0]);
        Assert.Equal(frames, thumb.Columns);
        Assert.Equal(S(frames * 0.040), thumb.Pass(item));
    }

    [Fact]
    public void A_video_keeps_a_few_frames_from_the_whole_animation_for_its_film_strip_and_brightness_applies_to_them()
    {
        var gif = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.gif"));
        _files["gif"] = gif;
        var all = GifFrames.Decode(gif);
        var item = new TimelineItem { Kind = ItemKind.Video, ResourceId = "gif", Stretching = 40, Brightness = 100 };
        var timeline = new Timeline { Kind = TimelineKind.Video };

        var strip = Thumbnails().Get(item, timeline)!.Strip!;

        Assert.Equal(Math.Min(all.Count, ItemThumbnails.MaxStripFrames), strip.Count);
        Assert.Equal(all[0][0, 0], strip[0][0, 0]);                                      // the strip begins with the first frame
        Assert.All(strip, f => Assert.True(f.Height <= ItemThumbnails.StripHeight));      // small enough for a lane

        // A frame in the middle comes from the middle of the animation: the strip's frames are evenly spaced.
        int middle = strip.Count / 2;
        var source = all[(int)((long)middle * all.Count / strip.Count)];
        Assert.Equal(source[source.Width / 2, source.Height / 2], strip[middle][source.Width / 2, source.Height / 2]);

        item.Brightness = 50;
        var dim = Thumbnails().Get(item, timeline)!.Strip!;
        var c = strip[0][strip[0].Width / 2, strip[0].Height / 2];
        var d = dim[0][strip[0].Width / 2, strip[0].Height / 2];
        Assert.True(d.R <= c.R / 2 + 1 && d.G <= c.G / 2 + 1 && d.B <= c.B / 2 + 1);
    }

    [Fact]
    public void Pictures_and_gradients_have_no_strip()
    {
        Png("a", 4, 3, new SKColor(1, 2, 3));
        Assert.Null(Thumbnails().Get(new TimelineItem { Kind = ItemKind.Image, ResourceId = "a" }, new Timeline())!.Strip);
    }

    [Fact]
    public void A_missing_or_damaged_file_gives_no_thumbnail_and_the_lane_draws_a_plain_block()
    {
        _files["bad"] = [1, 2, 3];
        var thumbnails = Thumbnails();
        var timeline = new Timeline();

        Assert.Null(thumbnails.Get(new TimelineItem { ResourceId = "gone" }, timeline));
        Assert.Null(thumbnails.Get(new TimelineItem { ResourceId = "bad" }, timeline));
        Assert.Null(thumbnails.Get(new TimelineItem { ResourceId = null }, timeline));
        Assert.Null(thumbnails.Get(new TimelineItem { Kind = ItemKind.Solid, Color = "#FFFFFF" }, timeline));
    }

    [Fact]
    public void A_picture_is_made_once_per_brightness_and_flip()
    {
        Png("a", 2, 2, new SKColor(9, 9, 9));
        var thumbnails = Thumbnails();
        var timeline = new Timeline();
        var one = new TimelineItem { ResourceId = "a", Brightness = 100 };
        var two = new TimelineItem { ResourceId = "a", Brightness = 100, Stretching = 50 };   // stretching is a speed: same picture
        var dim = new TimelineItem { ResourceId = "a", Brightness = 30 };

        Assert.Same(thumbnails.Get(one, timeline), thumbnails.Get(two, timeline));
        Assert.NotSame(thumbnails.Get(one, timeline), thumbnails.Get(dim, timeline));
    }
}
