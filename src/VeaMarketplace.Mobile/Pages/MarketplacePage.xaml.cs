using VeaMarketplace.Mobile.ViewModels;

namespace VeaMarketplace.Mobile.Pages;

public partial class MarketplacePage : ContentPage
{
    private readonly MarketplaceViewModel _viewModel;

    public MarketplacePage(MarketplaceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadProductsCommand.ExecuteAsync(null);
    }
}
