using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeaMarketplace.Client.Services;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Views;

public partial class ServerBrowserView : UserControl
{
    private readonly IVoiceService _voiceService = null!;
    private readonly IApiService _apiService = null!;
    private readonly IToastNotificationService _toastService = null!;
    private readonly ObservableCollection<VoiceRoomDto> _rooms = [];
    private VoiceRoomCategory? _selectedCategory;
    private VoiceRoomDto? _pendingPasswordRoom;

    public ServerBrowserView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        _toastService = (IToastNotificationService)App.ServiceProvider.GetService(typeof(IToastNotificationService))!;

        RoomsItemsControl.ItemsSource = _rooms;

        Unloaded += OnUnloaded;

        SetupEventHandlers();
        SetupVoiceServiceHandlers();
        LoadRooms();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe from events to prevent memory leaks
        _voiceService.OnPublicVoiceRoomsReceived -= OnPublicVoiceRoomsReceived;
        _voiceService.OnVoiceRoomAdded -= OnVoiceRoomAdded;
        _voiceService.OnVoiceRoomUpdated -= OnVoiceRoomUpdated;
        _voiceService.OnVoiceRoomRemoved -= OnVoiceRoomRemoved;
        _voiceService.OnVoiceRoomJoined -= OnVoiceRoomJoined;
        _voiceService.OnVoiceRoomCreated -= OnVoiceRoomCreated;
        _voiceService.OnVoiceRoomError -= OnVoiceRoomError;
    }

    private void SetupVoiceServiceHandlers()
    {
        _voiceService.OnPublicVoiceRoomsReceived += OnPublicVoiceRoomsReceived;
        _voiceService.OnVoiceRoomAdded += OnVoiceRoomAdded;
        _voiceService.OnVoiceRoomUpdated += OnVoiceRoomUpdated;
        _voiceService.OnVoiceRoomRemoved += OnVoiceRoomRemoved;
        _voiceService.OnVoiceRoomJoined += OnVoiceRoomJoined;
        _voiceService.OnVoiceRoomCreated += OnVoiceRoomCreated;
        _voiceService.OnVoiceRoomError += OnVoiceRoomError;
    }

    private void OnPublicVoiceRoomsReceived(List<VoiceRoomDto> rooms, int total, int page, int pageSize)
    {
        UpdateRooms(rooms);
    }

    private void OnVoiceRoomAdded(VoiceRoomDto room)
    {
        AddRoom(room);
    }

    private void OnVoiceRoomUpdated(VoiceRoomDto room)
    {
        UpdateRoom(room);
    }

    private void OnVoiceRoomRemoved(string roomId)
    {
        RemoveRoom(roomId);
    }

    private void OnVoiceRoomJoined(VoiceRoomDto room)
    {
        _toastService.ShowSuccess("Joined", $"Joined room: {room.Name}");
    }

    private void OnVoiceRoomCreated(VoiceRoomDto room)
    {
        _toastService.ShowSuccess("Room Created", $"Room '{room.Name}' created!");
        CreateRoomModal.Visibility = Visibility.Collapsed;
        AddRoom(room);
    }

    private void OnVoiceRoomError(string error)
    {
        _toastService.ShowError("Error", error);
    }

    private void SetupEventHandlers()
    {
        NewRoomVisibilityBox.SelectionChanged += (s, e) =>
        {
            if (NewRoomVisibilityBox.SelectedItem is ComboBoxItem item)
            {
                PasswordSection.Visibility = item.Tag?.ToString() == "Private"
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        };

        SearchBox.TextChanged += (s, e) =>
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        };
    }

    private async void LoadRooms()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            // Request rooms from the voice service
            await _voiceService.GetPublicVoiceRoomsAsync(_selectedCategory, SearchBox.Text?.Trim());
            await Task.Delay(100); // Small delay to let hub response come back
            UpdateRoomDisplay();
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateRoomDisplay()
    {
        if (_rooms.Count == 0)
        {
            NoRoomsPanel.Visibility = Visibility.Visible;
            RoomsItemsControl.Visibility = Visibility.Collapsed;
        }
        else
        {
            NoRoomsPanel.Visibility = Visibility.Collapsed;
            RoomsItemsControl.Visibility = Visibility.Visible;
        }

        RoomCountText.Text = $" ({_rooms.Count} active)";
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Filter rooms by search query
            var query = SearchBox.Text?.Trim();
            FilterRooms(query, _selectedCategory);
        }
    }

    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var tag = button.Tag?.ToString();

        _selectedCategory = string.IsNullOrEmpty(tag)
            ? null
            : Enum.Parse<VoiceRoomCategory>(tag);

        FilterRooms(SearchBox.Text?.Trim(), _selectedCategory);
    }

    private async void FilterRooms(string? query, VoiceRoomCategory? category)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            // Request filtered rooms from the server
            await _voiceService.GetPublicVoiceRoomsAsync(category, query);
            await Task.Delay(100); // Small delay to let hub response come back
            UpdateRoomDisplay();
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void RoomCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is VoiceRoomDto room)
        {
            TryJoinRoom(room);
        }
    }

    private void JoinRoom_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is VoiceRoomDto room)
        {
            e.Handled = true;
            TryJoinRoom(room);
        }
    }

    private void TryJoinRoom(VoiceRoomDto room)
    {
        if (room.Password != null)
        {
            // Show password modal
            _pendingPasswordRoom = room;
            PasswordRoomNameText.Text = $"Joining: {room.Name}";
            JoinPasswordBox.Password = "";
            PasswordModal.Visibility = Visibility.Visible;
        }
        else
        {
            JoinRoom(room, null);
        }
    }

    private async void JoinRoom(VoiceRoomDto room, string? password)
    {
        var user = _apiService.CurrentUser;
        if (user == null) return;

        try
        {
            // Call the voice service to join the room
            await _voiceService.JoinVoiceRoomAsync(room.Id, password);
        }
        catch (Exception ex)
        {
            _toastService.ShowError("Join Failed", ex.Message);
        }
    }

    private void CreateRoom_Click(object sender, RoutedEventArgs e)
    {
        ClearCreateForm();
        CreateRoomModal.Visibility = Visibility.Visible;
    }

    private void CancelCreateRoom_Click(object sender, RoutedEventArgs e)
    {
        CreateRoomModal.Visibility = Visibility.Collapsed;
    }

    private async void ConfirmCreateRoom_Click(object sender, RoutedEventArgs e)
    {
        var name = NewRoomNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _toastService.ShowWarning("Validation Error", "Please enter a room name");
            return;
        }

        var description = NewRoomDescBox.Text?.Trim() ?? "";
        var categoryItem = NewRoomCategoryBox.SelectedItem as ComboBoxItem;
        var category = Enum.Parse<VoiceRoomCategory>(categoryItem?.Tag?.ToString() ?? "General");
        var visibilityItem = NewRoomVisibilityBox.SelectedItem as ComboBoxItem;
        var isPublic = visibilityItem?.Tag?.ToString() != "Private";
        var password = isPublic ? null : NewRoomPasswordBox.Password;
        var maxParticipants = int.TryParse(NewRoomMaxBox.Text, out var max) ? max : 10;
        var allowScreenShare = AllowScreenShareCheck.IsChecked ?? true;

        var user = _apiService.CurrentUser;
        if (user == null) return;

        try
        {
            var dto = new CreateVoiceRoomDto
            {
                Name = name,
                Description = description,
                IsPublic = isPublic,
                Password = password,
                MaxParticipants = Math.Clamp(maxParticipants, 2, 50),
                Category = category,
                AllowScreenShare = allowScreenShare
            };

            // Call the voice service to create the room
            await _voiceService.CreateVoiceRoomAsync(dto);
            // The OnVoiceRoomCreated event handler will close the modal and show success
        }
        catch (Exception ex)
        {
            _toastService.ShowError("Create Failed", ex.Message);
        }
    }

    private void ClearCreateForm()
    {
        NewRoomNameBox.Text = "";
        NewRoomDescBox.Text = "";
        NewRoomCategoryBox.SelectedIndex = 0;
        NewRoomMaxBox.Text = "10";
        NewRoomVisibilityBox.SelectedIndex = 0;
        NewRoomPasswordBox.Password = "";
        AllowScreenShareCheck.IsChecked = true;
        PasswordSection.Visibility = Visibility.Collapsed;
    }

    private void CancelPassword_Click(object sender, RoutedEventArgs e)
    {
        PasswordModal.Visibility = Visibility.Collapsed;
        _pendingPasswordRoom = null;
    }

    private void ConfirmPassword_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingPasswordRoom != null)
        {
            JoinRoom(_pendingPasswordRoom, JoinPasswordBox.Password);
            PasswordModal.Visibility = Visibility.Collapsed;
            _pendingPasswordRoom = null;
        }
    }

    private void Modal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == CreateRoomModal)
        {
            CreateRoomModal.Visibility = Visibility.Collapsed;
        }
        else if (e.OriginalSource == PasswordModal)
        {
            PasswordModal.Visibility = Visibility.Collapsed;
            _pendingPasswordRoom = null;
        }
    }

    // Called when rooms are received from the hub
    public void UpdateRooms(List<VoiceRoomDto> rooms)
    {
        Dispatcher.Invoke(() =>
        {
            _rooms.Clear();
            foreach (var room in rooms)
            {
                _rooms.Add(room);
            }
            UpdateRoomDisplay();
        });
    }

    // Called when a single room is added
    public void AddRoom(VoiceRoomDto room)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_rooms.Any(r => r.Id == room.Id))
            {
                _rooms.Add(room);
            }
            UpdateRoomDisplay();
        });
    }

    // Called when a room is updated
    public void UpdateRoom(VoiceRoomDto room)
    {
        Dispatcher.Invoke(() =>
        {
            var existing = _rooms.FirstOrDefault(r => r.Id == room.Id);
            if (existing != null)
            {
                var index = _rooms.IndexOf(existing);
                _rooms[index] = room;
            }
            UpdateRoomDisplay();
        });
    }

    // Called when a room is removed
    public void RemoveRoom(string roomId)
    {
        Dispatcher.Invoke(() =>
        {
            var room = _rooms.FirstOrDefault(r => r.Id == roomId);
            if (room != null)
            {
                _rooms.Remove(room);
            }
            UpdateRoomDisplay();
        });
    }
}
