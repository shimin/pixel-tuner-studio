using PixelTuner.Package;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public class ImportCommandTests
{
    private sealed class FakePicker(string? ptp, Stream? save) : IFilePicker
    {
        public Task<Stream?> PickPackageAsync() => Task.FromResult<Stream?>(null);
        public Task<string?> PickLegacyProjectAsync() => Task.FromResult(ptp);
        public Task<Stream?> PickPackageSaveAsync(string suggestedName)
        {
            SuggestedName = suggestedName;
            return Task.FromResult(save);
        }
        public string? SuggestedName { get; private set; }
    }

    private static string Sample => Path.Combine(AppContext.BaseDirectory, "Fixtures", "80_sample", "80_sample.ptp");

    [Fact]
    public async Task Import_saves_a_package_and_shows_it()
    {
        var output = new NonClosingStream();
        var picker = new FakePicker(Sample, output);
        var vm = new MainViewModel { FilePicker = picker };

        await vm.ImportLegacyProjectCommand.ExecuteAsync(null);

        Assert.Null(vm.Error);
        Assert.Equal("80_sample.pxpkg", picker.SuggestedName);
        Assert.True(vm.HasProject);
        Assert.NotNull(vm.Status);
        output.Position = 0;
        using var pkg = PxPackage.Open(output, leaveOpen: true);
        Assert.Equal(vm.Project!.Timelines, pkg.Project.Timelines.Count);
        Assert.True(pkg.Project.Timelines.Count > 0);
    }

    [Fact]
    public async Task Cancelling_leaves_everything_as_it_was()
    {
        var vm = new MainViewModel { FilePicker = new FakePicker(null, null) };
        await vm.ImportLegacyProjectCommand.ExecuteAsync(null);
        Assert.False(vm.HasProject);
        Assert.Null(vm.Error);

        vm = new MainViewModel { FilePicker = new FakePicker(Sample, null) };   // import ok, save cancelled
        await vm.ImportLegacyProjectCommand.ExecuteAsync(null);
        Assert.False(vm.HasProject);
    }

    [Fact]
    public async Task A_broken_project_is_reported_not_thrown()
    {
        string bad = Path.Combine(Directory.CreateTempSubdirectory().FullName, "bad.ptp");
        File.WriteAllText(bad, "not xml");
        var vm = new MainViewModel { FilePicker = new FakePicker(bad, new NonClosingStream()) };

        await vm.ImportLegacyProjectCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Error);
        Assert.False(vm.HasProject);
    }

    /// <summary>A memory stream that survives the command disposing it, so the test can read the result.</summary>
    private sealed class NonClosingStream : MemoryStream
    {
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
