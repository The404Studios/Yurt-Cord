using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Helpers;
using VeaMarketplace.Server.Hubs;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

/// <summary>
/// Service for server administration functions.
/// Handles superuser configuration, role management, broadcasts, and server stats.
/// </summary>
public class AdminService
{
    private readonly DatabaseService _db;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly ILogger<AdminService> _logger;
    private readonly ConnectionStateManager _connectionManager;

    private SuperusersConfig? _superusersConfig;
    private ServerConfig? _serverConfig;
    private readonly object _configLock = new();

    public AdminService(
        DatabaseService db,
        IHubContext<ChatHub> chatHub,
        ConnectionStateManager connectionManager,
        ILogger<AdminService> logger)
    {
        _db = db;
        _chatHub = chatHub;
        _connectionManager = connectionManager;
        _logger = logger;

        LoadConfigurations();
    }

    #region Configuration

    private void LoadConfigurations()
    {
        LoadSuperusersConfig();
        LoadServerConfig();
    }

    private void LoadSuperusersConfig()
    {
        try
        {
            var configPath = ServerPaths.SuperusersConfigPath;

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                _superusersConfig = JsonSerializer.Deserialize<SuperusersConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogInformation("Loaded superusers config with {Count} superusers", _superusersConfig?.Superusers?.Count ?? 0);
            }
            else
            {
                // Create default config
                _superusersConfig = new SuperusersConfig
                {
                    Superusers = new List<SuperuserEntry>
                    {
                        new()
                        {
                            Username = "admin",
                            Email = "admin@example.com",
                            Role = UserRole.Admin,
                            Description = "Default admin account"
                        }
                    },
                    AutoPromoteOnRegister = true,
                    AllowRuntimeModification = true
                };

                SaveSuperusersConfig();
                _logger.LogInformation("Created default superusers config at {Path}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load superusers config");
            _superusersConfig = new SuperusersConfig();
        }
    }

    private void LoadServerConfig()
    {
        try
        {
            var configPath = ServerPaths.ServerConfigPath;

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                _serverConfig = JsonSerializer.Deserialize<ServerConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogInformation("Loaded server config");
            }
            else
            {
                // Create default config
                _serverConfig = new ServerConfig
                {
                    ServerName = "Yurt Cord",
                    ServerDescription = "A community marketplace and chat platform",
                    MaxUsersOnline = 1000,
                    AllowRegistration = true,
                    RequireEmailVerification = false,
                    MaintenanceMode = false,
                    AnnouncementChannel = "announcements"
                };

                SaveServerConfig();
                _logger.LogInformation("Created default server config at {Path}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load server config");
            _serverConfig = new ServerConfig();
        }
    }

