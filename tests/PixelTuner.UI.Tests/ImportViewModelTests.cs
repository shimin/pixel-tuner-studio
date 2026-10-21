using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.LibraryPanel;
using SkiaSharp;

namespace PixelTuner.UI.Tests;

public sealed class ImportViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-import").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private LibraryEntry? _result;
    private int _calls;

    public void Dispose() => Directory.Delete(_dir, true);

    private string Picture(string name, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, image.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        return path;
    }

    private ImportViewModel Open(string path, int targetHeight = 0, bool fixedHeight = false) =>
        new(_doc, path, ImageDecoder.DecodeFile(path), targetHeight, fixedHeight, entry => { _result = entry; _calls++; });

    [Fact]
    public void A_picture_that_is_left_as_it_is_goes_in_as_the_file_it_is()
    {
        string path = Picture("flag.png", 40, 36, new SKColor(200, 100, 50));
        var import = Open(path);

        Assert.True(import.IsUntouched);
        import.ApplyCommand.Execute(null);

        Assert.NotNull(_result);
        Assert.Equal(("flag", LibraryKind.Image, 40, 36), (_result!.Name, _result.Kind, _result.Width, _result.Height));
        Assert.Equal(path, _result.SourcePath);
        using var stored = new MemoryStream();
        _doc.OpenMedia(_result.ResourceId)!.CopyTo(stored);
        Assert.Equal(File.ReadAllBytes(path), stored.ToArray());
        Assert.Same(_result, Assert.Single(_doc.Project.Library));
    }

    [Fact]
    public void The_crop_rectangle_stays_inside_the_picture()
    {
        var import = Open(Picture("a.png", 40, 20, new SKColor(1, 2, 3)));

        import.Cut.Width = 500;
        Assert.Equal(40, import.Cut.Width);
        import.Cut.Width = 10;
        import.Cut.X = 35;                       // 35 + 10 would stick out: pulled back
        Assert.Equal(30, import.Cut.X);
        import.Cut.Height = 0;
        Assert.Equal(1, import.Cut.Height);
        Assert.True(import.IsCropped);

        import.ResetCutCommand.Execute(null);
        Assert.False(import.IsCropped);
        Assert.Equal((0, 0, 40, 20), (import.Cut.X, import.Cut.Y, import.Cut.Width, import.Cut.Height));
    }

    [Fact]
    public void Cropping_and_scaling_to_the_height_of_the_device_gives_that_height_and_the_proportions()
    {
        var import = Open(Picture("a.png", 200, 100, new SKColor(10, 20, 30)), targetHeight: 36, fixedHeight: true);

        Assert.True(import.ScaleToHeight);   // a timeline asks for 36 LEDs: scaled from the start
        Assert.Equal((72, 36), (import.ResultWidth, import.ResultHeight));

        import.Cut.Width = 100;   // a square: 36 x 36
        Assert.Equal((36, 36), (import.ResultWidth, import.ResultHeight));
        Assert.True(import.FitsTarget);

        import.ApplyCommand.Execute(null);
        Assert.Equal((36, 36), (_result!.Width, _result.Height));
        // The stored file is the cut and scaled picture, not the original.
        var stored = ImageDecoder.Decode(_doc.OpenMedia(_result.ResourceId)!);
        Assert.Equal((36, 36), (stored.Width, stored.Height));
        Assert.Equal(new Rgb(10, 20, 30), stored[5, 5]);
    }

    [Fact]
    public void A_picture_of_the_wrong_height_cannot_be_applied_where_the_height_is_fixed()
    {
        var import = Open(Picture("a.png", 40, 80, new SKColor(1, 2, 3)), targetHeight: 36, fixedHeight: true);
        import.ScaleToHeight = false;

        Assert.False(import.FitsTarget);
        import.ApplyCommand.Execute(null);

        Assert.Equal(0, _calls);
        Assert.Empty(_doc.Project.Library);
    }

    [Fact]
    public void When_the_height_is_free_the_user_chooses_to_scale_or_not()
    {
        var import = Open(Picture("a.png", 40, 80, new SKColor(1, 2, 3)));
        Assert.False(import.ScaleToHeight);
        Assert.Equal((40, 80), (import.ResultWidth, import.ResultHeight));

        import.ScaleToHeight = true;
        import.TargetHeight = 40;
        Assert.Equal((20, 40), (import.ResultWidth, import.ResultHeight));
        import.TargetHeight = 0;
        Assert.Equal(1, import.TargetHeight);   // at least one LED
    }

    [Fact]
    public void Colour_corrections_change_the_stored_picture()
    {
        var import = Open(Picture("a.png", 8, 8, new SKColor(100, 100, 100)));

        import.Brightness = 50;
        Assert.False(import.IsUntouched);
        Assert.Equal(new Rgb(150, 150, 150), import.Result[0, 0]);

        import.ResetColourCommand.Execute(null);
        Assert.True(import.IsUntouched);
        Assert.Equal(new Rgb(100, 100, 100), import.Result[0, 0]);
    }

    [Fact]
    public void The_same_picture_twice_is_one_entry()
    {
        string path = Picture("a.png", 8, 8, new SKColor(1, 2, 3));
        Open(path).ApplyCommand.Execute(null);
        var first = _result;
        Open(path).ApplyCommand.Execute(null);

        Assert.Same(first, _result);
        Assert.Single(_doc.Project.Library);
    }

    [Fact]
    public void Giving_up_adds_nothing_and_undo_takes_the_entry_back()
    {
        var give = Open(Picture("a.png", 8, 8, new SKColor(1, 2, 3)));
        give.CancelCommand.Execute(null);
        Assert.Null(_result);
        Assert.Equal(1, _calls);
        Assert.Empty(_doc.Project.Library);

        give.ApplyCommand.Execute(null);
        Assert.Single(_doc.Project.Library);
        _doc.Undo();
        Assert.Empty(_doc.Project.Library);
    }

    // ---- the perspective step

    [Fact]
    public void The_perspective_step_is_off_until_asked_for_and_then_changes_the_picture()
    {
        var import = Open(Picture("a.png", 60, 40, new SKColor(200, 200, 200)));
        Assert.False(import.WarpEnabled);
        Assert.True(import.IsUntouched);
        Assert.Same(import.Warped, import.Warped);

        import.WarpEnabled = true;

        Assert.False(import.IsUntouched);
        // PixelTuner 2's starting values; the top row is squeezed more than the bottom one: black where the picture left.
        Assert.Equal((import.WarpTop, import.WarpMiddle, import.WarpBottom), (PerspectiveWarp.DefaultTop, PerspectiveWarp.DefaultMiddle, PerspectiveWarp.DefaultBottom));
        Assert.Equal(Rgb.Black, import.Result[3, 4]);        // top left: squeezed away
        Assert.NotEqual(Rgb.Black, import.Result[30, 4]);    // top middle: still picture
        Assert.NotEqual(Rgb.Black, import.Result[3, 36]);    // bottom left: the bottom keeps the whole width
    }

    [Fact]
    public void The_squeeze_follows_its_sliders_and_a_new_cut_starts_the_trim_over()
    {
        var import = Open(Picture("a.png", 60, 40, new SKColor(200, 200, 200)));
        import.WarpEnabled = true;
        import.WarpTop = 0.51;

        var narrow = import.Result[10, 4];
        import.WarpTop = 1;
        Assert.NotEqual(narrow, import.Result[10, 4]);   // at 1 the top row is not squeezed either

        import.Trim.Set(5, 5, 20, 10);
        Assert.Equal((20, 10), (import.ResultWidth, import.ResultHeight));

        import.Cut.Set(0, 0, 30, 40);   // another cut is another picture: the trim is the whole of it again
        Assert.True(import.Trim.IsWhole);
        Assert.Equal((30, 40), (import.Trim.ImageWidth, import.Trim.ImageHeight));
    }

    [Fact]
    public void The_trim_works_on_the_squeezed_picture_and_scaling_comes_after_it()
    {
        var import = Open(Picture("a.png", 80, 40, new SKColor(120, 60, 30)), targetHeight: 20, fixedHeight: true);
        import.WarpEnabled = true;
        import.Trim.Set(10, 0, 60, 40);   // cut the black the squeeze left at the sides

        Assert.Equal((30, 20), (import.ResultWidth, import.ResultHeight));   // 60 x 40 scaled to 20 high
        Assert.True(import.FitsTarget);
    }

    [Fact]
    public void The_steps_are_shown_one_at_a_time_and_the_trim_only_after_a_squeeze()
    {
        var import = Open(Picture("a.png", 20, 20, new SKColor(1, 2, 3)));
        Assert.True(import.IsCutStep);

        import.ShowPerspectiveCommand.Execute(null);
        Assert.True(import.IsPerspectiveStep);
        Assert.False(import.IsCutStep);

        import.ShowFinishCommand.Execute(null);
        Assert.True(import.IsFinishStep);
        Assert.True(import.IsResultStep);    // nothing was squeezed: no trim, the result itself is shown
        Assert.False(import.IsTrimStep);

        import.WarpEnabled = true;
        Assert.True(import.IsTrimStep);
        Assert.False(import.IsResultStep);
    }

    [Fact]
    public void A_squeezed_picture_goes_into_the_package_as_a_png_of_the_result()
    {
        var import = Open(Picture("a.png", 60, 40, new SKColor(200, 100, 50)));
        import.WarpEnabled = true;
        import.ApplyCommand.Execute(null);

        var stored = ImageDecoder.Decode(_doc.OpenMedia(_result!.ResourceId)!);
        Assert.Equal((import.ResultWidth, import.ResultHeight), (stored.Width, stored.Height));
        Assert.Equal(import.Result[30, 20], stored[30, 20]);
        Assert.Equal("a.png", _doc.MediaName(_result.ResourceId));
    }
}
