using System.Collections.Specialized;
using System.ComponentModel;
using LocalMind.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace LocalMind.Views;

public sealed partial class ChatView : UserControl
{
    private INotifyCollectionChanged? _messages;
    private readonly List<ChatMessageVM> _observedMessages = [];

    public ChatView()
    {
        InitializeComponent();
        InputBox.AddHandler(KeyDownEvent, new KeyEventHandler(InputBox_KeyDown), true);
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => ScrollToBottom();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_messages is not null)
            _messages.CollectionChanged -= OnMessagesChanged;
        foreach (var message in _observedMessages)
            message.PropertyChanged -= OnMessagePropertyChanged;
        _observedMessages.Clear();

        if (DataContext is ChatViewModel vm)
        {
            _messages = vm.Messages;
            _messages.CollectionChanged += OnMessagesChanged;
            foreach (var message in vm.Messages)
                Observe(message);
            ScrollToBottom();
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (ChatMessageVM message in e.NewItems)
                Observe(message);
        ScrollToBottom();
    }

    private void Observe(ChatMessageVM message)
    {
        _observedMessages.Add(message);
        message.PropertyChanged += OnMessagePropertyChanged;
    }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatMessageVM.Text))
            ScrollToBottom();
    }

    private void ScrollToBottom()
        => DispatcherQueue.TryEnqueue(() =>
        {
            MessagesScroll.UpdateLayout();
            MessagesScroll.ChangeView(null, MessagesScroll.ScrollableHeight, null, disableAnimation: true);
        });

    private async void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ChatMessageVM message } button)
            return;

        var package = new DataPackage();
        package.SetText(message.Text);
        Clipboard.SetContent(package);

        if (button.Content is FontIcon icon)
        {
            icon.Glyph = "\uE73E"; // checkmark
            await Task.Delay(1500);
            icon.Glyph = "\uE8C8"; // copy
        }
    }

    private void Regenerate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChatViewModel vm)
            vm.RegenerateCommand.Execute(null);
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;

        var shiftDown = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
        if (shiftDown)
        {
            InputBox.SelectedText = Environment.NewLine;
            e.Handled = true;
            return;
        }

        e.Handled = true;
        if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
            vm.SendCommand.Execute(null);
    }
}
