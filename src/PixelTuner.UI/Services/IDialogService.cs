namespace PixelTuner.UI.Services;

public enum SaveChoice { Save, Discard, Cancel }

public interface IDialogService
{
    /// <summary>"Save changes to ...?" with Save, Don't save and Cancel.</summary>
    Task<SaveChoice> AskSaveChangesAsync(string documentName);

    Task ShowAboutAsync(string appName, string version);
}
