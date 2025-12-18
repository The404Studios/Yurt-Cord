using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class LeaderboardView : UserControl
{
    public LeaderboardView()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetService<LeaderboardViewModel>();
    }
}
