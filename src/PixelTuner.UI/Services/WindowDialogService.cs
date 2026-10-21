using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.Services;

/// <summary>
/// Small modal windows for the desktop apps, built in code. The buttons follow each system:
/// Windows puts Save, Don't save, Cancel left to right; macOS puts Don't save on the left and
/// the default Save on the right.
/// </summary>
public sealed class WindowDialogService(Window owner) : IDialogService
{
    public async Task<SaveChoice> AskSaveChangesAsync(string documentName)
    {
        var choice = SaveChoice.Cancel;
        var dialog = CreateDialog(string.Format(Strings.Dialog_SaveChanges, documentName), out var body, out var buttons);
        body.Children.Add(new TextBlock { Text = Strings.Dialog_SaveChangesDetail, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });

        Button Make(string text, SaveChoice result, bool isDefault = false, bool isCancel = false)
        {
            var button = new Button { Content = text, MinWidth = 96, IsDefault = isDefault, IsCancel = isCancel };
            if (isDefault) button.Classes.Add("accent");
            button.Click += (_, _) => { choice = result; dialog.Close(); };
            return button;
        }

        var save = Make(Strings.Dialog_Save, SaveChoice.Save, isDefault: true);
        var discard = Make(Strings.Dialog_DontSave, SaveChoice.Discard);
        var cancel = Make(Strings.Dialog_Cancel, SaveChoice.Cancel, isCancel: true);
        foreach (var button in OperatingSystem.IsMacOS() ? new[] { discard, cancel, save } : [save, discard, cancel])
            buttons.Children.Add(button);

        await dialog.ShowDialog(owner);
        return choice;
    }

    public async Task ShowAboutAsync(string appName, string version)
    {
        var dialog = CreateDialog(appName, out var body, out var buttons);
        body.Children.Add(new TextBlock { Text = string.Format(Strings.About_Version, version), Opacity = 0.75 });
        var ok = new Button { Content = Strings.Dialog_Close, MinWidth = 96, IsDefault = true, IsCancel = true };
        ok.Click += (_, _) => dialog.Close();
        buttons.Children.Add(ok);
        await dialog.ShowDialog(owner);
    }

    private static Window CreateDialog(string heading, out StackPanel body, out StackPanel buttons)
    {
        body = new StackPanel { Spacing = 8 };
        body.Children.Add(new TextBlock { Text = heading, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

        var layout = new StackPanel { Spacing = 20, Margin = new Thickness(24) };
        layout.Children.Add(body);
        layout.Children.Add(buttons);

        return new Window
        {
            Title = Product.Current.Name,
            Content = layout,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
    }
}
