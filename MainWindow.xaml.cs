using System.Windows.Input;
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
        ShowFromTrayCommand = new RelayCommand(ShowFromTray);
        AppWindow.Closing += OnClosing;
    }

    public ICommand ShowFromTrayCommand { get; }

    public bool IsVisibleToUser => _isVisible;

    public void SetViewModel(MainViewModel viewModel)
    {
        Root.DataContext = viewModel;
        ApplyTheme(viewModel.Settings.Theme);
        viewModel.Settings.ThemeChanged += ApplyTheme;
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

    private void ShowFromTray()
    {
        TrayIcon.CloseContextMenu();
        // Flyout still owns focus until this click finishes; restore on the next tick.
        DispatcherQueue.TryEnqueue(() =>
        {
            H.NotifyIcon.WindowExtensions.Show(this);
            _isVisible = true;
            Activate();
            this.BringToFront();
        });
    }

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
        picker.FileTypeChoices.Add("LocalMind conversation", [".localmind-chat.json"]);
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

    private void TrayNewChat_Click(object sender, RoutedEventArgs e)
    {
        if (Root.DataContext is MainViewModel vm)
        {
            vm.NewChatCommand.Execute(null);
            ShowFromTray();
        }
    }

    private void TrayOpenLogs_Click(object sender, RoutedEventArgs e)
    {
        if (Root.DataContext is MainViewModel vm)
        {
            vm.Settings.OpenLogsCommand.Execute(null);
        }
    }

    private void TrayContextFlyout_Opening(object sender, object e)
    {
        if (Root.DataContext is MainViewModel vm)
        {
            TrayRunAtStartup.IsChecked = vm.Settings.RunAtStartup;
        }
    }

    private void TrayRunAtStartup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem { IsChecked: bool isChecked } && Root.DataContext is MainViewModel vm)
        {
            vm.Settings.RunAtStartup = isChecked;
        }
    }

    private void TrayCheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (Root.DataContext is MainViewModel vm)
        {
            vm.CheckForUpdatesCommand.Execute(null);
        }
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        TrayIcon.Dispose();
        Close();
    }
}

