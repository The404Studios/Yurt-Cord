using VeaMarketplace.Mobile.ViewModels;

namespace VeaMarketplace.Mobile.Pages;

public partial class FriendsPage : ContentPage
{
    private readonly FriendsViewModel _viewModel;

    public FriendsPage(FriendsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadFriendsCommand.ExecuteAsync(null);
    }
}
