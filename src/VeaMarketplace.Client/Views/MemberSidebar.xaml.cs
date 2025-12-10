using System.ComponentModel;
using System.Windows.Controls;
using VeaMarketplace.Client.ViewModels;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Views;

public partial class MemberSidebar : UserControl
{
    private readonly ChatViewModel? _viewModel;

    public MemberSidebar()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _viewModel = (ChatViewModel)App.ServiceProvider.GetService(typeof(ChatViewModel))!;

        OnlineMembersControl.ItemsSource = _viewModel.OnlineUsers;

        // Update online count
        _viewModel.OnlineUsers.CollectionChanged += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                OnlineHeaderText.Text = $"ONLINE — {_viewModel.OnlineUsers.Count}";

                // Separate staff members
                var staff = _viewModel.OnlineUsers.Where(u => u.Role >= UserRole.Moderator).ToList();
                if (staff.Count > 0)
                {
                    StaffHeader.Visibility = System.Windows.Visibility.Visible;
                    StaffMembersControl.Visibility = System.Windows.Visibility.Visible;
                    StaffMembersControl.ItemsSource = staff;
                }
                else
                {
                    StaffHeader.Visibility = System.Windows.Visibility.Collapsed;
                    StaffMembersControl.Visibility = System.Windows.Visibility.Collapsed;
                }
            });
        };
    }
}
