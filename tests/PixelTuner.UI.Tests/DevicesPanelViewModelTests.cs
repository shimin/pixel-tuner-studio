using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using PixelTuner.Devices;
using PixelTuner.Package;
using PixelTuner.UI.DevicesPanel;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;
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
    public void Devices_that_were_connected_before_the_panel_opened_are_listed_at_once()
    {
        var late = new FakeDeviceHub();
        late.Plug(FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36), out _));
        using var editor = new EditorViewModel(ProjectDocument.CreateNew("Other"), null, null, late);
        Assert.Single(editor.Devices.Connected);
    }

    [Fact]
    public void A_problem_with_the_search_is_told_in_a_line_and_goes_away_with_it()
    {
        Assert.False(Panel.HasSearchProblem);
        _hub.Problem = "Address already in use";
        Panel.Poll();
        Assert.True(Panel.HasSearchProblem);
        Assert.Contains("3331", Panel.SearchProblem);
        _hub.Problem = null;
        Panel.Poll();
        Assert.False(Panel.HasSearchProblem);
    }

    [Fact]
    public void The_state_a_device_reports_is_read_into_the_same_line_so_the_selection_stays()
    {
        byte[] info = FakeShowTransport.PoiInfo(36);
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", info, out _));
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(24), out _, "COM8"));
        var first = Panel.Connected[0];
        var second = Panel.Connected[1];
        Panel.SelectedConnected = second;
        var changes = new List<string?>();
        second.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Panel.Poll();                                              // nothing changed: nothing is raised
        Assert.Empty(changes);
        Assert.Same(second, Panel.SelectedConnected);

        info[5] = 0; info[6] = 42;                                 // the battery of the first
        first.Device.ReadInfo();
        var firstChanges = new List<string?>();
        first.PropertyChanged += (_, e) => firstChanges.Add(e.PropertyName);
        Panel.Poll();

        Assert.Contains("battery 42", first.Detail);
        Assert.NotEmpty(firstChanges);
        Assert.Empty(changes);                                     // the other line is left alone
        Assert.Same(first, Panel.Connected[0]);
        Assert.Same(second, Panel.SelectedConnected);              // still selected
    }

    // ---- what is sent: the plan of a connected device

    private ShowDevice PlugPoi(out FakeShowTransport transport, int leds = 36, uint memory = 2_000_000, string channel = "COM7")
    {
        var device = FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(leds, memory), out transport, channel);
        _hub.Plug(device);
        return device;
    }

    [Fact]
    public void There_is_no_device_of_the_project_only_the_connected_ones_and_nothing_is_sent_unasked()
    {
        PixelTimeline("A", 36);
        bool dirty = _doc.IsDirty;
        Assert.Null(Panel.SelectedPlan);
        PlugPoi(out var transport);

        Assert.NotNull(Panel.SelectedPlan);
        Assert.Equal(-1, transport.AnnouncedSize);                     // set up, not sent
        Assert.Empty(transport.Show);
        Assert.Empty(_doc.Project.Devices);                            // and nothing is added to the project
        Assert.Equal(dirty, _doc.IsDirty);
    }

    [Fact]
    public void The_plan_starts_with_the_timelines_of_the_kind_that_have_something_on_them_and_fit_the_leds_of_the_device()
    {
        var a = PixelTimeline("A", 36);
        PixelTimeline("Too high", 50);
        _doc.Execute(Edits.AddTimeline(_doc.Project, new Timeline { Kind = TimelineKind.Pixel, Name = "empty" }));
        _doc.Execute(Edits.AddTimeline(_doc.Project, new Timeline { Kind = TimelineKind.Light, Name = "light" }));
        PlugPoi(out _);

        var plan = Panel.SelectedPlan!;
        Assert.Equal([a.Id], plan.Binding.TimelineIds);
        Assert.Equal(3, plan.Choices.Count);                           // the pixel timelines, also the empty one and the one that is too high
        Assert.Equal(["1", "", ""], plan.Choices.Select(c => c.Order));
        Assert.Equal([true, false, true], plan.Choices.Select(c => c.Fits));   // "empty" has no height yet
        Assert.False(plan.Choices[1].CanCheck);
        Assert.True(plan.CanBuild);
        Assert.Contains("KB", plan.Summary);
        Assert.Contains(" of ", plan.Summary);                          // against the memory of the device
    }

    [Fact]
    public void Ticking_a_timeline_puts_it_at_the_end_unticking_takes_it_out_and_the_arrows_change_the_order()
    {
        var a = PixelTimeline("A", 36);
        var b = PixelTimeline("B", 36);
        bool dirty = _doc.IsDirty;
        PlugPoi(out _);
        var plan = Panel.SelectedPlan!;
        Assert.Equal([a.Id, b.Id], plan.Binding.TimelineIds);

        plan.Choices[0].IsChecked = false;
        Assert.Equal([b.Id], plan.Binding.TimelineIds);
        plan.Choices[0].IsChecked = true;
        Assert.Equal([b.Id, a.Id], plan.Binding.TimelineIds);
        Assert.Equal(["2", "1"], plan.Choices.Select(c => c.Order));

        plan.MoveUpCommand.Execute(plan.Choices[0]);                   // A, played second, goes first
        Assert.Equal([a.Id, b.Id], plan.Binding.TimelineIds);
        plan.MoveDownCommand.Execute(plan.Choices[0]);
        Assert.Equal([b.Id, a.Id], plan.Binding.TimelineIds);
        plan.MoveDownCommand.Execute(plan.Choices[0]);                 // already last: nothing
        Assert.Equal([b.Id, a.Id], plan.Binding.TimelineIds);

        plan.Choices[0].IsChecked = false;
        plan.Choices[1].IsChecked = false;
        Assert.Empty(plan.Binding.TimelineIds);
        Assert.False(plan.CanBuild);
        Assert.NotEmpty(plan.IssueLines);
        Assert.Equal(dirty, _doc.IsDirty);                             // the choice is not part of the project
    }

    [Fact]
    public void The_plan_follows_the_project_new_timelines_are_listed_unticked_and_deleted_ones_leave()
    {
        var a = PixelTimeline("A", 36);
        PlugPoi(out _);
        var plan = Panel.SelectedPlan!;

        var b = PixelTimeline("B", 36);
        Assert.Equal(2, plan.Choices.Count);
        Assert.Equal([a.Id], plan.Binding.TimelineIds);                // B is not played until it is ticked

        _doc.Undo();
        _doc.Undo();                                                   // the item and the timeline B
        Assert.Single(plan.Choices);
        _doc.Execute(Edits.RemoveTimeline(_doc.Project, a));
        Assert.Empty(plan.Choices);
        Assert.Empty(plan.Binding.TimelineIds);
    }

    [Fact]
    public void Each_connected_device_has_a_plan_of_its_own_that_stays_while_it_is_connected()
    {
        PixelTimeline("A", 36);
        PixelTimeline("B", 36);
        var first = PlugPoi(out _);
        var plan = Panel.SelectedPlan!;
        plan.Choices[0].IsChecked = false;
        PlugPoi(out _, channel: "COM8");

        Panel.SelectedConnected = Panel.Connected[1];
        Assert.NotSame(plan, Panel.SelectedPlan);
        Assert.Equal(2, Panel.SelectedPlan!.Binding.TimelineIds.Count);

        Panel.SelectedConnected = Panel.Connected[0];
        Assert.Same(plan, Panel.SelectedPlan);
        Assert.Single(plan.Binding.TimelineIds);

        _hub.Unplug(first);                                            // gone: the plan goes with it
        _hub.Plug(first);
        Assert.NotSame(plan, Panel.SelectedPlan);
    }

    [Fact]
    public void A_device_in_its_bootloader_has_no_plan_and_cannot_be_uploaded_to()
    {
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5BOOT", FakeShowTransport.PoiInfo(36), out _));
        Assert.Null(Panel.SelectedPlan);
        Assert.True(Panel.HasNoShowDevice);
        Assert.False(Panel.UploadCommand.CanExecute(null));
    }

    [Fact]
    public void A_video_module_with_a_4_GB_card_shows_gigabytes_in_its_line_and_in_the_plan()
    {
        PixelTimeline("A", 36);
        _hub.Plug(FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(229, 3_878_489), out _));
        Assert.Contains("memory 3.7 GB", Panel.Connected.Single().Detail);

        Panel.SelectedPlan!.KindIndex = 1;                             // a fan: a pixel timeline
        Assert.Contains("3.7 GB", Panel.SelectedPlan.Summary);
        Assert.Equal(3_878_489L * 1024, Panel.SelectedPlan.Binding.MemoryBytes);
    }

    [Fact]
    public void A_video_module_takes_a_video_file_or_a_fan_file_and_a_fan_plays_one_timeline_and_a_pair_of_chains_two()
    {
        var a = PixelTimeline("A", 36);
        var b = PixelTimeline("B", 36);
        _hub.Plug(FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(36), out _));
        var plan = Panel.SelectedPlan!;
        Assert.True(plan.HasKindChoice);
        Assert.Equal(DeviceKind.Video, plan.Kind);
        Assert.Empty(plan.Choices);                                    // no video timelines yet

        plan.KindIndex = 1;
        Assert.Equal(DeviceKind.Fan, plan.Kind);
        Assert.Equal([a.Id], plan.Binding.TimelineIds);                // a fan plays one
        plan.Choices[1].IsChecked = true;                              // B takes the place of A
        Assert.Equal([b.Id], plan.Binding.TimelineIds);

        // A plan of another device does not offer the choice.
        PlugPoi(out _, channel: "COM8");
        Panel.SelectedConnected = Panel.Connected[1];
        Assert.False(Panel.SelectedPlan!.HasKindChoice);
    }

    // ---- the upload

    [Fact]
    public async Task Upload_sends_the_file_of_the_plan_block_by_block_and_closes_the_transfer_and_names_the_timelines()
    {
        PixelTimeline("Alpha", 36);
        PixelTimeline("Beta", 36);
        PlugPoi(out var transport);
        var plan = Panel.SelectedPlan!;
        plan.Choices[1].IsChecked = false;                             // only Alpha
        byte[] expected = plan.Result.Bin!;
        bool dirty = _doc.IsDirty;

        Assert.True(Panel.UploadCommand.CanExecute(null));
        await Panel.UploadCommand.ExecuteAsync(null);

        Assert.Equal(expected.Length, transport.AnnouncedSize);
        Assert.Equal(expected, transport.Show.ToArray());
        Assert.True(transport.ShowClosed);
        Assert.Equal(1.0, Panel.Progress);
        Assert.False(Panel.IsUploading);
        Assert.Contains("Pixel Poi 5", Panel.Message);
        Assert.Contains("Alpha", Panel.Message);
        Assert.DoesNotContain("Beta", Panel.Message);
        Assert.Equal(dirty, _doc.IsDirty);                             // an upload does not change the project
        Assert.True(transport.Blocks > 1);                             // several blocks of up to 250 bytes
    }

    [Fact]
    public async Task What_is_sent_follows_the_choice_made_before_the_button_is_pressed()
    {
        var a = PixelTimeline("A", 36, width: 8);
        var b = PixelTimeline("B", 36, width: 30);
        PlugPoi(out var transport);
        var plan = Panel.SelectedPlan!;
        plan.MoveDownCommand.Execute(plan.Choices[0]);                 // B first

        await Panel.UploadCommand.ExecuteAsync(null);

        Assert.Equal([b.Id, a.Id], plan.Binding.TimelineIds);
        Assert.Equal(plan.Result.Bin, transport.Show.ToArray());
    }

    [Fact]
    public async Task The_build_settings_are_part_of_the_file_and_changing_them_changes_the_plan()
    {
        PixelTimeline("A", 36);
        PlugPoi(out var transport);
        var plan = Panel.SelectedPlan!;
        byte[] before = plan.Result.Bin!;

        Panel.DepthIndex = 0;                                          // 12 bit
        Assert.NotEqual(before.Length, plan.Result.Bin!.Length);
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(plan.Result.Bin, transport.Show.ToArray());
    }

    [Fact]
    public async Task Nothing_is_sent_without_a_connected_device_or_when_the_build_refuses()
    {
        PixelTimeline("A", 36);
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(Strings.Devices_NoConnected, Panel.Message);

        PlugPoi(out var transport);
        Panel.SelectedPlan!.Choices[0].IsChecked = false;               // nothing in the file
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(Panel.SelectedPlan.Summary, Panel.Message);
        Assert.Equal(-1, transport.AnnouncedSize);
    }

    [Fact]
    public async Task A_file_that_does_not_fit_the_memory_the_device_reports_is_refused()
    {
        PixelTimeline("A", 36, width: 200);
        PlugPoi(out var transport, memory: 2);                          // 2 KB

        await Panel.UploadCommand.ExecuteAsync(null);

        Assert.Equal(-1, transport.AnnouncedSize);
        Assert.False(Panel.SelectedPlan!.CanBuild);
        Assert.Contains("does not fit", Panel.SelectedPlan.Summary);
        Assert.Equal(Panel.SelectedPlan.Summary, Panel.Message);
    }

    [Fact]
    public async Task An_upload_can_be_cancelled_between_blocks_and_nothing_more_is_sent()
    {
        PixelTimeline("A", 36);
        PlugPoi(out var transport);
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
        var poi = FakeShowTransport.Connect("E-PIC_v5FW", FakeShowTransport.PoiInfo(36), out var transport);
        transport.AcceptShow = 0;
        _hub.Plug(poi);

        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Contains("refused", Panel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Panel.IsUploading);
    }

    [Fact]
    public async Task A_video_module_gets_the_file_of_a_fan_and_a_lightmaster_the_light_file()
    {
        PixelTimeline("A", 36);
        _hub.Plug(FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(36), out var videoTransport));
        Panel.SelectedPlan!.KindIndex = 1;                             // a fan
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(Panel.SelectedPlan.Result.Bin, videoTransport.Show.ToArray());
        Assert.True(videoTransport.ShowClosed);

        var light = new Timeline { Kind = TimelineKind.Light, Name = "light" };
        _doc.Execute(Edits.AddTimeline(_doc.Project, light));
        _doc.Execute(Edits.AddItem(light, new TimelineItem { Kind = ItemKind.Solid, Color = "#FF2040A0", Start = S(0), Duration = S(1) }));
        _hub.Plug(FakeShowTransport.Connect("LRFW", FakeShowTransport.LightInfo(), out var lightTransport, "COM8"));
        Panel.SelectedConnected = Panel.Connected[^1];
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.True(BinReader.IsLght(lightTransport.Show.ToArray()));
        Assert.Equal(Panel.SelectedPlan!.Result.Bin!.Length, lightTransport.AnnouncedSize);
    }

    [Fact]
    public async Task An_epic_poi_gets_its_file_in_the_small_blocks_and_a_stretching_it_cannot_play_is_refused()
    {
        string gradient = _doc.AddMedia(Png(5, 1), "g.png");
        var timeline = new Timeline { Kind = TimelineKind.EpicPoi, Name = "epic" };
        _doc.Execute(Edits.AddTimeline(_doc.Project, timeline));
        var item = new TimelineItem { Kind = ItemKind.Gradient, ResourceId = gradient, SourcePath = "g.png", Start = S(0), Duration = S(1), Stretching = 4 };
        _doc.Execute(Edits.AddItem(timeline, item));

        _hub.Plug(FakeShowTransport.Connect("E-PIC_SFW", FakeShowTransport.EpicInfo(), out var transport));
        var plan = Panel.SelectedPlan!;
        Assert.Equal([timeline.Id], plan.Binding.TimelineIds);
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(plan.Result.Bin, transport.Show.ToArray());
        Assert.Equal((plan.Result.Bin!.Length + 59) / 60, transport.Blocks);   // 60 bytes a block
        Assert.True(transport.ShowClosed);

        item.Stretching = Edits.MaxEpicStretching + 1;
        transport.Show.Clear();
        plan.Refresh();
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

        _hub.Plug(FakeShowTransport.Connect("PLR_v2FW", FakeShowTransport.VideoInfo(5), out var transport));
        var plan = Panel.SelectedPlan!;
        Assert.Equal([left.Id, right.Id], plan.Binding.TimelineIds);
        Assert.False(plan.CanBuild);
        Assert.Contains(EditorViewModel.IssueText(new BuildIssue(BuildSeverity.Error, BuildIssueCode.VideoPairEndsDiffer)), plan.IssueLines);

        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(plan.Summary, Panel.Message);
        Assert.Equal(-1, transport.AnnouncedSize);

        _doc.Execute(Edits.ResizeItem(left, left.Items[0], S(3)));
        Assert.True(plan.CanBuild);
        await Panel.UploadCommand.ExecuteAsync(null);
        Assert.Equal(plan.Result.Bin, transport.Show.ToArray());
        Assert.Equal(5, BinReader.ReadPpoi(transport.Show.ToArray()).Height);   // 2 + 3 LEDs, one timeline of both
    }

    [Fact]
    public void There_is_no_way_to_save_the_file_of_a_show_from_the_panel()
    {
        Assert.DoesNotContain(typeof(DevicesPanelViewModel).GetProperties(), p => p.Name.Contains("Export", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PixelTuner.UI.Services.IFilePicker).GetMethods(), m => m.Name.Contains("Bin", StringComparison.OrdinalIgnoreCase));
    }

    // ---- firmware

    private static async Task Until(Func<bool> done)
    {
        for (int i = 0; i < 500 && !done(); i++) await Task.Delay(10);
        Assert.True(done());
    }

    private static byte[] Info(int platform, int boot, int firmware, int leds = 36)
    {
        byte[] info = FakeShowTransport.PoiInfo(leds);
        info[0] = (byte)platform; info[1] = (byte)boot; info[2] = (byte)firmware;
        return info;
    }

    [Fact]
    public void An_older_firmware_is_offered_the_newer_one_of_the_app_and_a_current_one_is_not()
    {
        var shipped = FirmwareCatalog.For(DeviceFamily.PixelPoi, 5)!;
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", Info(5, 3, shipped.Version - 1), out _));
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", Info(5, 3, shipped.Version), out _, "COM8"));

        var old = Panel.Connected[0];
        Assert.True(old.UpdateAvailable);
        Assert.Contains(shipped.Version.ToString(), old.FirmwareNote);
        Assert.Equal(Strings.Devices_UpdateFirmware, old.FirmwareButtonText);
        Assert.False(Panel.Connected[1].UpdateAvailable);
        Assert.False(Panel.Connected[1].HasFirmwareNote);
        Assert.True(Panel.Connected[1].CanUpdateFirmware);         // a reinstall is still possible
    }

    [Fact]
    public async Task A_device_is_asked_to_restart_into_its_bootloader_and_gets_the_firmware_when_it_comes_back_there()
    {
        var dialogs = new FakeDialogs(SaveChoice.Cancel);
        using var editor = new EditorViewModel(_doc, null, () => _picker, _hub, () => dialogs);
        var panel = editor.Devices;
        var shipped = FirmwareCatalog.For(DeviceFamily.PixelPoi, 5)!;
        var device = FakeShowTransport.Connect("E-PIC_v5FW", Info(5, 3, shipped.Version - 1), out var transport);
        _hub.Plug(device);

        await panel.UpdateFirmwareCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Confirmed);
        Assert.Contains("Pixel Poi 5", asked.Detail);
        Assert.True(transport.SwitchedToBoot);
        Assert.Empty(transport.Firmware);                           // not yet: the bootloader has to come up
        Assert.False(panel.UpdateFirmwareCommand.CanExecute(null)); // and nothing else is started meanwhile

        _hub.Unplug(device);
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5BOOT", Info(5, 3, shipped.Version - 1), out var boot));

        await Until(() => panel.Writing is not null);              // the device is listed from the thread that found it
        await panel.Writing!;
        Assert.Equal(shipped.Data, boot.Firmware.ToArray());
        Assert.True(boot.FirmwareClosed);
        Assert.Empty(boot.Show);
        Assert.False(panel.IsUploading);
        Assert.Contains("Pixel Poi 5", panel.Message);
        Assert.True(panel.UpdateFirmwareCommand.CanExecute(null));
    }

    [Fact]
    public async Task Nothing_is_written_when_the_update_is_not_confirmed()
    {
        var dialogs = new FakeDialogs(SaveChoice.Cancel, confirm: false);
        using var editor = new EditorViewModel(_doc, null, () => _picker, _hub, () => dialogs);
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", Info(5, 3, 1), out var transport));

        await editor.Devices.UpdateFirmwareCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Confirmed);
        Assert.False(transport.SwitchedToBoot);
        Assert.Equal(0, transport.Blocks);
    }

    [Fact]
    public async Task A_device_that_is_in_its_bootloader_already_is_written_at_once_and_firmware_cannot_be_cancelled()
    {
        var shipped = FirmwareCatalog.For(DeviceFamily.Video, 2)!;
        _hub.Plug(FakeShowTransport.Connect("PLR_v2BOOT", Info(2, 1, 1), out var boot));
        var line = Panel.Connected.Single();
        Assert.Equal(Strings.Devices_InstallFirmware, line.FirmwareButtonText);
        Assert.Equal(Strings.Devices_FirmwareBootNote, line.FirmwareNote);
        bool offeredCancel = false;
        boot.AfterBlock = _ => offeredCancel |= Panel.CancelUploadCommand.CanExecute(null);

        await Panel.UpdateFirmwareCommand.ExecuteAsync(null);

        Assert.Equal(shipped.Data, boot.Firmware.ToArray());
        Assert.False(boot.SwitchedToBoot);
        Assert.False(offeredCancel);
        Assert.Equal(1.0, Panel.Progress);
    }

    [Fact]
    public async Task A_firmware_file_is_written_to_the_selected_device_and_a_file_that_is_not_firmware_is_refused()
    {
        _hub.Plug(FakeShowTransport.Connect("PLR_v2BOOT", Info(2, 1, 1), out var boot));
        string good = Path.Combine(_dir, "mine.bin");
        byte[] firmware = Enumerable.Range(0, 400).Select(i => (byte)i).ToArray();
        File.WriteAllBytes(good, firmware);

        _picker.FirmwarePath = null;                                // cancelled
        await Panel.UpdateFirmwareFromFileCommand.ExecuteAsync(null);
        Assert.Empty(boot.Firmware);

        string tiny = Path.Combine(_dir, "tiny.bin");
        File.WriteAllBytes(tiny, [1, 2, 3]);
        _picker.FirmwarePath = tiny;
        await Panel.UpdateFirmwareFromFileCommand.ExecuteAsync(null);
        Assert.Empty(boot.Firmware);
        Assert.Contains("tiny.bin", Panel.Message);

        _picker.FirmwarePath = good;
        await Panel.UpdateFirmwareFromFileCommand.ExecuteAsync(null);
        Assert.Equal(firmware, boot.Firmware.ToArray());
        Assert.True(boot.FirmwareClosed);
    }

    [Fact]
    public async Task A_device_that_does_not_come_back_in_its_bootloader_is_told_in_a_line_and_the_buttons_come_back()
    {
        var shipped = FirmwareCatalog.For(DeviceFamily.PixelPoi, 5)!;
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5FW", Info(5, 3, shipped.Version - 1), out _));
        await Panel.UpdateFirmwareCommand.ExecuteAsync(null);
        Assert.False(Panel.UpdateFirmwareCommand.CanExecute(null));

        Panel.ExpireBootWait();
        Panel.Poll();

        Assert.Equal(Strings.Devices_BootTimeout, Panel.Message);
        Assert.True(Panel.UpdateFirmwareCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_bootloader_that_shows_up_without_being_asked_is_not_written()
    {
        _hub.Plug(FakeShowTransport.Connect("E-PIC_v5BOOT", Info(5, 3, 1), out var boot));
        await Task.Yield();
        Assert.Empty(boot.Firmware);
    }

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(10, 20, 30));
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }
}
