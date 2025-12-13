using System.Windows;
using System.Windows.Controls;

namespace VeaMarketplace.Client.Controls;

public partial class ReactionPicker : UserControl
{
    private static readonly List<string> RecentlyUsed = ["👍", "❤️", "😂", "🔥", "👀", "💯"];

    private static readonly List<string> Smileys =
    [
        "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂",
        "🙂", "😊", "😇", "🥰", "😍", "🤩", "😘", "😗",
        "😚", "😋", "😛", "😜", "🤪", "😝", "🤑", "🤗",
        "🤭", "🤫", "🤔", "🤐", "🤨", "😐", "😑", "😶",
        "😏", "😒", "🙄", "😬", "🤥", "😌", "😔", "😪",
        "🤤", "😴", "😷", "🤒", "🤕", "🤢", "🤮", "🤧",
        "🥵", "🥶", "🥴", "😵", "🤯", "🤠", "🥳", "😎",
        "🤓", "🧐", "😕", "😟", "🙁", "😮", "😯", "😲",
        "😳", "🥺", "😦", "😧", "😨", "😰", "😥", "😢",
        "😭", "😱", "😖", "😣", "😞", "😓", "😩", "😫"
    ];

    private static readonly List<string> Nature =
    [
        "🐶", "🐱", "🐭", "🐹", "🐰", "🦊", "🐻", "🐼",
        "🐨", "🐯", "🦁", "🐮", "🐷", "🐸", "🐵", "🙈",
        "🐔", "🐧", "🐦", "🐤", "🦆", "🦅", "🦉", "🦇",
        "🐺", "🐗", "🐴", "🦄", "🐝", "🐛", "🦋", "🐌",
        "🌸", "💐", "🌹", "🥀", "🌺", "🌻", "🌼", "🌷",
        "🌱", "🌲", "🌳", "🌴", "🌵", "🌾", "🌿", "☘️"
    ];

    private static readonly List<string> Food =
    [
        "🍎", "🍐", "🍊", "🍋", "🍌", "🍉", "🍇", "🍓",
        "🍈", "🍒", "🍑", "🥭", "🍍", "🥥", "🥝", "🍅",
        "🍕", "🍔", "🍟", "🌭", "🍿", "🧂", "🥓", "🥚",
        "🍳", "🥞", "🧇", "🥐", "🍞", "🥖", "🥨", "🧀",
        "☕", "🍵", "🧃", "🥤", "🍶", "🍺", "🍻", "🥂",
        "🍷", "🥃", "🍸", "🍹", "🧊", "🍩", "🍪", "🎂"
    ];

    private static readonly List<string> Objects =
    [
        "⌚", "📱", "💻", "⌨️", "🖥️", "🖨️", "🖱️", "🖲️",
        "💾", "💿", "📀", "📼", "📷", "📹", "🎥", "📞",
        "☎️", "📺", "📻", "🎙️", "🎚️", "🎛️", "🧭", "⏱️",
        "⏲️", "⏰", "🕰️", "⌛", "⏳", "📡", "🔋", "🔌",
        "💡", "🔦", "🕯️", "🧯", "🛢️", "💸", "💵", "💴",
        "💶", "💷", "💰", "💳", "💎", "⚖️", "🔧", "🔨"
    ];

    private static readonly List<string> Symbols =
    [
        "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍",
        "🤎", "💔", "❣️", "💕", "💞", "💓", "💗", "💖",
        "💘", "💝", "💟", "☮️", "✝️", "☪️", "🕉️", "☸️",
        "✡️", "🔯", "🕎", "☯️", "☦️", "🛐", "⛎", "♈",
        "✅", "☑️", "✔️", "❌", "❎", "➕", "➖", "➗",
        "✖️", "♾️", "💲", "💱", "™️", "©️", "®️", "〰️",
        "🔴", "🟠", "🟡", "🟢", "🔵", "🟣", "⚫", "⚪"
    ];

