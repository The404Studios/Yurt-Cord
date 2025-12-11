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
    void NavigateToFriends();
    void NavigateToVoiceCall();
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
    public void NavigateToFriends() => NavigateTo("Friends");
    public void NavigateToVoiceCall() => NavigateTo("VoiceCall");
}
