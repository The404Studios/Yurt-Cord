using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Client.Views;

/// <summary>
/// Helper class for attachment preview in UI
/// </summary>
public class PendingAttachment
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeText => FormatFileSize(FileSize);
    public string? ThumbnailPath { get; set; }
    public MessageAttachmentDto? UploadedAttachment { get; set; }
    public bool IsUploaded { get; set; }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

public partial class ChatView : UserControl
{
    private readonly ChatViewModel? _viewModel;
    private readonly IChatService? _chatService;
    private readonly IFileUploadService? _fileUploadService;
    private readonly IApiService? _apiService;
    private DateTime _lastTypingSent = DateTime.MinValue;

    // Pending attachments for current message
    private readonly ObservableCollection<PendingAttachment> _pendingAttachments = new();

    // Multi-user typing tracking
    private readonly Dictionary<string, DateTime> _typingUsers = new();
    private readonly System.Windows.Threading.DispatcherTimer _typingCleanupTimer;
    private const int TypingTimeoutSeconds = 4;

    public ChatView()
    {
        InitializeComponent();

        // Initialize typing cleanup timer
        _typingCleanupTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _typingCleanupTimer.Tick += TypingCleanupTimer_Tick;

        // Ensure timer is stopped and events unsubscribed when control is unloaded
        Unloaded += OnUnloaded;

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ChatViewModel)App.ServiceProvider.GetService(typeof(ChatViewModel))!;
        _chatService = (IChatService)App.ServiceProvider.GetService(typeof(IChatService))!;
        _fileUploadService = (IFileUploadService?)App.ServiceProvider.GetService(typeof(IFileUploadService));
        _apiService = (IApiService?)App.ServiceProvider.GetService(typeof(IApiService));

        DataContext = _viewModel;
        MessagesItemsControl.ItemsSource = _viewModel.Messages;

        // Setup attachment preview list
        AttachmentPreviewList.ItemsSource = _pendingAttachments;
        _pendingAttachments.CollectionChanged += OnPendingAttachmentsChanged;

        // Subscribe to messages collection changes to auto-scroll
        _viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;

        // Subscribe to typing indicator - now handles multiple users
        _chatService.OnUserTyping += OnUserTyping;

        // Update channel name when changed
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop timer and cleanup event subscriptions
        _typingCleanupTimer.Stop();
        _typingCleanupTimer.Tick -= TypingCleanupTimer_Tick;

