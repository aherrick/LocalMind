using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalMind;

public static class Dialogs
{
    public static async Task<bool> Confirm(XamlRoot root, string title, string content, string primaryText = "Delete")
    {
        var dialog = Create(root, title, content);
        dialog.PrimaryButtonText = primaryText;
        dialog.CloseButtonText = "Cancel";
        dialog.DefaultButton = ContentDialogButton.Close;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task Message(XamlRoot root, string title, string content)
    {
        var dialog = Create(root, title, content);
        dialog.CloseButtonText = "OK";
        dialog.DefaultButton = ContentDialogButton.Close;
        await dialog.ShowAsync();
    }

    public static async Task<string> Prompt(XamlRoot root, string title, string value, string primaryText = "Rename")
    {
        var textBox = new TextBox { Text = value, AcceptsReturn = false };
        textBox.Loaded += (_, _) => textBox.SelectAll();

        var dialog = Create(root, title, textBox);
        dialog.PrimaryButtonText = primaryText;
        dialog.CloseButtonText = "Cancel";
        dialog.DefaultButton = ContentDialogButton.Primary;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var text = textBox.Text.Trim();
        return text.Length > 0 ? text : null;
    }

    private static ContentDialog Create(XamlRoot root, string title, object content)
        => new()
        {
            Title = title,
            Content = content,
            XamlRoot = root,
        };
}
