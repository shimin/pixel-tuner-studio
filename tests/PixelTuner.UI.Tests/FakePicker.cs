using PixelTuner.UI.Services;

namespace PixelTuner.UI.Tests;

/// <summary>Answers the file dialogs from a script; null means the user cancelled.</summary>
internal sealed class FakePicker : IFilePicker
{
    public string? OpenPath { get; set; }
    public string? SavePath { get; set; }
    public string? LegacyPath { get; set; }
    public Stream? OpenStream { get; set; }
    public string? AudioPath { get; set; }
    public string? ImagePath { get; set; }
    public string? GifPath { get; set; }
    public string? SvgPath { get; set; }
    public string? FirmwarePath { get; set; }
    public string? SuggestedName { get; private set; }
    public int SaveDialogs { get; private set; }

    public Task<Stream?> PickPackageAsync() => Task.FromResult(OpenStream);
    public Task<string?> PickPackagePathAsync() => Task.FromResult(OpenPath);
    public Task<string?> PickLegacyProjectAsync() => Task.FromResult(LegacyPath);
    public Task<string?> PickAudioPathAsync() => Task.FromResult(AudioPath);
    public Task<string?> PickImagePathAsync() => Task.FromResult(ImagePath);
    public Task<string?> PickGifPathAsync() => Task.FromResult(GifPath);
    public Task<string?> PickSvgPathAsync() => Task.FromResult(SvgPath);
    public Task<string?> PickFirmwarePathAsync() => Task.FromResult(FirmwarePath);

    public Task<string?> PickPackageSavePathAsync(string suggestedName)
    {
        SuggestedName = suggestedName;
        SaveDialogs++;
        return Task.FromResult(SavePath);
    }
}

internal sealed class FakeDialogs(SaveChoice answer, bool confirm = true) : IDialogService
{
    public int Asked { get; private set; }
    public List<(string Heading, string Detail, string Action)> Confirmed { get; } = [];

    public Task<bool> ConfirmAsync(string heading, string detail, string action)
    {
        Confirmed.Add((heading, detail, action));
        return Task.FromResult(confirm);
    }

    public string? AskedAbout { get; private set; }

    public Task<SaveChoice> AskSaveChangesAsync(string documentName)
    {
        Asked++;
        AskedAbout = documentName;
        return Task.FromResult(answer);
    }

    public Task ShowAboutAsync(string appName, string version) => Task.CompletedTask;
}
