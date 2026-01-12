namespace VeaMarketplace.Mobile.Services;

public interface INavigationService
{
    Task NavigateToAsync(string route);
    Task NavigateToAsync(string route, IDictionary<string, object> parameters);
    Task GoBackAsync();
    Task NavigateToMainAsync();
    Task NavigateToLoginAsync();
}

public class NavigationService : INavigationService
{
    public async Task NavigateToAsync(string route)
    {
        await Shell.Current.GoToAsync(route);
    }

    public async Task NavigateToAsync(string route, IDictionary<string, object> parameters)
    {
        await Shell.Current.GoToAsync(route, parameters);
    }

    public async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    public async Task NavigateToMainAsync()
    {
        if (Shell.Current is AppShell appShell)
        {
            await appShell.NavigateToMainAsync();
        }
    }

    public async Task NavigateToLoginAsync()
    {
        if (Shell.Current is AppShell appShell)
        {
            await appShell.NavigateToLoginAsync();
        }
    }
}
