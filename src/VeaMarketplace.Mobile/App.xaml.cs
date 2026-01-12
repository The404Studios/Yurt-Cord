using VeaMarketplace.Mobile.Pages;

namespace VeaMarketplace.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);

        // Set minimum window size for tablets/desktop
        window.MinimumWidth = 400;
        window.MinimumHeight = 600;

        return window;
    }
}
