using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PixelTuner.Package;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.Services;

public interface IFilePicker
{
    /// <summary>Asks the user for a .pxpkg and returns it as a seekable stream, or null if cancelled.</summary>
    Task<Stream?> PickPackageAsync();
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