        if (_chatService != null)
        {
            _chatService.OnUserTyping -= OnUserTyping;
        }

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }

        _pendingAttachments.CollectionChanged -= OnPendingAttachmentsChanged;
    }

    private void OnPendingAttachmentsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        AttachmentPreviewArea.Visibility = _pendingAttachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        // Adjust input border corners when attachments are shown
        if (_pendingAttachments.Count > 0)
        {
            InputBorder.CornerRadius = new CornerRadius(0, 0, 8, 8);
        }
        else
        {
            InputBorder.CornerRadius = new CornerRadius(8);
        }
    }

    private void OnMessagesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            MessagesScrollViewer.ScrollToEnd();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnUserTyping(string username, string channel)
    {
        if (channel == _viewModel?.CurrentChannel)
        {
            Dispatcher.Invoke(() => AddTypingUser(username));
        }
    }

    private void OnViewModelPropertyChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.CurrentChannel))
        {
            Dispatcher.Invoke(() =>
            {
                ChannelNameText.Text = _viewModel?.CurrentChannel ?? string.Empty;
                // Clear typing users when switching channels
                _typingUsers.Clear();
                UpdateTypingIndicator();
            });
        }
    }

    #region Multi-User Typing Indicator

    private void AddTypingUser(string username)
    {
        // Don't show typing indicator for current user
        if (_viewModel?.CurrentChannel == null) return;

        _typingUsers[username] = DateTime.Now;
        UpdateTypingIndicator();

        // Start cleanup timer if not running
        if (!_typingCleanupTimer.IsEnabled)
        {
            _typingCleanupTimer.Start();
        }
    }

    private void TypingCleanupTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var expiredUsers = _typingUsers
            .Where(kvp => (now - kvp.Value).TotalSeconds > TypingTimeoutSeconds)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var user in expiredUsers)
        {
            _typingUsers.Remove(user);
        }

        UpdateTypingIndicator();

        // Stop timer if no one is typing
        if (_typingUsers.Count == 0)
        {
            _typingCleanupTimer.Stop();
        }
    }

    private void UpdateTypingIndicator()
    {
        // Take a snapshot of the typing users to avoid race conditions
        var usernames = _typingUsers.Keys.ToList();

        if (usernames.Count == 0)
        {
            TypingIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        string typingText = usernames.Count switch
        {
            1 => $"{usernames[0]} is typing...",
            2 => $"{usernames[0]} and {usernames[1]} are typing...",
            3 => $"{usernames[0]}, {usernames[1]}, and {usernames[2]} are typing...",
            _ => $"{usernames[0]}, {usernames[1]}, and {usernames.Count - 2} others are typing..."
        };

        TypingUserText.Text = typingText;
        TypingIndicator.Visibility = Visibility.Visible;
    }

    #endregion

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
        if (_chatService == null || _viewModel == null) return;

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
        if (_chatService == null || _viewModel == null) return;

        var message = MessageTextBox.Text?.Trim() ?? string.Empty;
        var hasAttachments = _pendingAttachments.Count > 0;

        // If no message and no attachments, don't send
        if (string.IsNullOrEmpty(message) && !hasAttachments) return;

        MessageTextBox.Text = string.Empty;

        // Upload attachments if any
        if (hasAttachments)
        {
            var uploadedAttachments = await UploadPendingAttachmentsAsync();
            if (uploadedAttachments.Count > 0)
            {
                await _chatService.SendMessageWithAttachmentsAsync(message, _viewModel.CurrentChannel, uploadedAttachments);
            }
            _pendingAttachments.Clear();
        }
        else
        {
            await _chatService.SendMessageAsync(message, _viewModel.CurrentChannel);
        }

        MessageTextBox.Focus();
    }

    #region Emoji Picker

    private readonly Dictionary<string, string[]> _emojiCategories = new()
    {
        ["Smileys"] = new[] { "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂", "🙂", "🙃", "😉", "😊", "😇", "🥰", "😍", "🤩", "😘", "😗", "☺", "😚", "😙", "🥲", "😋", "😛", "😜", "🤪", "😝", "🤑", "🤗", "🤭", "🤫", "🤔", "🤐", "🤨", "😐", "😑", "😶", "😏", "😒", "🙄", "😬", "🤥", "😌", "😔", "😪", "🤤", "😴", "😷", "🤒", "🤕", "🤢", "🤮", "🥴", "😵", "🤯", "🤠", "🥳", "🥸", "😎", "🤓", "🧐" },
        ["Gestures"] = new[] { "👍", "👎", "👌", "🤌", "🤏", "✌", "🤞", "🤟", "🤘", "🤙", "👈", "👉", "👆", "🖕", "👇", "☝", "👋", "🤚", "🖐", "✋", "🖖", "👏", "🙌", "🤲", "🤝", "🙏", "✍", "💪", "🦾", "🦿", "🦵", "🦶", "👂", "🦻", "👃", "🧠", "🫀", "🫁", "🦷", "🦴", "👀", "👁", "👅", "👄" },
        ["Symbols"] = new[] { "❤", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎", "💔", "❣", "💕", "💞", "💓", "💗", "💖", "💘", "💝", "💟", "☮", "✝", "☪", "🕉", "☸", "✡", "🔯", "🕎", "☯", "☦", "🛐", "⛎", "♈", "♉", "♊", "♋", "♌", "♍", "♎", "♏", "♐", "♑", "♒", "♓", "🆔", "⚛", "🉑", "☢", "☣", "📴", "📳", "🈶", "🈚", "🈸", "🈺", "🈷", "✴", "🆚", "💮", "🉐", "㊙", "㊗" },
        ["Animals"] = new[] { "🐱", "🐶", "🐭", "🐹", "🐰", "🦊", "🐻", "🐼", "🐻‍❄️", "🐨", "🐯", "🦁", "🐮", "🐷", "🐽", "🐸", "🐵", "🙈", "🙉", "🙊", "🐒", "🐔", "🐧", "🐦", "🐤", "🐣", "🐥", "🦆", "🦅", "🦉", "🦇", "🐺", "🐗", "🐴", "🦄", "🐝", "🪱", "🐛", "🦋", "🐌", "🐞", "🐜", "🪰", "🪲", "🪳", "🦟", "🦗", "🕷", "🕸", "🦂", "🐢", "🐍", "🦎", "🦖", "🦕", "🐙", "🦑", "🦐", "🦞", "🦀", "🐡", "🐠", "🐟", "🐬", "🐳", "🐋", "🦈", "🐊", "🐅", "🐆", "🦓", "🦍", "🦧", "🦣", "🐘", "🦛", "🦏", "🐪", "🐫", "🦒", "🦘", "🦬", "🐃", "🐂", "🐄", "🐎", "🐖", "🐏", "🐑", "🦙", "🐐", "🦌", "🐕", "🐩", "🦮", "🐕‍🦺", "🐈", "🐈‍⬛", "🪶", "🐓", "🦃", "🦤", "🦚", "🦜", "🦢", "🦩", "🕊", "🐇", "🦝", "🦨", "🦡", "🦫", "🦦", "🦥", "🐁", "🐀", "🐿", "🦔" },
        ["Food"] = new[] { "🍕", "🍔", "🍟", "🌭", "🍿", "🧂", "🥓", "🥚", "🍳", "🧇", "🥞", "🧈", "🍞", "🥐", "🥖", "🥨", "🧀", "🥗", "🥙", "🥪", "🌮", "🌯", "🫔", "🥫", "🍝", "🍜", "🍲", "🍛", "🍣", "🍱", "🥟", "🦪", "🍤", "🍙", "🍚", "🍘", "🍥", "🥠", "🥮", "🍢", "🍡", "🍧", "🍨", "🍦", "🥧", "🧁", "🍰", "🎂", "🍮", "🍭", "🍬", "🍫", "🍿", "🍩", "🍪", "🌰", "🥜", "🍯", "🥛", "🍼", "🫖", "☕", "🍵", "🧃", "🥤", "🧋", "🍶", "🍺", "🍻", "🥂", "🍷", "🥃", "🍸", "🍹", "🧉", "🍾", "🧊", "🥄", "🍴", "🍽", "🥣", "🥡", "🥢", "🧆" },
        ["Activities"] = new[] { "⚽", "🏀", "🏈", "⚾", "🥎", "🎾", "🏐", "🏉", "🥏", "🎱", "🪀", "🏓", "🏸", "🏒", "🏑", "🥍", "🏏", "🪃", "🥅", "⛳", "🪁", "🏹", "🎣", "🤿", "🥊", "🥋", "🎽", "🛹", "🛼", "🛷", "⛸", "🥌", "🎿", "⛷", "🏂", "🪂", "🏋", "🤼", "🤸", "🤺", "⛹", "🤾", "🏌", "🏇", "🧘", "🏄", "🏊", "🤽", "🚣", "🧗", "🚵", "🚴", "🏆", "🥇", "🥈", "🥉", "🏅", "🎖", "🏵", "🎗", "🎫", "🎟", "🎪", "🤹", "🎭", "🩰", "🎨", "🎬", "🎤", "🎧", "🎼", "🎹", "🥁", "🪘", "🎷", "🎺", "🪗", "🎸", "🪕", "🎻", "🎲", "♟", "🎯", "🎳", "🎮", "🎰", "🧩" },
        ["Travel"] = new[] { "🚗", "🚕", "🚙", "🚌", "🚎", "🏎", "🚓", "🚑", "🚒", "🚐", "🛻", "🚚", "🚛", "🚜", "🦯", "🦽", "🦼", "🛴", "🚲", "🛵", "🏍", "🛺", "🚨", "🚔", "🚍", "🚘", "🚖", "🚡", "🚠", "🚟", "🚃", "🚋", "🚞", "🚝", "🚄", "🚅", "🚈", "🚂", "🚆", "🚇", "🚊", "🚉", "✈", "🛫", "🛬", "🛩", "💺", "🛰", "🚀", "🛸", "🚁", "🛶", "⛵", "🚤", "🛥", "🛳", "⛴", "🚢", "⚓", "🪝", "⛽", "🚧", "🚦", "🚥", "🚏", "🗺", "🗿", "🗽", "🗼", "🏰", "🏯", "🏟", "🎡", "🎢", "🎠", "⛲", "⛱", "🏖", "🏝", "🏜", "🌋", "⛰", "🏔", "🗻", "🏕", "⛺", "🛖", "🏠", "🏡", "🏘", "🏚", "🏗", "🏭", "🏢", "🏬", "🏣", "🏤", "🏥", "🏦", "🏨", "🏪", "🏫", "🏩", "💒", "🏛", "⛪", "🕌", "🕍", "🛕", "🕋", "⛩", "🛤", "🛣", "🗾", "🎑", "🏞", "🌅", "🌄", "🌠", "🎇", "🎆", "🌇", "🌆", "🏙", "🌃", "🌌", "🌉", "🌁" },
        ["Objects"] = new[] { "💡", "🔦", "🏮", "🪔", "📱", "📲", "💻", "🖥", "🖨", "⌨", "🖱", "🖲", "💽", "💾", "💿", "📀", "🧮", "🎥", "🎞", "📽", "🎬", "📺", "📷", "📸", "📹", "📼", "🔍", "🔎", "🕯", "💡", "🔦", "🏮", "🪔", "📔", "📕", "📖", "📗", "📘", "📙", "📚", "📓", "📒", "📃", "📜", "📄", "📰", "🗞", "📑", "🔖", "🏷", "💰", "🪙", "💴", "💵", "💶", "💷", "💸", "💳", "🧾", "💹", "✉", "📧", "📨", "📩", "📤", "📥", "📦", "📫", "📪", "📬", "📭", "📮", "🗳", "✏", "✒", "🖋", "🖊", "🖌", "🖍", "📝", "💼", "📁", "📂", "🗂", "📅", "📆", "🗒", "🗓", "📇", "📈", "📉", "📊", "📋", "📌", "📍", "📎", "🖇", "📏", "📐", "✂", "🗃", "🗄", "🗑", "🔒", "🔓", "🔏", "🔐", "🔑", "🗝", "🔨", "🪓", "⛏", "⚒", "🛠", "🗡", "⚔", "🔫", "🪃", "🏹", "🛡", "🪚", "🔧", "🪛", "🔩", "⚙", "🗜", "⚖", "🦯", "🔗", "⛓", "🪝", "🧰", "🧲", "🪜", "⚗", "🧪", "🧫", "🧬", "🔬", "🔭", "📡", "💉", "🩸", "💊", "🩹", "🩺", "🚪", "🛗", "🪞", "🪟", "🛏", "🛋", "🪑", "🚽", "🪠", "🚿", "🛁", "🪤", "🪒", "🧴", "🧷", "🧹", "🧺", "🧻", "🪣", "🧼", "🪥", "🧽", "🧯", "🛒", "🚬", "⚰", "🪦", "⚱", "🗿", "🪧", "🏧" }
    };

    private string _currentEmojiCategory = "Smileys";

    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
        if (EmojiPopup.IsOpen)
        {
            LoadEmojiCategory(_currentEmojiCategory);
        }
    }

    private void LoadEmojiCategory(string category)
    {
        _currentEmojiCategory = category;
        if (_emojiCategories.TryGetValue(category, out var emojis))
        {
            EmojiGrid.ItemsSource = emojis;
        }
    }

    private void EmojiCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string category)
        {
            LoadEmojiCategory(category);
        }
    }

    private void Emoji_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string emoji)
        {
            // Insert emoji at cursor position
            var caretIndex = MessageTextBox.CaretIndex;
            MessageTextBox.Text = MessageTextBox.Text.Insert(caretIndex, emoji);
            MessageTextBox.CaretIndex = caretIndex + emoji.Length;
            MessageTextBox.Focus();
        }
    }

    private void EmojiSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = EmojiSearchBox.Text?.ToLower() ?? "";
        EmojiSearchPlaceholder.Visibility = string.IsNullOrEmpty(query)
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (string.IsNullOrEmpty(query))
        {
            LoadEmojiCategory(_currentEmojiCategory);
            return;
        }

        // Search all categories
        var results = _emojiCategories.Values
            .SelectMany(e => e)
            .Distinct()
            .Take(50)
            .ToArray();
        EmojiGrid.ItemsSource = results;
    }

    #endregion

    #region Attachments

    private void AddAttachment_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "All Files (*.*)|*.*|Images (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp|Videos (*.mp4;*.webm;*.mov)|*.mp4;*.webm;*.mov|Documents (*.pdf;*.doc;*.docx;*.xls;*.xlsx)|*.pdf;*.doc;*.docx;*.xls;*.xlsx",
            Title = "Select files to attach"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var filePath in dialog.FileNames)
            {
                AddAttachmentFile(filePath);
            }
        }
    }

    private void AddAttachmentFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        // Check file size (max 50MB)
        if (fileInfo.Length > 50 * 1024 * 1024)
        {
            MessageBox.Show($"File '{fileInfo.Name}' is too large. Maximum file size is 50MB.",
                "File Too Large", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Create thumbnail for images
        string? thumbnailPath = null;
        var extension = fileInfo.Extension.ToLowerInvariant();
        if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp")
        {
            thumbnailPath = filePath; // Use the file itself as thumbnail for images
        }

        var attachment = new PendingAttachment
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            FileSize = fileInfo.Length,
            ThumbnailPath = thumbnailPath
        };

        _pendingAttachments.Add(attachment);
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PendingAttachment attachment)
        {
            _pendingAttachments.Remove(attachment);
        }
    }

    private async Task<List<MessageAttachmentDto>> UploadPendingAttachmentsAsync()
    {
        var uploadedAttachments = new List<MessageAttachmentDto>();

        if (_fileUploadService == null || _apiService?.AuthToken == null)
            return uploadedAttachments;

        UploadProgressPanel.Visibility = Visibility.Visible;
        var total = _pendingAttachments.Count;
        var current = 0;

        foreach (var pending in _pendingAttachments.ToList())
        {
            if (pending.IsUploaded && pending.UploadedAttachment != null)
            {
                uploadedAttachments.Add(pending.UploadedAttachment);
                continue;
            }

            current++;
            UploadProgressBar.Value = (current * 100.0) / total;

            var result = await _fileUploadService.UploadAttachmentAsync(pending.FilePath, _apiService.AuthToken);
            if (result.Success && result.FileId != null)
            {
                var attachment = new MessageAttachmentDto
                {
                    Id = result.FileId,
                    FileName = result.FileName ?? pending.FileName,
                    FileUrl = result.FileUrl ?? string.Empty,
                    ThumbnailUrl = result.ThumbnailUrl,
                    FileSize = result.FileSize,
                    MimeType = result.MimeType,
                    Type = result.FileType,
                    Width = result.Width,
                    Height = result.Height,
                    UploadedAt = DateTime.UtcNow
                };

                pending.UploadedAttachment = attachment;
                pending.IsUploaded = true;
                uploadedAttachments.Add(attachment);
            }
            else
            {
                MessageBox.Show($"Failed to upload '{pending.FileName}': {result.Message}",
                    "Upload Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        UploadProgressPanel.Visibility = Visibility.Collapsed;
        return uploadedAttachments;
    }

    #endregion

    #region Connection Status

    private async void ConnectionRetry_Click(object sender, RoutedEventArgs e)
    {
        if (_chatService == null || _apiService?.AuthToken == null) return;

        // Show reconnecting state
        ConnectionBanner.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(250, 168, 26)); // Orange
        ConnectionBannerText.Text = "Reconnecting to chat...";
        ConnectionSpinner.Visibility = Visibility.Visible;
        ConnectionIcon.Visibility = Visibility.Collapsed;
        ConnectionRetryButton.Visibility = Visibility.Collapsed;

        try
        {
            await _chatService.ConnectAsync(_apiService.AuthToken);
            // Hide banner on success
            ConnectionBanner.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            // Show error state
            ConnectionBanner.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(237, 66, 69)); // Red
            ConnectionBannerText.Text = "Unable to connect. Check your connection.";
            ConnectionSpinner.Visibility = Visibility.Collapsed;
            ConnectionIcon.Text = "⚠";
            ConnectionIcon.Visibility = Visibility.Visible;
            ConnectionRetryButton.Visibility = Visibility.Visible;
        }
    }

    public void UpdateOnlineCount(int count)
    {
        Dispatcher.Invoke(() =>
        {
            OnlineCountText.Text = $"{count} online";
        });
    }

    #endregion
}
