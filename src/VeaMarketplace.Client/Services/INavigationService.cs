namespace VeaMarketplace.Client.Services;

public interface INavigationService
{
    event Action<string>? OnNavigate;
    string CurrentView { get; }

    void NavigateTo(string viewName);
    void NavigateToChat();
    void NavigateToMarketplace();
    void NavigateToProfile();
    void NavigateToSettings();
    void NavigateToFriends();
    void NavigateToVoiceCall();
}

public class NavigationService : INavigationService
{
    public event Action<string>? OnNavigate;
    public string CurrentView { get; private set; } = "Chat";

    public void NavigateTo(string viewName)
    {
        CurrentView = viewName;
        OnNavigate?.Invoke(viewName);
    }

    public void NavigateToChat() => NavigateTo("Chat");
    public void NavigateToMarketplace() => NavigateTo("Marketplace");
    public void NavigateToProfile() => NavigateTo("Profile");
    public void NavigateToSettings() => NavigateTo("Settings");
    public void NavigateToFriends() => NavigateTo("Friends");
    public void NavigateToVoiceCall() => NavigateTo("VoiceCall");
}
