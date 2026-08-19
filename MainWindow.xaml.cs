using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LocalMind.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        => Root.DataContext = viewModel;

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
        H.NotifyIcon.WindowExtensions.Show(this);
        _isVisible = true;
        Activate();
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

    private void TrayOpen_Click(object sender, RoutedEventArgs e) => ShowFromTray();

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        TrayIcon.Dispose();
        Close();
    }
}

