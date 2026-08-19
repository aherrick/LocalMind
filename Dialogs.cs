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
}
