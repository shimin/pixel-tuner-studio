using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Package;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;

namespace PixelTuner.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>Set by the view: file dialogs need the window or view that hosts them.</summary>
    public IFilePicker? FilePicker { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    ProjectSummary? _project;

    [ObservableProperty]
    string? _error;

    public bool HasProject => Project is not null;

    /// <summary>The app name: Pixel Tuner Studio or Pixel Tuner Stage.</summary>
    public string Title => Product.Current.Name;

    [RelayCommand]
    async Task OpenProjectAsync()
    {
        if (FilePicker is null) return;
        Error = null;
        try
        {
            await using var stream = await FilePicker.PickPackageAsync();
            if (stream is null) return;
            using var pkg = PxPackage.Open(stream, leaveOpen: true);
            Project = ProjectSummary.From(pkg);
        }
        catch (Exception ex) when (ex is InvalidDataException or PackageTooNewException or IOException)
        {
            Error = string.Format(Strings.Error_OpenFailed, ex.Message);
        }
    }
}

/// <summary>What the start screen shows about an opened package.</summary>
public sealed record ProjectSummary(string Name, int Timelines, int Devices, int Markers, int Media, ObservableCollection<string> TimelineNames)
{
    public static ProjectSummary From(PxPackage pkg) => new(
        string.IsNullOrWhiteSpace(pkg.Project.Name) ? "—" : pkg.Project.Name,
        pkg.Project.Timelines.Count,
        pkg.Project.Devices.Count,
        pkg.Project.Markers.Count,
        pkg.Manifest.Resources.Count,
        new ObservableCollection<string>(pkg.Project.Timelines.Select(t => t.Name)));
}
