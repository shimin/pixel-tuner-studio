namespace PixelTuner.UI.Services;

public enum SaveChoice { Save, Discard, Cancel }

public interface IDialogService
{
    /// <summary>"Save changes to ...?" with Save, Don't save and Cancel.</summary>
    Task<SaveChoice> AskSaveChangesAsync(string documentName);

    /// <summary>A question with a heading, an explanation, and the action to take or Cancel. True when the user took the action.</summary>
    Task<bool> ConfirmAsync(string heading, string detail, string action);

    Task ShowAboutAsync(string appName, string version);
}
