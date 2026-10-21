using PixelTuner.Package;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public class ImportCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ui-import").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Fixtures", "80_sample", "80_sample.ptp");

    [Fact]
    public async Task Import_writes_a_package_and_opens_it()
    {
        string target = Path.Combine(_dir, "out.pxpkg");
        var picker = new FakePicker { LegacyPath = Sample, SavePath = target };
        using var vm = new MainViewModel { FilePicker = picker };

        await vm.ImportLegacyProjectCommand.ExecuteAsync(null);

        Assert.Null(vm.Error);
        Assert.Equal("80_sample.pxpkg", picker.SuggestedName);
        Assert.True(vm.HasProject);
        Assert.Equal(target, vm.Document!.Path);
        Assert.False(vm.Document.IsDirty);
        Assert.NotNull(vm.Status);
        using var pkg = PxPackage.Open(target);
        Assert.Equal(vm.Project!.Timelines, pkg.Project.Timelines.Count);
        Assert.True(pkg.Project.Timelines.Count > 0);
    }

    [Fact]
    public async Task Cancelling_either_dialog_leaves_everything_as_it_was()
    {
        using var vm = new MainViewModel { FilePicker = new FakePicker() };
        await vm.ImportLegacyProjectCommand.ExecuteAsync(null);
        Assert.False(vm.HasProject);
        Assert.Null(vm.Error);

        vm.FilePicker = new FakePicker { LegacyPath = Sample };   // chosen, but no place to save
        await vm.ImportLegacyProjectCommand.ExecuteAsync(null);
        Assert.False(vm.HasProject);
    }

    [Fact]
    public async Task A_broken_project_is_reported_not_thrown()
    {
        string bad = Path.Combine(_dir, "bad.ptp");
        File.WriteAllText(bad, "not xml");
        using var vm = new MainViewModel { FilePicker = new FakePicker { LegacyPath = bad, SavePath = Path.Combine(_dir, "bad.pxpkg") } };

        await vm.ImportLegacyProjectCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Error);
        Assert.False(vm.HasProject);
    }
}
