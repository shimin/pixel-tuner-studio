using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PixelTuner.Package;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.Services;

public interface IFilePicker
{
    /// <summary>Asks the user for a .pxpkg and returns it as a seekable stream, or null if cancelled.</summary>
    Task<Stream?> PickPackageAsync();

    /// <summary>Asks the user for a PixelTuner 2 project (.ptp). Returns its path on disk, or null if cancelled.</summary>
    Task<string?> PickLegacyProjectAsync();

    /// <summary>Asks where to save the imported package. Returns a stream to write it to, or null if cancelled.</summary>
    Task<Stream?> PickPackageSaveAsync(string suggestedName);
}

/// <summary>
/// File picker through Avalonia's storage provider: the system dialog on Windows and
/// macOS, the document picker on iOS and the storage access framework on Android.
/// </summary>
public sealed class StorageFilePicker(TopLevel topLevel) : IFilePicker
{
    static readonly FilePickerFileType PackageType = new(Strings.FileType_Package)
    {
        Patterns = ["*" + PxPackageFormat.Extension],
        MimeTypes = [PxPackageFormat.MimeType, "application/zip", "application/octet-stream"],
        // Declared in the iOS and macOS Info.plist as an exported type.
        AppleUniformTypeIdentifiers = ["com.pixeltuner.pxpkg"],
    };

    static readonly FilePickerFileType LegacyType = new(Strings.FileType_LegacyProject)
    {
        Patterns = ["*.ptp"],
    };

    public async Task<string?> PickLegacyProjectAsync()
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [LegacyType, FilePickerFileTypes.All],
        });
        // The project's media sit next to it, so it has to be a real path.
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<Stream?> PickPackageSaveAsync(string suggestedName)
    {
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = PxPackageFormat.Extension.TrimStart('.'),
            FileTypeChoices = [PackageType],
            ShowOverwritePrompt = true,
        });
        return file is null ? null : await file.OpenWriteAsync();
    }

    public async Task<Stream?> PickPackageAsync()
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [PackageType, FilePickerFileTypes.All],
        });
        if (files.Count == 0) return null;

        await using var source = await files[0].OpenReadAsync();
        // Mobile pickers return forward-only streams; ZIP needs to seek.
        var copy = new MemoryStream();
        await source.CopyToAsync(copy);
        copy.Position = 0;
        return copy;
    }
}
