namespace VeaMarketplace.Client.Services;

public interface INavigationService
{
    event Action<string>? OnNavigate;
    event Action<string?>? OnViewUserProfile;
    string CurrentView { get; }
    string? ViewingUserId { get; }

    void NavigateTo(string viewName);
    void NavigateToChat();
    void NavigateToMarketplace();
    void NavigateToProfile();
    void NavigateToProfile(string userId);
    void NavigateToSettings();
    void NavigateToSettings(string section);
    void NavigateToFriends();
    void NavigateToVoiceCall();
    void NavigateToProduct(string productId);
    void NavigateToDirectMessage(string userId);
}

public class NavigationService : INavigationService
{
    public event Action<string>? OnNavigate;
    public event Action<string?>? OnViewUserProfile;
    public string CurrentView { get; private set; } = "Chat";
    public string? ViewingUserId { get; private set; }

    public void NavigateTo(string viewName)
    {
        CurrentView = viewName;
        OnNavigate?.Invoke(viewName);
    }

    public void NavigateToChat() => NavigateTo("Chat");
    public void NavigateToMarketplace() => NavigateTo("Marketplace");

    public void NavigateToProfile()
    {
        ViewingUserId = null;
        OnViewUserProfile?.Invoke(null);
        NavigateTo("Profile");
    }

    public void NavigateToProfile(string userId)
    {
        ViewingUserId = userId;
        OnViewUserProfile?.Invoke(userId);
        NavigateTo("Profile");
    }

    public void NavigateToSettings() => NavigateTo("Settings");

    public void NavigateToSettings(string section)
    {
        // Store section for settings view to use
        NavigateTo($"Settings:{section}");
    }

    public void NavigateToFriends() => NavigateTo("Friends");
    public void NavigateToVoiceCall() => NavigateTo("VoiceCall");

    public void NavigateToProduct(string productId)
    {
        // Navigate to product details
        NavigateTo($"Product:{productId}");
    }

    public void NavigateToDirectMessage(string userId)
    {
        // Navigate to DM with specific user
        NavigateTo($"DirectMessage:{userId}");
    }
}
