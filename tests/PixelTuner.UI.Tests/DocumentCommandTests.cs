using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public class DocumentCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ui-doc").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Package(string name = "show.pxpkg", string projectName = "Show")
    {
        string path = Path.Combine(_dir, name);
        new PxPackageWriter().Save(path, new ShowProject { Name = projectName });
        return path;
    }

    private static MainViewModel Studio(FakePicker picker, SaveChoice answer = SaveChoice.Cancel) =>
        new() { FilePicker = picker, Dialogs = new FakeDialogs(answer) };

    [Fact]
    public async Task Open_shows_the_project_and_the_title_names_it()
    {
        using var vm = Studio(new FakePicker { OpenPath = Package() });
        Assert.Equal("Pixel Tuner Studio", vm.Title);

        await vm.OpenProjectCommand.ExecuteAsync(null);

        Assert.True(vm.HasProject);
        Assert.Equal("Show", vm.ProjectName);
        Assert.Equal("Show — Pixel Tuner Studio", vm.Title);
    }

    [Fact]
    public async Task Editing_marks_the_title_and_save_clears_it()
    {
        string path = Package();
        using var vm = Studio(new FakePicker { OpenPath = path });
        await vm.OpenProjectCommand.ExecuteAsync(null);

        vm.ProjectName = "Renamed";
        Assert.StartsWith(OperatingSystem.IsMacOS() ? "Renamed — Edited — " : "Renamed* — ", vm.Title);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Renamed — Pixel Tuner Studio", vm.Title);
        using var pkg = PxPackage.Open(path);
        Assert.Equal("Renamed", pkg.Project.Name);
    }

    [Fact]
    public async Task A_new_project_is_untitled_and_save_asks_for_a_file()
    {
        string target = Path.Combine(_dir, "new.pxpkg");
        var picker = new FakePicker { SavePath = target };
        using var vm = Studio(picker);

        await vm.NewProjectCommand.ExecuteAsync(null);
        Assert.Equal("Untitled — Pixel Tuner Studio", vm.Title);

        vm.ProjectName = "Fresh";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Fresh.pxpkg", picker.SuggestedName);
        Assert.Equal(target, vm.Document!.Path);
        Assert.False(vm.Document.IsDirty);
        Assert.True(File.Exists(target));
    }

    [Fact]
    public async Task Save_as_cancelled_changes_nothing()
    {
        var picker = new FakePicker { OpenPath = Package() };
        using var vm = Studio(picker);
        await vm.OpenProjectCommand.ExecuteAsync(null);
        vm.ProjectName = "Edit";

        await vm.SaveAsCommand.ExecuteAsync(null);   // SavePath is null: the user cancelled

        Assert.True(vm.Document!.IsDirty);
        Assert.Null(vm.Error);
    }

    [Fact]
    public async Task Unsaved_changes_ask_before_another_project_is_opened()
    {
        string second = Package("second.pxpkg", "Second");
        var picker = new FakePicker { OpenPath = Package() };
        var dialogs = new FakeDialogs(SaveChoice.Cancel);
        using var vm = new MainViewModel { FilePicker = picker, Dialogs = dialogs };
        await vm.OpenProjectCommand.ExecuteAsync(null);
        vm.ProjectName = "Edited";

        picker.OpenPath = second;
        await vm.OpenProjectCommand.ExecuteAsync(null);   // cancel: stays
        Assert.Equal(1, dialogs.Asked);
        Assert.Equal("Edited", dialogs.AskedAbout);
        Assert.Equal("Edited", vm.ProjectName);

        vm.Dialogs = new FakeDialogs(SaveChoice.Discard);
        await vm.OpenProjectCommand.ExecuteAsync(null);   // discard: opens the other one
        Assert.Equal("Second", vm.ProjectName);
    }

    [Fact]
    public async Task Choosing_save_in_the_question_saves_then_goes_on()
    {
        string path = Package();
        var picker = new FakePicker { OpenPath = path };
        using var vm = Studio(picker, SaveChoice.Save);
        await vm.OpenProjectCommand.ExecuteAsync(null);
        vm.ProjectName = "Kept";

        await vm.CloseProjectCommand.ExecuteAsync(null);

        Assert.False(vm.HasProject);
        using var pkg = PxPackage.Open(path);
        Assert.Equal("Kept", pkg.Project.Name);
    }

    [Fact]
    public async Task Saving_that_fails_keeps_the_project_open_and_does_not_close_it()
    {
        // The save dialog answers with a place that cannot be written.
        string blocker = Package("blocker.pxpkg");
        var picker = new FakePicker { SavePath = Path.Combine(blocker, "inside", "x.pxpkg") };
        using var vm = Studio(picker, SaveChoice.Save);
        await vm.NewProjectCommand.ExecuteAsync(null);
        vm.ProjectName = "Precious";

        await vm.CloseProjectCommand.ExecuteAsync(null);

        Assert.True(vm.HasProject);
        Assert.NotNull(vm.Error);
        Assert.True(vm.Document!.IsDirty);
    }

    [Fact]
    public async Task A_dropped_file_opens_and_a_broken_one_is_reported()
    {
        using var vm = Studio(new FakePicker());
        await vm.OpenPathAsync(Package());
        Assert.True(vm.HasProject);

        string bad = Path.Combine(_dir, "bad.pxpkg");
        File.WriteAllText(bad, "not a zip");
        await vm.OpenPathAsync(bad);

        Assert.NotNull(vm.Error);
        Assert.True(vm.HasProject);   // the previous project stays
    }

    [Fact]
    public async Task Stage_opens_from_a_stream_and_cannot_edit()
    {
        var previous = Product.Current;
        Product.Current = Product.Stage;
        try
        {
            var stream = new MemoryStream(File.ReadAllBytes(Package()));
            using var vm = new MainViewModel { FilePicker = new FakePicker { OpenStream = stream } };
            Assert.False(vm.CanEdit);

            await vm.OpenProjectCommand.ExecuteAsync(null);

            Assert.True(vm.HasProject);
            Assert.Equal("Show — Pixel Tuner Stage", vm.Title);
            await vm.OpenPathAsync(Package("other.pxpkg"));   // paths belong to Studio
            Assert.Equal("Show", vm.ProjectName);
        }
        finally
        {
            Product.Current = previous;
        }
    }
}
