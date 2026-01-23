using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeaMarketplace.Shared.DTOs;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Interface for real-time room/server hub service
/// </summary>
public interface IRoomHubService
{
    bool IsConnected { get; }
    ObservableCollection<RoomDto> UserRooms { get; }
    ObservableCollection<RoomDto> PublicRooms { get; }
    ObservableCollection<StreamInfoDto> ActiveStreams { get; }
    RoomDto? CurrentRoom { get; }

    // Events
    event Action? OnConnected;
    event Action<string>? OnError;
    event Action<RoomDto>? OnRoomCreated;
    event Action<RoomDto>? OnRoomJoined;
    event Action<RoomDto>? OnRoomUpdated;
    event Action<RoomMemberDto>? OnMemberJoined;
    event Action<string>? OnMemberLeft;
    event Action<string>? OnMemberOffline;
    event Action<RoomChannelDto>? OnChannelCreated;
    event Action<RoomRoleDto>? OnRoleCreated;
    event Action<string, string>? OnRoleAssigned;
    event Action<StreamInfoDto>? OnStreamStarted;
    event Action<string>? OnStreamStopped;
    event Action<StreamInfoDto>? OnStreamReady;
    event Action<string, byte[], int, int>? OnStreamFrameReceived;
    event Action<string, string>? OnProductListed;

    // Connection
    Task ConnectAsync(string token);
    Task DisconnectAsync();

    // Room operations
    Task CreateRoomAsync(CreateRoomRequest request);
    Task JoinRoomAsync(string roomId);
    Task LeaveRoomAsync(string roomId);
    Task UpdateRoomAsync(string roomId, UpdateRoomRequest request);
    Task GetPublicRoomsAsync(int skip = 0, int take = 50);

    // Channel operations
    Task CreateChannelAsync(string roomId, CreateChannelRequest request);

    // Role operations
    Task CreateRoleAsync(string roomId, CreateRoleRequest request);
    Task AssignRoleAsync(string roomId, string targetUserId, string roleId);

    // Streaming operations
    Task StartStreamAsync(string roomId, string channelId, StreamQualityRequestDto quality);
    Task StopStreamAsync(string roomId);
    Task SendStreamFrameAsync(string roomId, byte[] frameData, int width, int height);
    Task RequestStreamQualityAsync(string roomId, string streamerId, string qualityPreset);

    // Marketplace
    Task ListProductInRoomAsync(string roomId, string productId);
}

/// <summary>
/// Real-time room/server service for receiving room updates via SignalR
/// </summary>
public class RoomHubService : IRoomHubService, IAsyncDisposable
{
    private HubConnection? _connection;
    private static readonly string HubUrl = AppConstants.Hubs.GetRoomsUrl();
    private string? _authToken;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public ObservableCollection<RoomDto> UserRooms { get; } = new();
    public ObservableCollection<RoomDto> PublicRooms { get; } = new();
    public ObservableCollection<StreamInfoDto> ActiveStreams { get; } = new();
    public RoomDto? CurrentRoom { get; private set; }

    // Events
    public event Action? OnConnected;
    public event Action<string>? OnError;
    public event Action<RoomDto>? OnRoomCreated;
    public event Action<RoomDto>? OnRoomJoined;
    public event Action<RoomDto>? OnRoomUpdated;
    public event Action<RoomMemberDto>? OnMemberJoined;
    public event Action<string>? OnMemberLeft;
    public event Action<string>? OnMemberOffline;
    public event Action<RoomChannelDto>? OnChannelCreated;
    public event Action<RoomRoleDto>? OnRoleCreated;
    public event Action<string, string>? OnRoleAssigned;
    public event Action<StreamInfoDto>? OnStreamStarted;
    public event Action<string>? OnStreamStopped;
    public event Action<StreamInfoDto>? OnStreamReady;
    public event Action<string, byte[], int, int>? OnStreamFrameReceived;
    public event Action<string, string>? OnProductListed;

