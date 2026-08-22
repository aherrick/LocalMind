using CommunityToolkit.Mvvm.Input;
using LocalMind.Services;
using LocalMind.ViewModels;
using LocalMind.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;

namespace LocalMind;

public sealed partial class MainWindow : WinUIEx.WindowEx
{
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SetWindowIcon();
        AppWindow.Closing += OnClosing;
        Activated += OnFirstActivated;
    }

    // The taskbar button is created lazily on first show, so re-apply the icon once to avoid the
    // occasional default-icon race when the icon is only set in the constructor.
    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        SetWindowIcon();
    }

    private void SetWindowIcon()
        => AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "localmind.ico"));

    public string AppVersion => $"LocalMind v{AppInfo.Version}";

    private MainViewModel ViewModel => Root.DataContext as MainViewModel;

    private static ChatViewModel ChatOf(object sender) => (sender as FrameworkElement)?.DataContext as ChatViewModel;

    public void ShowFromActivation() => ShowFromTray();

    public void SetViewModel(MainViewModel viewModel)
    {
        Root.DataContext = viewModel;
        ApplyTheme(viewModel.Settings.Theme);

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsSettingsOpen)
                && viewModel.IsSettingsOpen)
            {
                if (SettingsHost.Content is null)
                {
                    SettingsHost.Content = new SettingsView { DataContext = viewModel.Settings };
                }
            }
        };

        // The native tray menu reads IsChecked when it builds its items, so keep it current.
        TrayRunAtStartup.IsChecked = viewModel.Settings.RunAtStartup;
        viewModel.Settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.RunAtStartup))
            {
                TrayRunAtStartup.IsChecked = viewModel.Settings.RunAtStartup;
            }
            else if (args.PropertyName == nameof(SettingsViewModel.Theme))
            {
                ApplyTheme(viewModel.Settings.Theme);
            }
        };
    }

    private void ApplyTheme(string theme)
        => Root.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

    public void HideToTray() => H.NotifyIcon.WindowExtensions.Hide(this);

    private void OnClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_forceClose)
        {
            return;
        }

        var minimizeToTray = ViewModel?.Settings.MinimizeToTrayOnClose ?? true;
        if (minimizeToTray)
        {
            args.Cancel = true;
            HideToTray();
        }
        else
        {
            TrayIcon.Dispose();
        }
    }

    [RelayCommand]
    private void ShowFromTray()
        // The flyout still owns focus until this click finishes; restore on the next tick.
        => OnUiThread(ShowFromTrayCore);

    private void ShowFromTrayCore()
    {
        TrayIcon.CloseContextMenu();
        if (AppWindow.Presenter is OverlappedPresenter
            {
                State: OverlappedPresenterState.Minimized,
            } presenter)
        {
            presenter.Restore(false);
        }
        H.NotifyIcon.EfficiencyMode.EfficiencyModeUtilities.SetEfficiencyMode(false);
        AppWindow.Show(true);
        this.BringToFront();
    }

    private async void DeleteChat_Click(object sender, RoutedEventArgs e)
    {
        if (ChatOf(sender) is { } chat && ViewModel is { } vm
            && await Dialogs.Confirm("Delete chat?", "This can't be undone."))
        {
            vm.DeleteChatCommand.Execute(chat);
        }
    }

    private void PinChat_Click(object sender, RoutedEventArgs e)
    {
        if (ChatOf(sender) is { } chat)
        {
            ViewModel?.TogglePinCommand.Execute(chat);
        }
    }

    // Flyout items don't reliably inherit the row's DataContext, so set it from the flyout target.
    private void ChatMenu_Opening(object sender, object e)
    {
        if (sender is MenuFlyout { Target.DataContext: ChatViewModel chat } flyout)
        {
            foreach (var item in flyout.Items)
            {
                item.DataContext = chat;
            }
        }
    }

    private async void RenameChat_Click(object sender, RoutedEventArgs e)
    {
        if (ChatOf(sender) is { } chat
            && await Dialogs.Prompt("Rename chat", chat.Title) is { } title)
        {
            chat.Rename(title);
        }
    }

    private async void ExportChat_Click(object sender, RoutedEventArgs e)
    {
        if (ChatOf(sender) is not { } chat)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = chat.Title,
        };
        picker.FileTypeChoices.Add("LocalMind conversation", [".json"]);
        picker.FileTypeChoices.Add("Markdown document", [".md"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            await chat.Export(file.Path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Chat export failed.", ex);
        }
    }

    private void ChatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView { SelectedItem: ChatViewModel chat } list || ViewModel is not { } vm)
        {
            return;
        }

        vm.SelectedChat = chat;
        // The two lists share one SelectedChat; clear the sibling so only one row stays highlighted.
        var sibling = ReferenceEquals(list, PinnedList) ? ChatList : PinnedList;
        sibling.SelectedItem = null;
    }

    private void NewChat_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var command = ViewModel?.NewChatCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            args.Handled = true;
        }
    }

    private void SearchChats_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        vm.IsSettingsOpen = false;
        DispatcherQueue.TryEnqueue(() => ChatSearchBox.Focus(FocusState.Keyboard));
        args.Handled = true;
    }

    [RelayCommand]
    private void NewChatFromTray()
        => OnUiThread(() =>
        {
            if (ViewModel is { } vm)
            {
                vm.NewChatCommand.Execute(null);
                ShowFromTrayCore();
            }
        });

    [RelayCommand]
    private void OpenRepository() => AppInfo.OpenRepository();

    [RelayCommand]
    private void OpenLogs() => AppLog.OpenDirectory();

    [RelayCommand]
    private void ToggleRunAtStartup()
        => OnUiThread(() =>
        {
            if (ViewModel is { } vm)
            {
                vm.Settings.RunAtStartup = !vm.Settings.RunAtStartup;
            }
        });

    [RelayCommand]
    private void CheckForUpdatesFromTray()
        => OnUiThread(() =>
        {
            // Surface the window first so the result dialog is visible when launched from the tray.
            ShowFromTrayCore();
            if (ViewModel is not { } vm)
            {
                return;
            }

            vm.IsSettingsOpen = true;
            if (vm.OpenSettingsCommand.CanExecute(null))
            {
                vm.OpenSettingsCommand.Execute(null);
            }

            var command = vm.Settings.CheckForUpdatesCommand;
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        });

    [RelayCommand]
    private void ExitApp()
        => OnUiThread(() =>
        {
            _forceClose = true;
            TrayIcon.Dispose();
            Close();
        });

    // Native tray menu items are invoked from the tray icon's own message loop.
    private void OnUiThread(Action action) => DispatcherQueue.TryEnqueue(() => action());
}

