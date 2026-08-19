using LocalMind.Providers;
using LocalMind.Services;
using LocalMind.ViewModels;
using Microsoft.UI.Xaml;
using Velopack;

namespace LocalMind;

public partial class App : Application
{
    private MainWindow _window;

    public App()
    {
        VelopackApp.Build().Run();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        var foundry = new FoundryLocalProvider();
        var ollama = new OllamaProvider();
        var store = new ChatStore();
        var notifications = new NotificationService();
        notifications.Register();
        var updates = new UpdateService();

        var viewModel = new MainViewModel(
            foundry, ollama, store, notifications, updates,
            () => _window.IsVisibleToUser);

        _window.SetViewModel(viewModel);
        _window.Activate();
        _ = viewModel.Initialize();
    }
}