    public async Task ConnectAsync(string token)
    {
        _authToken = token;

        // Dispose existing connection if any
        if (_connection != null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(HubUrl)
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        // Handle reconnection
        _connection.Reconnected += async (connectionId) =>
        {
            try
            {
                Debug.WriteLine($"RoomHubService: Reconnected with connectionId {connectionId}");
                if (_authToken != null)
                {
                    await _connection.InvokeAsync("Authenticate", _authToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RoomHubService: Failed to re-authenticate after reconnection: {ex.Message}");
            }
        };

        _connection.Closed += (exception) =>
        {
            Debug.WriteLine($"RoomHubService: Connection closed. Exception: {exception?.Message}");
            return Task.CompletedTask;
        };

        RegisterHandlers();
        await _connection.StartAsync().ConfigureAwait(false);
        await _connection.InvokeAsync("Authenticate", token).ConfigureAwait(false);
    }

    private void RegisterHandlers()
    {
        if (_connection == null) return;

        // Authentication
        _connection.On("AuthenticationSuccess", () =>
        {
            Debug.WriteLine("RoomHubService: Connected");
            OnConnected?.Invoke();
        });

        _connection.On<string>("AuthenticationFailed", error =>
        {
            Debug.WriteLine($"RoomHubService: Authentication failed: {error}");
            OnError?.Invoke(error);
        });

        _connection.On<string>("RoomError", error =>
        {
            Debug.WriteLine($"RoomHubService: Room error: {error}");
            OnError?.Invoke(error);
        });

        _connection.On<string>("StreamError", error =>
        {
            Debug.WriteLine($"RoomHubService: Stream error: {error}");
            OnError?.Invoke(error);
        });

        _connection.On<string>("MarketplaceError", error =>
        {
            Debug.WriteLine($"RoomHubService: Marketplace error: {error}");
            OnError?.Invoke(error);
        });

        // User rooms list
        _connection.On<List<RoomDto>>("UserRooms", rooms =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                UserRooms.Clear();
                foreach (var room in rooms)
                {
                    UserRooms.Add(room);
                }
            });
        });

        // Public rooms list
        _connection.On<List<RoomDto>>("PublicRooms", rooms =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                PublicRooms.Clear();
                foreach (var room in rooms)
                {
                    PublicRooms.Add(room);
                }
            });
        });

        // Room created
        _connection.On<RoomDto>("RoomCreated", room =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                UserRooms.Add(room);
                OnRoomCreated?.Invoke(room);
            });
        });

        // Room joined
        _connection.On<RoomDto>("RoomJoined", room =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentRoom = room;
                if (!UserRooms.Any(r => r.Id == room.Id))
                {
                    UserRooms.Add(room);
                }
                OnRoomJoined?.Invoke(room);
            });
        });

        // Room updated
        _connection.On<RoomDto>("RoomUpdated", room =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var existing = UserRooms.FirstOrDefault(r => r.Id == room.Id);
                if (existing != null)
                {
                    var index = UserRooms.IndexOf(existing);
                    UserRooms[index] = room;
                }
                if (CurrentRoom?.Id == room.Id)
                {
                    CurrentRoom = room;
                }
                OnRoomUpdated?.Invoke(room);
            });
        });

        // Member events
        _connection.On<RoomMemberDto>("MemberJoined", member =>
        {
            OnMemberJoined?.Invoke(member);
        });

        _connection.On<string>("MemberLeft", userId =>
        {
            OnMemberLeft?.Invoke(userId);
        });

        _connection.On<string>("MemberOffline", userId =>
        {
            OnMemberOffline?.Invoke(userId);
        });

        // Channel created
        _connection.On<RoomChannelDto>("ChannelCreated", channel =>
        {
            OnChannelCreated?.Invoke(channel);
        });

        // Role events
        _connection.On<RoomRoleDto>("RoleCreated", role =>
        {
            OnRoleCreated?.Invoke(role);
        });

        _connection.On<string, string>("RoleAssigned", (userId, roleId) =>
        {
            OnRoleAssigned?.Invoke(userId, roleId);
        });

        // Streaming events
        _connection.On<List<StreamInfoDto>>("ActiveStreams", streams =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ActiveStreams.Clear();
                foreach (var stream in streams)
                {
                    ActiveStreams.Add(stream);
                }
            });
        });

        _connection.On<StreamInfoDto>("StreamStarted", stream =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ActiveStreams.Add(stream);
                OnStreamStarted?.Invoke(stream);
            });
        });

        _connection.On<string>("StreamStopped", streamerId =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var stream = ActiveStreams.FirstOrDefault(s => s.StreamerId == streamerId);
                if (stream != null)
                {
                    ActiveStreams.Remove(stream);
                }
                OnStreamStopped?.Invoke(streamerId);
            });
        });

        _connection.On<StreamInfoDto>("StreamReady", stream =>
        {
            OnStreamReady?.Invoke(stream);
        });

        _connection.On<string, byte[], int, int>("ReceiveStreamFrame", (userId, frameData, width, height) =>
        {
            OnStreamFrameReceived?.Invoke(userId, frameData, width, height);
        });

        // Marketplace
        _connection.On<string, string>("ProductListed", (productId, userId) =>
        {
            OnProductListed?.Invoke(productId, userId);
        });
    }

    public async Task CreateRoomAsync(CreateRoomRequest request)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("CreateRoom", request).ConfigureAwait(false);
        }
    }

    public async Task JoinRoomAsync(string roomId)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("JoinRoom", roomId).ConfigureAwait(false);
        }
    }

    public async Task LeaveRoomAsync(string roomId)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("LeaveRoom", roomId).ConfigureAwait(false);
            CurrentRoom = null;
        }
    }

    public async Task UpdateRoomAsync(string roomId, UpdateRoomRequest request)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("UpdateRoom", roomId, request).ConfigureAwait(false);
        }
    }

    public async Task GetPublicRoomsAsync(int skip = 0, int take = 50)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("GetPublicRooms", skip, take).ConfigureAwait(false);
        }
    }

    public async Task CreateChannelAsync(string roomId, CreateChannelRequest request)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("CreateChannel", roomId, request).ConfigureAwait(false);
        }
    }

    public async Task CreateRoleAsync(string roomId, CreateRoleRequest request)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("CreateRole", roomId, request).ConfigureAwait(false);
        }
    }

    public async Task AssignRoleAsync(string roomId, string targetUserId, string roleId)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("AssignRole", roomId, targetUserId, roleId).ConfigureAwait(false);
        }
    }

    public async Task StartStreamAsync(string roomId, string channelId, StreamQualityRequestDto quality)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("StartStream", roomId, channelId, quality).ConfigureAwait(false);
        }
    }

    public async Task StopStreamAsync(string roomId)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("StopStream", roomId).ConfigureAwait(false);
        }
    }

    public async Task SendStreamFrameAsync(string roomId, byte[] frameData, int width, int height)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("SendStreamFrame", roomId, frameData, width, height).ConfigureAwait(false);
        }
    }

    public async Task RequestStreamQualityAsync(string roomId, string streamerId, string qualityPreset)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("RequestStreamQuality", roomId, streamerId, qualityPreset).ConfigureAwait(false);
        }
    }

    public async Task ListProductInRoomAsync(string roomId, string productId)
    {
        if (_connection != null && IsConnected)
        {
            await _connection.InvokeAsync("ListProductInRoom", roomId, productId).ConfigureAwait(false);
        }
    }

    public async Task DisconnectAsync()
    {
        CurrentRoom = null;
        if (_connection != null)
        {
            await _connection.StopAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