    public void SaveSuperusersConfig()
    {
        try
        {
            ServerPaths.EnsureDirectoryExists(ServerPaths.DataDirectory, _logger);

            var json = JsonSerializer.Serialize(_superusersConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ServerPaths.SuperusersConfigPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save superusers config");
        }
    }

    public void SaveServerConfig()
    {
        try
        {
            ServerPaths.EnsureDirectoryExists(ServerPaths.DataDirectory, _logger);

            var json = JsonSerializer.Serialize(_serverConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ServerPaths.ServerConfigPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save server config");
        }
    }

    #endregion

    #region Superuser Management

    /// <summary>
    /// Checks if a username or email is a configured superuser and returns the role to assign.
    /// </summary>
    public UserRole? GetSuperuserRole(string username, string? email)
    {
        if (_superusersConfig?.Superusers == null || !_superusersConfig.AutoPromoteOnRegister)
            return null;

        var entry = _superusersConfig.Superusers.FirstOrDefault(s =>
            s.Username.Equals(username, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(s.Email) &&
             s.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        return entry?.Role;
    }

    /// <summary>
    /// Adds a new superuser entry.
    /// </summary>
    public bool AddSuperuser(string adminId, SuperuserEntry entry)
    {
        var admin = _db.Users.FindById(adminId);
        if (admin == null || admin.Role < UserRole.Admin)
            return false;

        if (!_superusersConfig?.AllowRuntimeModification ?? true)
            return false;

        lock (_configLock)
        {
            _superusersConfig ??= new SuperusersConfig { Superusers = new List<SuperuserEntry>() };
            _superusersConfig.Superusers ??= new List<SuperuserEntry>();

            // Check if already exists
            if (_superusersConfig.Superusers.Any(s =>
                s.Username.Equals(entry.Username, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _superusersConfig.Superusers.Add(entry);
            SaveSuperusersConfig();
        }

        // Apply role to existing user if they exist
        var user = _db.Users.FindOne(u => u.Username.Equals(entry.Username, StringComparison.OrdinalIgnoreCase));
        if (user != null && user.Role < entry.Role)
        {
            user.Role = entry.Role;
            _db.Users.Update(user);
        }

        return true;
    }

    /// <summary>
    /// Removes a superuser entry.
    /// </summary>
    public bool RemoveSuperuser(string adminId, string username)
    {
        var admin = _db.Users.FindById(adminId);
        if (admin == null || admin.Role < UserRole.Admin)
            return false;

        if (!_superusersConfig?.AllowRuntimeModification ?? true)
            return false;

        lock (_configLock)
        {
            var entry = _superusersConfig?.Superusers?.FirstOrDefault(s =>
                s.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                _superusersConfig!.Superusers!.Remove(entry);
                SaveSuperusersConfig();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all configured superusers.
    /// </summary>
    public List<SuperuserEntry> GetSuperusers(string adminId)
    {
        var admin = _db.Users.FindById(adminId);
        if (admin == null || admin.Role < UserRole.Admin)
            return new List<SuperuserEntry>();

        return _superusersConfig?.Superusers ?? new List<SuperuserEntry>();
    }

    #endregion

    #region Role Management

    /// <summary>
    /// Promotes a user to a higher role.
    /// </summary>
    public bool PromoteUser(string adminId, string targetUserId, UserRole newRole)
    {
        var admin = _db.Users.FindById(adminId);
        var target = _db.Users.FindById(targetUserId);

        if (admin == null || target == null)
            return false;

        // Only admins can promote users
        if (admin.Role < UserRole.Admin)
            return false;

        // Can't promote to equal or higher role than self
        if (newRole >= admin.Role)
            return false;

        // Can't modify users with equal or higher roles
        if (target.Role >= admin.Role)
            return false;

        target.Role = newRole;
        _db.Users.Update(target);

        _logger.LogInformation("User {Admin} promoted {Target} to {Role}",
            admin.Username, target.Username, newRole);

        return true;
    }

    /// <summary>
    /// Demotes a user to a lower role.
    /// </summary>
    public bool DemoteUser(string adminId, string targetUserId, UserRole newRole)
    {
        var admin = _db.Users.FindById(adminId);
        var target = _db.Users.FindById(targetUserId);

        if (admin == null || target == null)
            return false;

        // Only admins can demote users
        if (admin.Role < UserRole.Admin)
            return false;

        // Can't modify users with equal or higher roles
        if (target.Role >= admin.Role)
            return false;

        // Demotion must result in lower role
        if (newRole >= target.Role)
            return false;

        target.Role = newRole;
        _db.Users.Update(target);

        _logger.LogInformation("User {Admin} demoted {Target} to {Role}",
            admin.Username, target.Username, newRole);

        return true;
    }

    #endregion

    #region Broadcast & Announcements

    /// <summary>
    /// Sends a system-wide broadcast message.
    /// </summary>
    public async Task<bool> SendBroadcastAsync(string adminId, string message, string? targetChannel = null)
    {
        var admin = _db.Users.FindById(adminId);
        if (admin == null || admin.Role < UserRole.Admin)
            return false;

        var channel = targetChannel ?? _serverConfig?.AnnouncementChannel ?? "general";

        // Create and store the message
        var chatMessage = new ChatMessage
        {
            Content = message,
            Channel = channel,
            SenderId = "SYSTEM",
            SenderUsername = "System",
            SenderRole = UserRole.Admin,
            Timestamp = DateTime.UtcNow,
            IsSystemMessage = true
        };

        _db.Messages.Insert(chatMessage);

        // Broadcast to all connected clients
        await _chatHub.Clients.All.SendAsync("ReceiveMessage", new
        {
            chatMessage.Id,
            chatMessage.Content,
            chatMessage.Channel,
            chatMessage.SenderId,
            chatMessage.SenderUsername,
            chatMessage.Timestamp,
            chatMessage.IsSystemMessage,
            IsAnnouncement = true
        });

        _logger.LogInformation("Admin {Admin} sent broadcast: {Message}", admin.Username, message);

        return true;
    }

    /// <summary>
    /// Sends a notification to a specific user.
    /// </summary>
    public async Task NotifyUserAsync(string userId, string title, string message)
    {
        await _chatHub.Clients.User(userId).SendAsync("SystemNotification", new
        {
            Title = title,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    #endregion

    #region Online Users

    /// <summary>
    /// Gets all currently online users.
    /// </summary>
    public List<OnlineUserInfo> GetOnlineUsers()
    {
        return _connectionManager.GetAllOnlineUsers();
    }

    /// <summary>
    /// Gets online user count.
    /// </summary>
    public int GetOnlineUserCount()
    {
        return _connectionManager.OnlineUserCount;
    }

    #endregion

    #region Server Stats

    /// <summary>
    /// Gets comprehensive server statistics.
    /// </summary>
    public ServerStatsDto GetServerStats(string adminId)
    {
        var admin = _db.Users.FindById(adminId);
        if (admin == null || admin.Role < UserRole.Moderator)
            return new ServerStatsDto();

        var now = DateTime.UtcNow;
        var today = now.Date;
        var yesterday = today.AddDays(-1);
        var lastWeek = today.AddDays(-7);

        return new ServerStatsDto
        {
            TotalUsers = _db.Users.Count(),
            OnlineUsers = _connectionManager.OnlineUserCount,
            TotalProducts = _db.Products.Count(),
            ActiveProducts = _db.Products.Count(p => p.Status == ProductStatus.Active),
            TotalMessages = _db.Messages.Count(),
            MessagesToday = _db.Messages.Count(m => m.Timestamp >= today),
            TotalOrders = _db.Orders.Count(),
            OrdersToday = _db.Orders.Count(o => o.CreatedAt >= today),
            ActiveBans = _db.UserBans.Count(b => b.IsActive),
            ActiveMutes = _db.UserMutes.Count(m => m.IsActive && m.ExpiresAt > now),
            PendingReports = _db.MessageReports.Count(r => r.Status == ReportStatus.Pending),
            TotalRooms = _db.Rooms.Count(),
            NewUsersToday = _db.Users.Count(u => u.CreatedAt >= today),
            NewUsersThisWeek = _db.Users.Count(u => u.CreatedAt >= lastWeek),
            ServerUptime = Environment.TickCount64 / 1000, // seconds
            ServerName = _serverConfig?.ServerName ?? "Yurt Cord",
            MaintenanceMode = _serverConfig?.MaintenanceMode ?? false
        };
    }

    #endregion

    #region Kick User

    /// <summary>
    /// Kicks a user from the server (disconnects them).
    /// </summary>
    public async Task<bool> KickUserAsync(string adminId, string targetUserId, string reason)
    {
        var admin = _db.Users.FindById(adminId);
        var target = _db.Users.FindById(targetUserId);

        if (admin == null || target == null)
            return false;

        if (admin.Role < UserRole.Moderator)
            return false;

        if (target.Role >= admin.Role)
            return false;

        // Notify the user they're being kicked
        await _chatHub.Clients.User(targetUserId).SendAsync("Kicked", new
        {
            Reason = reason,
            KickedBy = admin.Username
        });

        // Disconnect the user
        _connectionManager.DisconnectUser(targetUserId);

        _logger.LogInformation("User {Admin} kicked {Target} for: {Reason}",
            admin.Username, target.Username, reason);

        return true;
    }

    #endregion
}

#region Configuration Models

public class SuperusersConfig
{
    public List<SuperuserEntry>? Superusers { get; set; }
    public bool AutoPromoteOnRegister { get; set; } = true;
    public bool AllowRuntimeModification { get; set; } = true;
}

public class SuperuserEntry
{
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public UserRole Role { get; set; } = UserRole.Admin;
    public string? Description { get; set; }
}

public class ServerConfig
{
    public string ServerName { get; set; } = "Yurt Cord";
    public string? ServerDescription { get; set; }
    public int MaxUsersOnline { get; set; } = 1000;
    public bool AllowRegistration { get; set; } = true;
    public bool RequireEmailVerification { get; set; } = false;
    public bool MaintenanceMode { get; set; } = false;
    public string AnnouncementChannel { get; set; } = "announcements";
}

public class ServerStatsDto
{
    public int TotalUsers { get; set; }
    public int OnlineUsers { get; set; }
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int TotalMessages { get; set; }
    public int MessagesToday { get; set; }
    public int TotalOrders { get; set; }
    public int OrdersToday { get; set; }
    public int ActiveBans { get; set; }
    public int ActiveMutes { get; set; }
    public int PendingReports { get; set; }
    public int TotalRooms { get; set; }
    public int NewUsersToday { get; set; }
    public int NewUsersThisWeek { get; set; }
    public long ServerUptime { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public bool MaintenanceMode { get; set; }
}

#endregion
