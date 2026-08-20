using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalMind;

public static class Dialogs
{
    public static async Task<bool> Confirm(XamlRoot root, string title, string content, string primaryText = "Delete")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task<string> Prompt(XamlRoot root, string title, string value, string primaryText = "Rename")
    {
        var textBox = new TextBox { Text = value, AcceptsReturn = false };
        textBox.Loaded += (_, _) => textBox.SelectAll();

        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var text = textBox.Text.Trim();
        return text.Length > 0 ? text : null;
    }
}
