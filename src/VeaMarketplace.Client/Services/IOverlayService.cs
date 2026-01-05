using VeaMarketplace.Client.Views;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service interface for spawning flying messages and notifications on the screen overlay
/// </summary>
public interface IOverlayService
{
    /// <summary>
    /// Spawns a flying message that crosses the screen (visible even when app is minimized)
    /// </summary>
    void SpawnFlyingMessage(string sender, string content, string? avatarUrl = null);

    /// <summary>
    /// Spawns a flying notification banner that crosses the screen
    /// </summary>
    void SpawnFlyingNotification(string title, string message, OverseerOverlay.NotificationType type = OverseerOverlay.NotificationType.Info);

    /// <summary>
    /// Spawns a floating envelope animation
    /// </summary>
    void SpawnFlyingEnvelope(string sender);

    /// <summary>
    /// Shows or hides the overlay
    /// </summary>
    void SetOverlayVisible(bool visible);
}

/// <summary>
/// Implementation of overlay service using OverseerOverlay window
/// </summary>
public class OverlayService : IOverlayService
{
    private readonly OverseerOverlay _overlay;

    public OverlayService()
    {
        _overlay = OverseerOverlay.Instance;
    }

    public void SpawnFlyingMessage(string sender, string content, string? avatarUrl = null)
    {
        _overlay.SpawnFlyingMessage(sender, content, avatarUrl);
    }

    public void SpawnFlyingNotification(string title, string message, OverseerOverlay.NotificationType type = OverseerOverlay.NotificationType.Info)
    {
        _overlay.SpawnFlyingNotification(title, message, type);
    }

    public void SpawnFlyingEnvelope(string sender)
    {
        _overlay.SpawnFlyingEnvelope(sender);
    }

    public void SetOverlayVisible(bool visible)
    {
        if (visible)
            _overlay.Show();
        else
            _overlay.Hide();
    }
}
