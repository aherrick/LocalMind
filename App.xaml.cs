using LocalMind.Providers;
using LocalMind.Services;
using LocalMind.ViewModels;
using Microsoft.UI.Xaml;

namespace LocalMind;

public partial class App : Application
{
    private MainWindow _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => AppLog.Error("Unhandled XAML exception.", e.Exception);

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLog.Info("Application launched.");
        _window = new MainWindow();

        var foundry = new FoundryLocalProvider();
        var ollama = new OllamaProvider();
        var store = new ChatStore();
        var notifications = new NotificationService();
        notifications.Register();
        var updates = new UpdateService();
        var settingsStore = new SettingsStore();

        var viewModel = new MainViewModel(
            foundry, ollama, store, notifications, updates, settingsStore,
            () => _window.IsVisibleToUser);

        _window.SetViewModel(viewModel);
        _window.Activate();
        // Activate first so the tray icon is created, then hide.
        if (viewModel.Settings.StartMinimized)
        {
            _window.HideToTray();
        }
        viewModel.Initialize();
    }
}

