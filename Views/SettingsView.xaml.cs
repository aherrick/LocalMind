using LocalMind.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalMind.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private async void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FoundryModelVM model }
            && await Dialogs.Confirm(XamlRoot, "Delete model?", $"{model.DisplayName} will be removed from this device."))
        {
            model.DeleteCommand.Execute(null);
        }
    }
}
