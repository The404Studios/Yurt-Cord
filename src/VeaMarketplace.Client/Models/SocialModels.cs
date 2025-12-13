using System.Windows.Media;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Models;

/// <summary>
/// Client-side user display model with additional UI properties
/// </summary>
public class UserDisplayModel
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? CustomStatus { get; set; }
    public string? StatusEmoji { get; set; }
    public string? BannerColor1 { get; set; }
    public string? BannerColor2 { get; set; }
    public UserStatus Status { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<UserRoleDisplay>? Roles { get; set; }
}

/// <summary>
/// User role for display purposes
/// </summary>
public class UserRoleDisplay
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Brush Color { get; set; } = Brushes.Blue;
}

/// <summary>
/// Friend relationship status
/// </summary>
public enum FriendRelationship
{
    None,
    Friends,
    PendingOutgoing,
    PendingIncoming,
    Blocked
}

/// <summary>
/// User online status
/// </summary>
public enum UserStatus
{
    Offline,
    Online,
    Idle,
    DoNotDisturb,
    Invisible
}
