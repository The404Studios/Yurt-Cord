using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<IApiService, ApiService>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<IVoiceService, VoiceService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<MarketplaceViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<VoiceChannelViewModel>();

        ServiceProvider = services.BuildServiceProvider();

        base.OnStartup(e);
    }
}
