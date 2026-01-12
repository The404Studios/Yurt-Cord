using VeaMarketplace.Mobile.ViewModels;

namespace VeaMarketplace.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.TryAutoLoginCommand.ExecuteAsync(null);
    }
}
