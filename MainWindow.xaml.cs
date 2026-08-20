using CommunityToolkit.Mvvm.Input;
using LocalMind.Services;
using LocalMind.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace LocalMind;

public sealed partial class MainWindow : WinUIEx.WindowEx
{
    private bool _isVisible = true;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "localmind.ico"));
        AppWindow.Closing += OnClosing;
    }

    public bool IsVisibleToUser => _isVisible;
    public string AppVersion => $"LocalMind v{AppInfo.Version}";

    public void SetViewModel(MainViewModel viewModel)
    {
        Root.DataContext = viewModel;
        ApplyTheme(viewModel.Settings.Theme);
        viewModel.Settings.ThemeChanged += ApplyTheme;

        // The native tray menu reads IsChecked when it builds its items, so keep it current.
        TrayRunAtStartup.IsChecked = viewModel.Settings.RunAtStartup;
        viewModel.Settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.RunAtStartup))
            {
                TrayRunAtStartup.IsChecked = viewModel.Settings.RunAtStartup;
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

    public void HideToTray()
    {
        H.NotifyIcon.WindowExtensions.Hide(this);
        _isVisible = false;
    }

    private void OnClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_forceClose)
        {
            return;
        }

        args.Cancel = true;
        H.NotifyIcon.WindowExtensions.Hide(this);
        _isVisible = false;
    }

    [RelayCommand]
    private void ShowFromTray()
        // The flyout still owns focus until this click finishes; restore on the next tick.
        => OnUiThread(() =>
        {
            TrayIcon.CloseContextMenu();
            H.NotifyIcon.WindowExtensions.Show(this);
            _isVisible = true;
            Activate();
            this.BringToFront();
        });

    private async void DeleteChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChatViewModel chat } && Root.DataContext is MainViewModel vm
            && await Dialogs.Confirm(Content.XamlRoot, "Delete chat?", "This can't be undone."))
        {
            vm.DeleteChatCommand.Execute(chat);
        }
    }

    private void PinChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChatViewModel chat } && Root.DataContext is MainViewModel vm)
        {
            vm.TogglePinCommand.Execute(chat);
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
        if (sender is FrameworkElement { DataContext: ChatViewModel chat }
            && await Dialogs.Prompt(Content.XamlRoot, "Rename chat", chat.Title) is { } title)
        {
            chat.Rename(title);
        }
    }

    private async void ExportChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ChatViewModel chat } || Root.DataContext is not MainViewModel vm)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = chat.Title,
        };
        picker.FileTypeChoices.Add("LocalMind conversation", [".json"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            await vm.ExportChat(chat, file.Path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Chat export failed.", ex);
        }
    }

    private void ChatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView { SelectedItem: ChatViewModel chat } list || Root.DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.SelectedChat = chat;
        // The two lists share one SelectedChat; clear the sibling so only one row stays highlighted.
        var sibling = ReferenceEquals(list, PinnedList) ? ChatList : PinnedList;
        sibling.SelectedItem = null;
    }

    [RelayCommand]
    private void NewChatFromTray()
        => OnUiThread(() =>
        {
            if (Root.DataContext is MainViewModel vm)
            {
                vm.NewChatCommand.Execute(null);
                ShowFromTray();
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
            if (Root.DataContext is MainViewModel vm)
            {
                vm.Settings.RunAtStartup = !vm.Settings.RunAtStartup;
            }
        });

    [RelayCommand]
    private void CheckForUpdatesFromTray()
        => OnUiThread(() =>
        {
            if (Root.DataContext is MainViewModel vm)
            {
                vm.CheckForUpdatesCommand.Execute(null);
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

