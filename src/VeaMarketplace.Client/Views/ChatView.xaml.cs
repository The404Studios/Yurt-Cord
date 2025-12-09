using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class ChatView : UserControl
{
    private readonly ChatViewModel _viewModel;
    private readonly IChatService _chatService;
    private DateTime _lastTypingSent = DateTime.MinValue;

    public ChatView()
    {
        InitializeComponent();

        _viewModel = (ChatViewModel)App.ServiceProvider.GetService(typeof(ChatViewModel))!;
        _chatService = (IChatService)App.ServiceProvider.GetService(typeof(IChatService))!;

        DataContext = _viewModel;
        MessagesItemsControl.ItemsSource = _viewModel.Messages;

        // Subscribe to messages collection changes to auto-scroll
        _viewModel.Messages.CollectionChanged += (s, e) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                MessagesScrollViewer.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Background);
        };

        // Subscribe to typing indicator
        _chatService.OnUserTyping += (username, channel) =>
        {
            if (channel == _viewModel.CurrentChannel)
            {
                Dispatcher.Invoke(() =>
                {
                    TypingUserText.Text = $"{username} is typing...";
                    TypingIndicator.Visibility = Visibility.Visible;
                });

                // Hide after 3 seconds
                Task.Delay(3000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TypingIndicator.Visibility = Visibility.Collapsed;
                    });
                });
            }
        };

        // Update channel name when changed
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ChatViewModel.CurrentChannel))
            {
                Dispatcher.Invoke(() =>
                {
                    ChannelNameText.Text = _viewModel.CurrentChannel;
                });
            }
        };
    }

    private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            SendMessage();
        }
    }

    private async void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Send typing indicator (throttled)
        if (!string.IsNullOrEmpty(MessageTextBox.Text) &&
            (DateTime.Now - _lastTypingSent).TotalSeconds > 2)
        {
            _lastTypingSent = DateTime.Now;
            await _chatService.SendTypingAsync(_viewModel.CurrentChannel);
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private async void SendMessage()
    {
        var message = MessageTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(message)) return;

        MessageTextBox.Text = string.Empty;
        await _chatService.SendMessageAsync(message, _viewModel.CurrentChannel);
        MessageTextBox.Focus();
    }
}