    public event EventHandler<string>? EmojiSelected;

    public ReactionPicker()
    {
        InitializeComponent();
        LoadEmojis();
    }

    private void LoadEmojis()
    {
        RecentEmojis.ItemsSource = RecentlyUsed;
        SmileysEmojis.ItemsSource = Smileys;
        NatureEmojis.ItemsSource = Nature;
        FoodEmojis.ItemsSource = Food;
        ObjectsEmojis.ItemsSource = Objects;
        SymbolsEmojis.ItemsSource = Symbols;
    }

    private void QuickReaction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var emoji = button.Content?.ToString();
            if (!string.IsNullOrEmpty(emoji))
            {
                AddToRecent(emoji);
                EmojiSelected?.Invoke(this, emoji);
            }
        }
    }

    private void Emoji_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var emoji = button.Content?.ToString();
            if (!string.IsNullOrEmpty(emoji))
            {
                AddToRecent(emoji);
                EmojiSelected?.Invoke(this, emoji);
            }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLowerInvariant();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(searchText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (string.IsNullOrEmpty(searchText))
        {
            LoadEmojis();
            RecentSection.Visibility = Visibility.Visible;
            return;
        }

        // Simple emoji search - filter by common emoji names
        var emojiMap = GetEmojiNameMap();
        var filtered = emojiMap
            .Where(kvp => kvp.Value.Contains(searchText))
            .Select(kvp => kvp.Key)
            .ToList();

        RecentSection.Visibility = Visibility.Collapsed;
        SmileysEmojis.ItemsSource = filtered.Intersect(Smileys).ToList();
        NatureEmojis.ItemsSource = filtered.Intersect(Nature).ToList();
        FoodEmojis.ItemsSource = filtered.Intersect(Food).ToList();
        ObjectsEmojis.ItemsSource = filtered.Intersect(Objects).ToList();
        SymbolsEmojis.ItemsSource = filtered.Intersect(Symbols).ToList();
    }

    private static void AddToRecent(string emoji)
    {
        if (RecentlyUsed.Contains(emoji))
        {
            RecentlyUsed.Remove(emoji);
        }
        RecentlyUsed.Insert(0, emoji);
        if (RecentlyUsed.Count > 12)
        {
            RecentlyUsed.RemoveAt(RecentlyUsed.Count - 1);
        }
    }

    private static Dictionary<string, string> GetEmojiNameMap()
    {
        return new Dictionary<string, string>
        {
            ["😀"] = "grinning happy smile",
            ["😃"] = "grinning happy smile",
            ["😄"] = "grinning happy smile laugh",
            ["😁"] = "grinning happy smile beam",
            ["😂"] = "laugh cry tears joy",
            ["🤣"] = "rofl rolling laugh",
            ["😊"] = "smile blush happy",
            ["😍"] = "love heart eyes",
            ["🥰"] = "love hearts face",
            ["😘"] = "kiss love heart",
            ["😎"] = "cool sunglasses",
            ["🤔"] = "thinking think hmm",
            ["😢"] = "cry sad tear",
            ["😭"] = "cry sob tears",
            ["😱"] = "scream fear shock",
            ["😡"] = "angry mad rage",
            ["👍"] = "thumbs up like yes good",
            ["👎"] = "thumbs down dislike no bad",
            ["❤️"] = "heart love red",
            ["💔"] = "broken heart sad",
            ["🔥"] = "fire hot lit",
            ["💯"] = "hundred perfect score",
            ["✅"] = "check yes done",
            ["❌"] = "cross no wrong",
            ["🎉"] = "party celebrate tada",
            ["🎊"] = "party confetti celebrate",
            ["👀"] = "eyes look see",
            ["👋"] = "wave hi hello bye",
            ["🙏"] = "pray please thanks hope",
            ["💪"] = "muscle strong flex",
            ["🤝"] = "handshake deal agree",
            ["👏"] = "clap applause bravo"
        };
    }
}
