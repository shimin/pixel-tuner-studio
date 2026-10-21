using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Package;
using PixelTuner.Package.Import;
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

    [ObservableProperty]
    string? _status;

    public bool HasProject => Project is not null;

    /// <summary>Whether this app imports PixelTuner 2 projects.</summary>
    public bool CanImportLegacy => Product.Current.CanImportLegacy;

    /// <summary>The app name: Pixel Tuner Studio or Pixel Tuner Stage.</summary>
    public string Title => Product.Current.Name;

    [RelayCommand]
    async Task OpenProjectAsync()
    {
        if (FilePicker is null) return;
        Error = Status = null;
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

    [RelayCommand]
    async Task ImportLegacyProjectAsync()
    {
        if (FilePicker is null) return;
        Error = Status = null;
        try
        {
            string? ptp = await FilePicker.PickLegacyProjectAsync();
            if (ptp is null) return;

            // Import into memory first: a failed import must not leave a half-written package.
            var buffer = new MemoryStream();
            var result = await Task.Run(() => PtpImporter.Import(ptp, buffer));

            await using var output = await FilePicker.PickPackageSaveAsync(
                Path.GetFileNameWithoutExtension(ptp) + PxPackageFormat.Extension);
            if (output is null) return;
            buffer.Position = 0;
            await buffer.CopyToAsync(output);
            await output.FlushAsync();

            buffer.Position = 0;
            using var pkg = PxPackage.Open(buffer, leaveOpen: true);
            Project = ProjectSummary.From(pkg);
            Status = result.MissingMedia.Count == 0
                ? Strings.Import_Done
                : string.Format(Strings.Import_DoneMissing, result.MissingMedia.Count,
                    string.Join(", ", result.MissingMedia.Select(Path.GetFileName)));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
                                       or System.Xml.XmlException or NotSupportedException)
        {
            Error = string.Format(Strings.Error_ImportFailed, ex.Message);
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
