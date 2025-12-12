using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class ChatView : UserControl
{
    private readonly ChatViewModel? _viewModel;
    private readonly IChatService? _chatService;
    private DateTime _lastTypingSent = DateTime.MinValue;

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

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

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

        // Subscribe to typing indicator - now handles multiple users
        _chatService.OnUserTyping += (username, channel) =>
        {
            if (channel == _viewModel.CurrentChannel)
            {
                Dispatcher.Invoke(() =>
                {
                    AddTypingUser(username);
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
                    // Clear typing users when switching channels
                    _typingUsers.Clear();
                    UpdateTypingIndicator();
                });
            }
        };
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
        if (_typingUsers.Count == 0)
        {
            TypingIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        var usernames = _typingUsers.Keys.ToList();
        string typingText;

        switch (usernames.Count)
        {
            case 1:
                typingText = $"{usernames[0]} is typing...";
                break;
            case 2:
                typingText = $"{usernames[0]} and {usernames[1]} are typing...";
                break;
            case 3:
                typingText = $"{usernames[0]}, {usernames[1]}, and {usernames[2]} are typing...";
                break;
            default:
                var othersCount = usernames.Count - 2;
                typingText = $"{usernames[0]}, {usernames[1]}, and {othersCount} others are typing...";
                break;
        }

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

        var message = MessageTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(message)) return;

        MessageTextBox.Text = string.Empty;
        await _chatService.SendMessageAsync(message, _viewModel.CurrentChannel);
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
}
