using VeaMarketplace.Mobile.Pages;

namespace VeaMarketplace.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("chat/channel", typeof(ChatPage));
        Routing.RegisterRoute("profile/user", typeof(ProfilePage));
        Routing.RegisterRoute("marketplace/product", typeof(MarketplacePage));
    }

    /// <summary>
    /// Navigate to main app after login
    /// </summary>
    public async Task NavigateToMainAsync()
    {
        // Switch to tab bar navigation
        MainTabBar.IsVisible = true;
        LoginShell.IsVisible = false;
        await GoToAsync("//main/chat");
    }

    /// <summary>
    /// Navigate to login after logout
    /// </summary>
    public async Task NavigateToLoginAsync()
    {
        MainTabBar.IsVisible = false;
        LoginShell.IsVisible = true;
        await GoToAsync("//login");
    }
}
