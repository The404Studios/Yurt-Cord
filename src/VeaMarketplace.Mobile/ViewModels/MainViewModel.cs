using CommunityToolkit.Mvvm.ComponentModel;
using VeaMarketplace.Mobile.Services;

namespace VeaMarketplace.Mobile.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IApiService _apiService;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _avatarUrl;

    public MainViewModel(IAuthService authService, IApiService apiService)
    {
        _authService = authService;
        _apiService = apiService;
        Username = _authService.CurrentUsername;
    }
}
