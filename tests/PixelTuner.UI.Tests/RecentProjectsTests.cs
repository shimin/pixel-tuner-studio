using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public sealed class RecentProjectsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ui-recent").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Package(string name, string projectName = "Show")
    {
        string path = Path.Combine(_dir, name);
        new PxPackageWriter().Save(path, new ShowProject { Name = projectName });
        return path;
    }

    private string ListFile => Path.Combine(_dir, "settings", "recent.txt");

    // ---- the list

    [Fact]
    public void The_newest_project_is_first_a_project_is_there_once_and_the_list_has_a_limit()
    {
        var recent = new RecentProjects(null, limit: 3);
        string a = Package("a.pxpkg"), b = Package("b.pxpkg"), c = Package("c.pxpkg"), d = Package("d.pxpkg");
        recent.Add(a); recent.Add(b); recent.Add(c);
        recent.Add(a);   // again: moves up
        Assert.Equal([a, c, b], recent.Paths);
        recent.Add(d);
        Assert.Equal([d, a, c], recent.Paths);
    }

    [Fact]
    public void The_list_is_kept_in_a_file_and_forgets_files_that_are_gone()
    {
        string a = Package("a.pxpkg"), b = Package("b.pxpkg");
        var first = new RecentProjects(ListFile);
        first.Add(a); first.Add(b);

        Assert.Equal([b, a], new RecentProjects(ListFile).Paths);

        File.Delete(a);
        Assert.Equal([b], new RecentProjects(ListFile).Paths);
    }

    [Fact]
    public void Remove_and_clear_change_the_file_too_and_tell_the_view()
    {
        string a = Package("a.pxpkg"), b = Package("b.pxpkg");
        var recent = new RecentProjects(ListFile);
        int changes = 0;
        recent.Changed += (_, _) => changes++;
        recent.Add(a); recent.Add(b);
        recent.Remove(a);
        Assert.Equal([b], new RecentProjects(ListFile).Paths);
        recent.Clear();
        Assert.Empty(new RecentProjects(ListFile).Paths);
        Assert.Equal(4, changes);
    }

    [Fact]
    public void A_list_that_cannot_be_kept_does_not_stop_anything()
    {
        string blocked = Path.Combine(_dir, "file");
        File.WriteAllText(blocked, "");   // a file where the folder of the list should be
        var recent = new RecentProjects(Path.Combine(blocked, "recent.txt"));
        recent.Add(Package("a.pxpkg"));
        Assert.Single(recent.Paths);
    }

    // ---- the start screen

    private static MainViewModel Studio(RecentProjects recent, FakePicker picker) =>
        new(recent) { FilePicker = picker, Dialogs = new FakeDialogs(SaveChoice.Cancel) };

    [Fact]
    public async Task Opening_a_project_puts_it_on_the_start_screen_and_a_click_opens_it_again()
    {
        string a = Package("a.pxpkg", "Alpha"), b = Package("b.pxpkg", "Beta");
        var recent = new RecentProjects(ListFile);
        var picker = new FakePicker { OpenPath = a };
        using var vm = Studio(recent, picker);
        Assert.False(vm.HasRecent);

        await vm.OpenProjectCommand.ExecuteAsync(null);
        await vm.CloseProjectCommand.ExecuteAsync(null);
        picker.OpenPath = b;
        await vm.OpenProjectCommand.ExecuteAsync(null);
        await vm.CloseProjectCommand.ExecuteAsync(null);

        Assert.True(vm.HasRecent);
        Assert.Equal(["b", "a"], vm.Recent.Select(r => r.Name));
        Assert.Equal(_dir, vm.Recent[0].Folder);

        await vm.Recent[1].OpenCommand.ExecuteAsync(null);
        Assert.Equal("Alpha", vm.ProjectName);
        Assert.Equal(["a", "b"], vm.Recent.Select(r => r.Name));
    }

    [Fact]
    public async Task A_project_that_is_gone_says_so_and_leaves_the_list()
    {
        string a = Package("a.pxpkg");
        var recent = new RecentProjects(ListFile);
        recent.Add(a);
        using var vm = Studio(recent, new FakePicker());
        File.Delete(a);

        await vm.Recent[0].OpenCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Error);
        Assert.False(vm.HasProject);
        Assert.Empty(vm.Recent);
        Assert.False(vm.HasRecent);
    }

    [Fact]
    public async Task Save_as_and_a_dropped_file_are_remembered_and_clear_empties_the_list()
    {
        string saved = Path.Combine(_dir, "saved.pxpkg"), dropped = Package("dropped.pxpkg");
        var recent = new RecentProjects(ListFile);
        var picker = new FakePicker { SavePath = saved };
        using var vm = Studio(recent, picker);

        vm.NewProjectCommand.Execute(null);
        await vm.SaveAsCommand.ExecuteAsync(null);
        await vm.OpenPathAsync(dropped);
        Assert.Equal(["dropped", "saved"], vm.Recent.Select(r => r.Name));

        vm.ClearRecentCommand.Execute(null);
        Assert.Empty(vm.Recent);
        Assert.Empty(new RecentProjects(ListFile).Paths);
    }
}
