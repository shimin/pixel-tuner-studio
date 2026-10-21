using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PixelTuner.Package;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.Services;

public interface IFilePicker
{
    /// <summary>Phones: asks for a .pxpkg and returns a copy as a seekable stream, or null if cancelled.</summary>
    Task<Stream?> PickPackageAsync();

    /// <summary>Desktop: asks for a .pxpkg and returns its path, or null if cancelled (or the file has no path).</summary>
    Task<string?> PickPackagePathAsync();

    /// <summary>Desktop: asks where to save a .pxpkg. Returns the path, or null if cancelled.</summary>
    Task<string?> PickPackageSavePathAsync(string suggestedName);

    /// <summary>Desktop: asks for the show's soundtrack. Returns its path, or null if cancelled.</summary>
    Task<string?> PickAudioPathAsync();

    /// <summary>Desktop: asks for a PixelTuner 2 project (.ptp). Returns its path, or null if cancelled.</summary>
    Task<string?> PickLegacyProjectAsync();
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

    static readonly FilePickerFileType AudioType = new(Strings.FileType_Audio)
    {
        Patterns = ["*.mp3", "*.wav", "*.ogg", "*.flac", "*.m4a", "*.aac", "*.wma", "*.opus"],
        MimeTypes = ["audio/*"],
        AppleUniformTypeIdentifiers = ["public.audio"],
    };

    public async Task<string?> PickAudioPathAsync()
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [AudioType, FilePickerFileTypes.All],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

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

    public async Task<string?> PickPackagePathAsync()
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [PackageType, FilePickerFileTypes.All],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> PickPackageSavePathAsync(string suggestedName)
    {
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = PxPackageFormat.Extension.TrimStart('.'),
            FileTypeChoices = [PackageType],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
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
