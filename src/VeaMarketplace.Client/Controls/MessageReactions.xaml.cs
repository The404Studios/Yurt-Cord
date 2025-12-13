using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using VeaMarketplace.Client.Models;

namespace VeaMarketplace.Client.Controls;

public partial class MessageReactions : UserControl
{
    public static readonly DependencyProperty ReactionsProperty =
        DependencyProperty.Register(nameof(Reactions), typeof(ObservableCollection<MessageReaction>),
            typeof(MessageReactions), new PropertyMetadata(null, OnReactionsChanged));

    public static readonly DependencyProperty MessageIdProperty =
        DependencyProperty.Register(nameof(MessageId), typeof(string),
            typeof(MessageReactions), new PropertyMetadata(null));

    public ObservableCollection<MessageReaction> Reactions
    {
        get => (ObservableCollection<MessageReaction>)GetValue(ReactionsProperty);
        set => SetValue(ReactionsProperty, value);
    }

    public string MessageId
    {
        get => (string)GetValue(MessageIdProperty);
        set => SetValue(MessageIdProperty, value);
    }

    public event EventHandler<ReactionEventArgs>? ReactionClicked;

    public MessageReactions()
    {
        InitializeComponent();
    }

    private static void OnReactionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MessageReactions control)
        {
            control.UpdateReactions();
        }
    }

    private void UpdateReactions()
    {
        if (Reactions == null || !Reactions.Any())
        {
            ReactionsItemsControl.ItemsSource = null;
            Visibility = Visibility.Collapsed;
            return;
        }

        var groupedReactions = Reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new ReactionDisplay
            {
                Emoji = g.Key,
                Count = g.Count(),
                HasUserReacted = g.Any(r => r.IsCurrentUser),
                Tooltip = string.Join(", ", g.Select(r => r.Username).Take(10)) +
                         (g.Count() > 10 ? $" and {g.Count() - 10} more" : ""),
                Users = g.Select(r => r.UserId).ToList()
            })
            .OrderByDescending(r => r.Count)
            .ToList();

        ReactionsItemsControl.ItemsSource = groupedReactions;
        Visibility = Visibility.Visible;
    }

    private void ReactionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ReactionDisplay reaction)
        {
            ReactionClicked?.Invoke(this, new ReactionEventArgs
            {
                MessageId = MessageId,
                Emoji = reaction.Emoji,
                ShouldRemove = reaction.HasUserReacted
            });
        }
    }
}

public class MessageReaction
{
    public string Id { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool IsCurrentUser { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReactionDisplay
{
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool HasUserReacted { get; set; }
    public string Tooltip { get; set; } = string.Empty;
    public List<string> Users { get; set; } = [];
}

public class ReactionEventArgs : EventArgs
{
    public string MessageId { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool ShouldRemove { get; set; }
}
