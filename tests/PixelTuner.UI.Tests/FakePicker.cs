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
    public string? SuggestedName { get; private set; }
    public int SaveDialogs { get; private set; }

    public Task<Stream?> PickPackageAsync() => Task.FromResult(OpenStream);
    public Task<string?> PickPackagePathAsync() => Task.FromResult(OpenPath);
    public Task<string?> PickLegacyProjectAsync() => Task.FromResult(LegacyPath);
    public Task<string?> PickAudioPathAsync() => Task.FromResult(AudioPath);

    public Task<string?> PickPackageSavePathAsync(string suggestedName)
    {
        SuggestedName = suggestedName;
        SaveDialogs++;
        return Task.FromResult(SavePath);
    }
}

internal sealed class FakeDialogs(SaveChoice answer) : IDialogService
{
    public int Asked { get; private set; }
    public string? AskedAbout { get; private set; }

    public Task<SaveChoice> AskSaveChangesAsync(string documentName)
    {
        Asked++;
        AskedAbout = documentName;
        return Task.FromResult(answer);
    }

    public Task ShowAboutAsync(string appName, string version) => Task.CompletedTask;
}
