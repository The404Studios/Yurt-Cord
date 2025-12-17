namespace VeaMarketplace.Shared.Models;

/// <summary>
/// Represents a follow relationship between users.
/// Following allows seeing a user's public activities in your feed.
/// </summary>
public class UserFollow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The user who is following
    /// </summary>
    public string FollowerId { get; set; } = string.Empty;

    /// <summary>
    /// The user being followed
    /// </summary>
    public string FollowedId { get; set; } = string.Empty;

    /// <summary>
    /// When the follow was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
