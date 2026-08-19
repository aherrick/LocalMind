using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LocalMind.ViewModels;
using Microsoft.UI.Xaml;

namespace LocalMind
{
    public sealed partial class MainWindow : WinUIEx.WindowEx
    {
        private bool _isVisible = true;
        private bool _forceClose;

        public MainWindow()
        {
            InitializeComponent();
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
                return;

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
                && await Dialogs.ConfirmAsync(Content.XamlRoot, "Delete chat?", "This can't be undone."))
                vm.DeleteChatCommand.Execute(chat);
        }

        private void TrayOpen_Click(object sender, RoutedEventArgs e) => ShowFromTray();

        private void TrayExit_Click(object sender, RoutedEventArgs e)
        {
            _forceClose = true;
            TrayIcon.Dispose();
            Close();
        }
    }
}

