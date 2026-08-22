using LocalMind.Providers;
using LocalMind.Services;
using LocalMind.ViewModels;
using Microsoft.UI.Xaml;

namespace LocalMind;

public partial class App : Application
{
    private static MainWindow _window;

    internal static XamlRoot XamlRoot =>
        _window?.Content?.XamlRoot
        ?? throw new InvalidOperationException("The main window XAML root is not available.");

    internal static bool IsWindowVisible => _window?.AppWindow.IsVisible == true;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => AppLog.Error("Unhandled XAML exception.", e.Exception);

    internal static void ActivateExistingWindow() => _window?.ShowFromActivation();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLog.Info("Application launched.");
        _window = new MainWindow();

        var foundry = new FoundryLocalProvider();
        var ollama = new OllamaProvider();
        var llama = new LlamaCppProvider();
        var viewModel = new MainViewModel(foundry, ollama, llama);

        _window.SetViewModel(viewModel);
        _window.Activate();
        // Activate first so the tray icon is created, then hide.
        if (viewModel.Settings.StartMinimized)
        {
            _window.HideToTray();
        }
        viewModel.Initialize();
        _window.DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            NotificationService.Register);
    }
}

