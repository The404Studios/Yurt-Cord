using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class UserSearchDialog : Window
{
    private readonly IFriendService? _friendService;
    private readonly IApiService? _apiService;
    private readonly ObservableCollection<UserSearchResultDto> _results = new();
    private CancellationTokenSource? _searchCts;

    public UserDto? SelectedUser { get; private set; }

    public UserSearchDialog()
    {
        InitializeComponent();

        if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            _friendService = (IFriendService?)App.ServiceProvider?.GetService(typeof(IFriendService));
            _apiService = (IApiService?)App.ServiceProvider?.GetService(typeof(IApiService));
        }

        ResultsListBox.ItemsSource = _results;
    }

    private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, _searchCts.Token);
            await PerformSearch();
        }
        catch (TaskCanceledException)
        {
            // Ignore - new search started
        }
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = PerformSearch();
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        _ = PerformSearch();
    }

    private async Task PerformSearch()
    {
        var query = SearchTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length < 2)
        {
            _results.Clear();
            NoResultsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        LoadingPanel.Visibility = Visibility.Visible;
        NoResultsPanel.Visibility = Visibility.Collapsed;
        ResultsListBox.Visibility = Visibility.Collapsed;

        try
        {
            var results = await (_apiService?.SearchUsersAsync(query) ?? Task.FromResult<List<UserSearchResultDto>>([]));

            _results.Clear();

            if (results != null && results.Count > 0)
            {
                foreach (var user in results)
                {
                    _results.Add(user);
                }
                ResultsListBox.Visibility = Visibility.Visible;
            }
            else
            {
                NoResultsPanel.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Search failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Could show profile preview
    }

    private async void AddFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is UserSearchResultDto user)
        {
            try
            {
                button.IsEnabled = false;
                button.Content = "Sending...";

                if (_friendService != null)
                {
                    await _friendService.SendFriendRequestByIdAsync(user.UserId);
                    button.Content = "Sent!";
                    MessageBox.Show($"Friend request sent to {user.Username}!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    button.Content = "Failed";
                    button.IsEnabled = true;
                    MessageBox.Show("Friend service not available", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                button.Content = "Add Friend";
                button.IsEnabled = true;
                MessageBox.Show($"Failed to send request: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        DragMove();
    }
}
