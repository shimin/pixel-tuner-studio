using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using PixelTuner.Devices;
using PixelTuner.Package;
using PixelTuner.UI.DevicesPanel;
using PixelTuner.UI.Resources;
using PixelTuner.UI.ViewModels;
using SkiaSharp;

namespace PixelTuner.UI.Tests;

public sealed class DevicesPanelViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-devices").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakeDeviceHub _hub = new();
    private readonly FakePicker _picker = new();
    private readonly EditorViewModel _editor;
    private DevicesPanelViewModel Panel => _editor.Devices;

    public DevicesPanelViewModelTests() => _editor = new EditorViewModel(_doc, null, () => _picker, _hub);

    public void Dispose()
    {
        _editor.Dispose();
        _doc.Dispose();
        Directory.Delete(_dir, true);
    }

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private int _shade;

    private string Picture(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor((byte)(20 + 10 * ++_shade), 100, 50));
        using var image = SKImage.FromBitmap(bitmap);
        return _doc.AddMedia(image.Encode(SKEncodedImageFormat.Png, 100).ToArray(), $"p{_shade}.png");
    }

    private Timeline PixelTimeline(string name, int height, double start = 0, double length = 1, int width = 8)
    {
        var timeline = new Timeline { Kind = TimelineKind.Pixel, Name = name };
        _doc.Execute(Edits.AddTimeline(_doc.Project, timeline));
        _doc.Execute(Edits.AddItem(timeline, new TimelineItem
        {
            Kind = ItemKind.Image, ResourceId = Picture(width, height), SourcePath = "p.png", MediaWidth = width, MediaHeight = height,
            Start = S(start), Duration = S(length), Stretching = 4,
        }));
        return timeline;
    }

    private DeviceRowViewModel AddPoi()
    {
        Panel.AddDeviceCommand.Execute(DeviceKind.Poi);
        return Panel.Selected!;
    }

    // ---- build settings

    [Fact]
    public void The_build_settings_show_the_project_s_and_change_it_with_undo()
    {
        Assert.True(Panel.HasDevices);
        Assert.Equal((1, 4, 5), (Panel.DepthIndex, Panel.GapStretching, Panel.LightRate));

        Panel.DepthIndex = 0;
        Panel.GapStretching = 10;
        Panel.LightRate = 20;
        Assert.Equal((ColorDepth.Bit12, 10, 20), (_doc.Project.Build.Depth, _doc.Project.Build.GapStretching, _doc.Project.Build.LightRate));

        _doc.Undo();
        _doc.Undo();
        _doc.Undo();
        Assert.Equal((1, 4, 5), (Panel.DepthIndex, Panel.GapStretching, Panel.LightRate));
    }

    [Fact]
    public void A_setting_out_of_range_is_told_and_leaves_the_project_alone()
    {
        Panel.GapStretching = 1;                           // below the least stretching
        Assert.True(Panel.HasMessage);
        Assert.Equal(4, _doc.Project.Build.GapStretching);
        Panel.LightRate = 0;
        Assert.Equal(5, _doc.Project.Build.LightRate);
        Assert.False(_doc.IsDirty);
    }

    // ---- the devices of the project

    [Fact]
    public void A_new_device_plays_the_timelines_of_its_kind_that_have_something_on_them()
    {
        var a = PixelTimeline("A", 36);
        var b = PixelTimeline("B", 36);
        _doc.Execute(Edits.AddTimeline(_doc.Project, new Timeline { Kind = TimelineKind.Pixel, Name = "empty" }));
        _doc.Execute(Edits.AddTimeline(_doc.Project, new Timeline { Kind = TimelineKind.Light, Name = "light" }));

        var row = AddPoi();
        Assert.Same(row, Panel.Selected);
        Assert.Equal([a.Id, b.Id], row.Binding.TimelineIds);
        Assert.Equal(3, row.Choices.Count);                        // the pixel timelines, also the empty one
        Assert.Equal(["1", "2", ""], row.Choices.Select(c => c.Order));
        Assert.True(row.CanBuild);
        Assert.Contains("KB", row.Summary);

        _doc.Undo();
        Assert.Empty(Panel.Rows);
        Assert.False(Panel.HasSelected);
    }

    [Fact]
    public void Ticking_a_timeline_adds_it_at_the_end_unticking_takes_it_out_and_every_step_undoes()
    {
        var a = PixelTimeline("A", 36);
        var b = PixelTimeline("B", 36);
        var row = AddPoi();

        row.Choices[0].IsChecked = false;
        Assert.Equal([b.Id], row.Binding.TimelineIds);
        row.Choices[0].IsChecked = true;
        Assert.Equal([b.Id, a.Id], row.Binding.TimelineIds);
        Assert.Equal("2", row.Choices[0].Order);

        row.Choices[0].IsChecked = false;
        row.Choices[1].IsChecked = false;
        Assert.Empty(row.Binding.TimelineIds);
        Assert.False(row.CanBuild);
        Assert.NotEmpty(row.IssueLines);
    }

    [Fact]
    public void The_order_of_the_timelines_is_the_order_of_the_file_and_is_changed_with_the_arrows()
    {
        var a = PixelTimeline("A", 36);
        var b = PixelTimeline("B", 36);
        var row = AddPoi();

        row.MoveDownCommand.Execute(row.Choices[0]);         // the choices stay in the order of the project, the numbers are the file's
        Assert.Equal([b.Id, a.Id], row.Binding.TimelineIds);
        Assert.Equal(["2", "1"], row.Choices.Select(c => c.Order));
        row.MoveUpCommand.Execute(row.Choices[1]);           // already first: nothing happens
        Assert.Equal([b.Id, a.Id], row.Binding.TimelineIds);
        row.MoveUpCommand.Execute(row.Choices[0]);
        Assert.Equal([a.Id, b.Id], row.Binding.TimelineIds);

        _doc.Undo();
        Assert.Equal([b.Id, a.Id], row.Binding.TimelineIds);
    }

    [Fact]
    public void A_poi_plays_only_timelines_as_high_as_it_has_leds_and_the_others_cannot_be_ticked()
    {
        var tall = PixelTimeline("tall", 36);
        var small = PixelTimeline("small", 24);
        var row = AddPoi();                                         // LedCount 0: both play

        row.LedCount = 24;                                          // refused: the 36 high one is played
        Assert.True(Panel.HasMessage);
        Assert.Equal(0, row.LedCount);

        row.Choices[0].IsChecked = false;
        row.LedCount = 24;
        Assert.Equal(24, row.LedCount);
        Assert.False(row.Choices[0].Fits);
        Assert.False(row.Choices[0].CanCheck);
        Assert.Contains("24", row.Choices[0].Detail);
        Assert.True(row.Choices[1].CanCheck);
        Assert.Equal([small.Id], row.Binding.TimelineIds);
        _ = tall;
    }

    [Fact]
    public void Another_kind_of_device_keeps_what_it_can_play_and_the_memory_is_checked_against_the_file()
    {
        PixelTimeline("A", 36, width: 400);
        var row = AddPoi();
        Assert.True(row.HasLedCount);

        row.KindIndex = 1;                                           // hoop
        Assert.Equal(DeviceKind.Hoop, row.Binding.DeviceType);
        Assert.Single(row.Binding.TimelineIds);

        row.MemoryKb = 1;                                            // a file of more than a kilobyte does not fit
        Assert.False(row.CanBuild);
        Assert.Contains(row.Result.Errors, e => e.Code == BuildIssueCode.TooBig);
        Assert.Contains("does not fit", row.Summary);

        row.MemoryKb = 4000;
        Assert.True(row.CanBuild);
        Assert.Contains(" of ", row.Summary);

        row.KindIndex = 3;                                           // video module: pixel timelines are not its
        Assert.Empty(row.Binding.TimelineIds);
        Assert.False(row.HasLedCount);
    }

    [Fact]
    public void The_name_and_the_name_on_the_network_are_edits_of_the_project()
    {
        PixelTimeline("A", 36);
        var row = AddPoi();
        row.Name = "Left poi";
        row.Tag = " LEFT ";
        Assert.Equal(("Left poi", "LEFT"), (row.Binding.Name, row.Binding.NetworkTag));
        Assert.Equal("Left poi", row.Title);
        _doc.Undo();
        _doc.Undo();
        Assert.Null(row.Binding.NetworkTag);
    }

    [Fact]
    public void Removing_a_device_is_one_undo_step()
    {
        PixelTimeline("A", 36);
        AddPoi();
        Panel.RemoveDeviceCommand.Execute(null);
        Assert.Empty(_doc.Project.Devices);
        _doc.Undo();
        Assert.Single(Panel.Rows);
    }

    // ---- the file

    [Fact]
    public async Task Export_writes_the_file_the_build_makes_where_the_user_says()
    {
        PixelTimeline("A", 36);
        var row = AddPoi();
        row.Name = "Left poi";
        string path = Path.Combine(_dir, "left.bin");
        _picker.BinPath = path;

        await Panel.ExportBinCommand.ExecuteAsync(null);

        Assert.Equal("Left poi.bin", _picker.SuggestedBinName);
        Assert.Equal(row.Result.Bin, await File.ReadAllBytesAsync(path));
        Assert.True(BinReader.IsPpoi(await File.ReadAllBytesAsync(path)));
        Assert.Contains("left.bin", Panel.Message);
    }

    [Fact]
    public async Task A_cancelled_export_writes_nothing_and_one_the_build_refuses_says_why()
    {
        PixelTimeline("A", 36);
        var row = AddPoi();
        _picker.BinPath = null;
        await Panel.ExportBinCommand.ExecuteAsync(null);
        Assert.Empty(Directory.GetFiles(_dir));

        row.Choices[0].IsChecked = false;                           // nothing to play
        _picker.BinPath = Path.Combine(_dir, "x.bin");
        await Panel.ExportBinCommand.ExecuteAsync(null);
        Assert.False(File.Exists(_picker.BinPath));
        Assert.Equal(row.Summary, Panel.Message);
    }

    // ---- connected devices

    [Fact]
    public void A_device_that_is_plugged_in_is_listed_with_what_it_reports_and_gone_when_unplugged()
    {
        Assert.False(Panel.HasConnected);
        var poi = FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36), out _);
        _hub.Plug(poi);

        var line = Assert.Single(Panel.Connected);
        Assert.Same(line, Panel.SelectedConnected);
        Assert.True(line.CanPlayShows);
        Assert.Equal("Pixel Poi 5", line.Title);
        Assert.Contains("36 LEDs", line.Detail);
        Assert.Contains("COM7", line.Detail);
        Assert.Contains("“left”", line.Detail);
        Assert.True(Panel.HasConnected);

        _hub.Unplug(poi);
        Assert.Empty(Panel.Connected);
        Assert.Null(Panel.SelectedConnected);
        Assert.False(Panel.HasConnected);
    }

    [Fact]
    public void A_device_in_its_bootloader_is_listed_but_plays_no_shows()
    {
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5BOOT", FakeShowTransport.PoiInfo(36), out _));
        var line = Panel.Connected.Single();
        Assert.False(line.CanPlayShows);
        Assert.Contains("boot", line.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.False(Panel.AddFromConnectedCommand.CanExecute(null));
    }

    [Fact]
    public void Devices_that_were_connected_before_the_panel_opened_are_listed_at_once()
    {
        var late = new FakeDeviceHub();
        late.Plug(FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36), out _));
        using var editor = new EditorViewModel(ProjectDocument.CreateNew("Other"), null, null, late);
        Assert.Single(editor.Devices.Connected);
    }

    [Fact]
    public async Task The_search_for_devices_is_switched_on_and_off()
    {
        Assert.False(Panel.IsSearching);
        await Panel.ToggleSearchCommand.ExecuteAsync(null);
        Assert.True(_hub.IsSearching);
        Assert.True(Panel.IsSearching);
        await Panel.ToggleSearchCommand.ExecuteAsync(null);
        Assert.False(Panel.IsSearching);
    }

    [Fact]
    public void A_project_device_is_made_from_a_connected_one_with_its_leds_memory_and_name_and_the_timelines_that_fit()
    {
        var fits = PixelTimeline("fits", 36);
        PixelTimeline("other", 24);
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36, 2_000_000, "left"), out _));

        Panel.AddFromConnectedCommand.Execute(null);
        var binding = Panel.Selected!.Binding;
        Assert.Equal(DeviceKind.Poi, binding.DeviceType);
        Assert.Equal((36, 2_000_000L, "left", "left"), (binding.LedCount, binding.MemoryBytes, binding.Name, binding.NetworkTag));
        Assert.Equal([fits.Id], binding.TimelineIds);
    }

    [Fact]
    public void The_project_device_takes_the_leds_memory_and_tag_of_a_connected_one_in_one_step()
    {
        PixelTimeline("A", 36);
        var row = AddPoi();
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36, 1_500_000, "right"), out _));

        Assert.True(Panel.TakeFromConnectedCommand.CanExecute(null));
        Panel.TakeFromConnectedCommand.Execute(null);
        Assert.Equal((36, 1_500_000L, "right"), (row.Binding.LedCount, row.Binding.MemoryBytes, row.Binding.NetworkTag));
        _doc.Undo();
        Assert.Equal((0, 0L, (string?)null), (row.Binding.LedCount, row.Binding.MemoryBytes, row.Binding.NetworkTag));
    }

    // ---- the upload

    [Fact]
    public async Task A_poi_gets_the_file_of_the_project_device_block_by_block_and_the_transfer_is_closed()
    {
        PixelTimeline("A", 36);
        var row = AddPoi();
        var poi = FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36, 2_000_000), out var transport);
        _hub.Plug(poi);
        Panel.TakeFromConnectedCommand.Execute(null);
        byte[] expected = row.Result.Bin!;
        bool dirty = _doc.IsDirty;

        await Panel.UploadCommand.ExecuteAsync(null);

        Assert.Equal(expected.Length, transport.AnnouncedSize);
        Assert.Equal(expected, transport.Show.ToArray());
        Assert.True(transport.ShowClosed);
        Assert.Equal(1.0, Panel.Progress);
        Assert.False(Panel.IsUploading);
        Assert.Contains("Pixel Poi 5", Panel.Message);
        Assert.Equal(dirty, _doc.IsDirty);                       // an upload does not change the project
        Assert.True(transport.Blocks > 1);                       // several blocks of up to 250 bytes
    }

    [Fact]
    public async Task Nothing_is_sent_without_a_project_device_or_a_connected_one()
    {
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(Strings.Devices_NoProjectDevice, Panel.Message);

        PixelTimeline("A", 36);
        AddPoi();
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(Strings.Devices_NoConnected, Panel.Message);
    }

    [Fact]
    public async Task A_device_of_another_family_a_bootloader_or_another_number_of_leds_is_refused_before_anything_is_sent()
    {
        PixelTimeline("A", 36);
        var row = AddPoi();

        _hub.Plug(FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(64), out var video));
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Contains("Video module", Panel.Message);
        Assert.Equal(-1, video.AnnouncedSize);

        _hub.Unplug(Panel.Connected[0].Device);
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5BOOT", FakeShowTransport.PoiInfo(36), out var boot));
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Contains("boot", Panel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(-1, boot.AnnouncedSize);

        _hub.Unplug(Panel.Connected[0].Device);
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(24), out var shorter));
        row.LedCount = 0;                                       // the project device does not know its LEDs: the first timeline decides, 36
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(-1, shorter.AnnouncedSize);
        Assert.Contains("36", Panel.Message);                   // the picture is 36 high, the device has 24
    }

    [Fact]
    public async Task A_file_that_does_not_fit_the_memory_the_device_reports_is_refused()
    {
        PixelTimeline("A", 36);
        var row = AddPoi();
        long size = row.Result.Bin!.Length;
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36, memory: 100), out var transport));

        await Panel.UploadCommand.ExecuteAsync(null);

        Assert.Equal(-1, transport.AnnouncedSize);
        Assert.Contains(EditorViewModel.IssueText(new BuildIssue(BuildSeverity.Error, BuildIssueCode.TooBig, Detail: size)), Panel.Message);
    }

    [Fact]
    public async Task An_upload_can_be_cancelled_between_blocks_and_nothing_more_is_sent()
    {
        PixelTimeline("A", 36);
        AddPoi();
        var poi = FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36), out var transport);
        _hub.Plug(poi);
        Panel.TakeFromConnectedCommand.Execute(null);
        transport.AfterBlock = block =>
        {
            if (block == 2) Panel.CancelUploadCommand.Execute(null);
        };

        await Panel.UploadCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Devices_UploadCancelled, Panel.Message);
        Assert.False(Panel.IsUploading);
        Assert.Equal(2, transport.Blocks);
        Assert.False(transport.ShowClosed);
        Assert.False(Panel.CancelUploadCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_device_that_refuses_the_show_is_told_in_a_line()
    {
        PixelTimeline("A", 36);
        AddPoi();
        var poi = FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36), out var transport);
        transport.AcceptShow = 0;
        _hub.Plug(poi);
        Panel.TakeFromConnectedCommand.Execute(null);

        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Contains("refused", Panel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Panel.IsUploading);
    }

    [Fact]
    public async Task A_video_module_also_plays_the_file_of_a_fan_and_a_lightmaster_the_light_file()
    {
        PixelTimeline("A", 36);
        Panel.AddDeviceCommand.Execute(DeviceKind.Fan);
        var fan = Panel.Selected!;
        Assert.Single(fan.Binding.TimelineIds);
        var module = FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(36), out var videoTransport);
        _hub.Plug(module);
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(fan.Result.Bin, videoTransport.Show.ToArray());

        var light = new Timeline { Kind = TimelineKind.Light, Name = "light" };
        _doc.Execute(Edits.AddTimeline(_doc.Project, light));
        _doc.Execute(Edits.AddItem(light, new TimelineItem { Kind = ItemKind.Solid, Color = "#FF2040A0", Start = S(0), Duration = S(1) }));
        Panel.AddDeviceCommand.Execute(DeviceKind.Light);
        var lightRow = Panel.Selected!;
        _hub.Plug(FakeShowTransport.Connect("LRFW", FakeShowTransport.LightInfo(), out var lightTransport, "COM8"));
        Panel.SelectedConnected = Panel.Connected[^1];
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.True(BinReader.IsLght(lightTransport.Show.ToArray()));
        Assert.Equal(lightRow.Result.Bin!.Length, lightTransport.AnnouncedSize);
    }

    [Fact]
    public async Task An_epic_poi_gets_its_file_in_the_small_blocks_and_a_stretching_it_cannot_play_is_refused()
    {
        string gradient = _doc.AddMedia(Png(5, 1), "g.png");
        var timeline = new Timeline { Kind = TimelineKind.EpicPoi, Name = "epic" };
        _doc.Execute(Edits.AddTimeline(_doc.Project, timeline));
        var item = new TimelineItem { Kind = ItemKind.Gradient, ResourceId = gradient, SourcePath = "g.png", Start = S(0), Duration = S(1), Stretching = 4 };
        _doc.Execute(Edits.AddItem(timeline, item));
        Panel.AddDeviceCommand.Execute(DeviceKind.EpicPoi);
        var row = Panel.Selected!;
        Assert.Equal([timeline.Id], row.Binding.TimelineIds);
        Assert.False(row.HasLedCount);

        _hub.Plug(FakeShowTransport.Connect("E-PIC_SFW", FakeShowTransport.EpicInfo(), out var transport));
        Assert.True(Panel.SelectedConnected!.CanPlayShows);
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(row.Result.Bin, transport.Show.ToArray());
        Assert.Equal((row.Result.Bin!.Length + 59) / 60, transport.Blocks);   // 60 bytes a block
        Assert.True(transport.ShowClosed);

        item.Stretching = Edits.MaxEpicStretching + 1;
        transport.Show.Clear();
        row.Refresh();
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(EditorViewModel.IssueText(new BuildIssue(BuildSeverity.Error, BuildIssueCode.StretchingTooLarge)), Panel.Message);
        Assert.Empty(transport.Show);
    }

    [Fact]
    public async Task A_pair_of_video_chains_that_end_at_different_times_is_refused_and_one_that_ends_together_is_sent()
    {
        string gif = _doc.AddMedia(Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.gif"));
        Timeline Chain(string name, double length, params int[] leds)
        {
            var layout = new LedLayout { Name = name, Columns = 2, Rows = 2, Leds = [.. leds] };
            var timeline = new Timeline { Kind = TimelineKind.Video, Name = name };
            _doc.Execute(Edits.AddTimeline(_doc.Project, timeline));
            _doc.Execute(Edits.AddItem(timeline, new TimelineItem
            {
                Kind = ItemKind.Video, ResourceId = gif, SourcePath = "glasses.gif", Start = S(0), Duration = S(length), Stretching = 40,
            }));
            _doc.Execute(Edits.AddLedLayout(_doc.Project, layout, timeline));
            return timeline;
        }
        var left = Chain("left", 2, 0, 1);
        var right = Chain("right", 3, 2, 3, 1);

        Panel.AddDeviceCommand.Execute(DeviceKind.Video);
        var row = Panel.Selected!;
        Assert.Equal([left.Id, right.Id], row.Binding.TimelineIds);
        Assert.False(row.CanBuild);
        Assert.Contains(EditorViewModel.IssueText(new BuildIssue(BuildSeverity.Error, BuildIssueCode.VideoPairEndsDiffer)), row.IssueLines);

        _hub.Plug(FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(5), out var transport));
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(row.Summary, Panel.Message);
        Assert.Equal(-1, transport.AnnouncedSize);

        _doc.Execute(Edits.ResizeItem(left, left.Items[0], S(3)));
        Assert.True(row.CanBuild);
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(row.Result.Bin, transport.Show.ToArray());
        Assert.Equal(5, BinReader.ReadPpoi(transport.Show.ToArray()).Height);   // 2 + 3 LEDs, one timeline of both
    }

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(10, 20, 30));
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }
}
