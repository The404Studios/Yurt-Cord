using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using VeaMarketplace.Client.ViewModels;

namespace VeaMarketplace.Client.Views;

public partial class FriendsView : UserControl
{
    private readonly DispatcherTimer? _typingTimer;

    public FriendsView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        DataContext = App.ServiceProvider.GetService(typeof(FriendsViewModel));

        // Set up typing debounce timer
        _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _typingTimer.Tick += TypingTimer_Tick;

        // Cleanup timer when unloaded
        Unloaded += (s, e) =>
        {
            _typingTimer?.Stop();
        };
    }

    private void DmInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Reset the timer on each keystroke
        _typingTimer?.Stop();
        _typingTimer?.Start();
    }

    private void TypingTimer_Tick(object? sender, EventArgs e)
    {
        _typingTimer?.Stop();
        // Send typing indicator
        if (DataContext is FriendsViewModel vm)
        {
            vm.SendTypingCommand.Execute(null);
        }
    }
}
