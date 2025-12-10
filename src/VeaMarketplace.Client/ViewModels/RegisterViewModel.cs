using CommunityToolkit.Mvvm.ComponentModel;

namespace VeaMarketplace.Client.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;
}
