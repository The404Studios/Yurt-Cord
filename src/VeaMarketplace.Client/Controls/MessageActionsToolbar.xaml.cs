using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Controls;

public partial class MessageActionsToolbar : UserControl
{
    private string? _messageId;
    private string? _messageContent;
    private bool _isOwnMessage;
    private bool _canModerate;

    public event EventHandler<string>? ReactionRequested;
    public event EventHandler<string>? ReplyRequested;
    public event EventHandler<string>? EditRequested;
    public event EventHandler<string>? DeleteRequested;
    public event EventHandler<string>? PinRequested;
    public event EventHandler<string>? CopyRequested;
    public event EventHandler<string>? ReportRequested;

    public MessageActionsToolbar()
    {
        InitializeComponent();
    }

    public void ShowForMessage(string messageId, string messageContent, bool isOwnMessage, bool canModerate = false)
    {
        _messageId = messageId;
        _messageContent = messageContent;
        _isOwnMessage = isOwnMessage;
        _canModerate = canModerate;

        // Show/hide buttons based on context
        EditButton.Visibility = isOwnMessage ? Visibility.Visible : Visibility.Collapsed;
        PinButton.Visibility = canModerate ? Visibility.Visible : Visibility.Collapsed;

        Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    private void AddReactionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_messageId != null)
        {
            ReactionRequested?.Invoke(this, _messageId);
        }
    }

    private void ReplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_messageId != null)
        {
            ReplyRequested?.Invoke(this, _messageId);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_messageId != null && _isOwnMessage)
        {
            EditRequested?.Invoke(this, _messageId);
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_messageId != null)
        {
            PinRequested?.Invoke(this, _messageId);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_messageContent))
        {
            Clipboard.SetText(_messageContent);
            CopyRequested?.Invoke(this, _messageId ?? string.Empty);
        }
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        var contextMenu = new ContextMenu();

        if (!_isOwnMessage)
        {
            var reportItem = new MenuItem { Header = "Report Message" };
            reportItem.Click += (s, args) =>
            {
                if (_messageId != null)
                    ReportRequested?.Invoke(this, _messageId);
            };
            contextMenu.Items.Add(reportItem);
        }

        var copyIdItem = new MenuItem { Header = "Copy Message ID" };
        copyIdItem.Click += (s, args) =>
        {
            if (_messageId != null)
            {
                Clipboard.SetText(_messageId);
                var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
                toastService?.ShowInfo("Copied", "Message ID copied to clipboard");
            }
        };
        contextMenu.Items.Add(copyIdItem);

        var copyLinkItem = new MenuItem { Header = "Copy Message Link" };
        copyLinkItem.Click += (s, args) =>
        {
            if (_messageId != null)
            {
                Clipboard.SetText($"{AppConstants.UrlScheme}message/{_messageId}");
                var toastService = (IToastNotificationService?)App.ServiceProvider.GetService(typeof(IToastNotificationService));
                toastService?.ShowInfo("Copied", "Message link copied to clipboard");
            }
        };
        contextMenu.Items.Add(copyLinkItem);

        if (_isOwnMessage || _canModerate)
        {
            contextMenu.Items.Add(new Separator());

            var deleteItem = new MenuItem
            {
                Header = "Delete Message",
                Foreground = FindResource("AccentRedBrush") as System.Windows.Media.Brush
            };
            deleteItem.Click += (s, args) =>
            {
                if (_messageId != null)
                    DeleteRequested?.Invoke(this, _messageId);
            };
            contextMenu.Items.Add(deleteItem);
        }

        contextMenu.PlacementTarget = MoreButton;
        contextMenu.IsOpen = true;
    }
}
