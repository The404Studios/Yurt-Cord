using System.Text.Json;
using VeaMarketplace.Server.Data;
using VeaMarketplace.Server.Helpers;
using VeaMarketplace.Shared.Enums;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class RoleConfigurationService
{
    private readonly DatabaseService _db;
    private readonly ILogger<RoleConfigurationService> _logger;
    private readonly string _configFilePath;

    public RoleConfigurationService(DatabaseService db, ILogger<RoleConfigurationService> logger)
    {
        _db = db;
        _logger = logger;
        _configFilePath = ServerPaths.RolesConfigPath;
    }

    public void LoadRolesFromConfig()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger.LogWarning("Role configuration file not found at {Path}. Creating default config.", _configFilePath);
                CreateDefaultConfig();
                return;
            }

            var json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<RoleConfigRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                _logger.LogWarning("Failed to parse role configuration. Using defaults.");
                return;
            }

            _logger.LogInformation("Loading role configuration from {Path}", _configFilePath);

            // Apply system roles (Owner, Admin, Moderator, VIP, Verified)
            ApplySystemRoles(config.Roles);

            // Apply custom roles
            ApplyCustomRoles(config.CustomRoles);

            _logger.LogInformation("Role configuration loaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading role configuration");
        }
    }

    private void ApplySystemRoles(SystemRoles? roles)
    {
        if (roles == null) return;

        // Apply Owner role
        ApplyRoleToUsers(roles.Owners?.UserIds, UserRole.Owner);

        // Apply Admin role
        ApplyRoleToUsers(roles.Admins?.UserIds, UserRole.Admin);

        // Apply Moderator role
        ApplyRoleToUsers(roles.Moderators?.UserIds, UserRole.Moderator);

        // Apply VIP role
        ApplyRoleToUsers(roles.Vip?.UserIds, UserRole.VIP);

        // Apply Verified role
        ApplyRoleToUsers(roles.Verified?.UserIds, UserRole.Verified);
    }

    private void ApplyRoleToUsers(List<string>? userIds, UserRole role)
    {
        if (userIds == null || userIds.Count == 0) return;

        foreach (var userId in userIds)
        {
            var user = _db.Users.FindById(userId);
            if (user != null)
            {
                if (user.Role != role)
                {
                    user.Role = role;
                    _db.Users.Update(user);
                    _logger.LogInformation("Assigned {Role} role to user {UserId} ({Username})",
                        role, userId, user.Username);
                }
            }
            else
            {
                _logger.LogWarning("User {UserId} not found for role assignment", userId);
            }
        }
    }

    private void ApplyCustomRoles(List<CustomRoleConfig>? customRoles)
    {
        if (customRoles == null || customRoles.Count == 0) return;

        foreach (var roleConfig in customRoles)
        {
            // Find or create the custom role
            var existingRole = _db.CustomRoles
                .Find(r => r.Name.Equals(roleConfig.Name, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            CustomRole role;
            if (existingRole != null)
            {
                // Update existing role
                existingRole.Color = roleConfig.Color ?? existingRole.Color;
                existingRole.Permissions = roleConfig.Permissions ?? existingRole.Permissions;
                _db.CustomRoles.Update(existingRole);
                role = existingRole;
            }
            else
            {
                // Create new role
                role = new CustomRole
                {
                    Name = roleConfig.Name ?? "Unnamed Role",
                    Color = roleConfig.Color ?? "#99AAB5",
                    Permissions = roleConfig.Permissions ?? new List<string>(),
                    Position = _db.CustomRoles.Count() + 1
                };
                _db.CustomRoles.Insert(role);
                _logger.LogInformation("Created custom role: {RoleName}", role.Name);
            }

            // Assign role to users
            if (roleConfig.UserIds != null)
            {
                foreach (var userId in roleConfig.UserIds)
                {
                    var user = _db.Users.FindById(userId);
                    if (user != null && !user.CustomRoleIds.Contains(role.Id))
                    {
                        user.CustomRoleIds.Add(role.Id);
                        _db.Users.Update(user);
                        _logger.LogInformation("Assigned custom role {RoleName} to user {UserId} ({Username})",
                            role.Name, userId, user.Username);
                    }
                }
            }
        }
    }

    private void CreateDefaultConfig()
    {
        var defaultConfig = new RoleConfigRoot
        {
            Description = "Role configuration for Yurt Cord server. Add user IDs to assign roles on server startup.",
            Roles = new SystemRoles
            {
                Owners = new RoleUserList { Description = "Server owners with full administrative access", UserIds = new List<string>() },
                Admins = new RoleUserList { Description = "Administrators with moderation and management permissions", UserIds = new List<string>() },
                Moderators = new RoleUserList { Description = "Moderators who can manage users and content", UserIds = new List<string>() },
                Vip = new RoleUserList { Description = "VIP users with special privileges", UserIds = new List<string>() },
                Verified = new RoleUserList { Description = "Verified users", UserIds = new List<string>() }
            },
            CustomRoles = new List<CustomRoleConfig>
            {
                new() { Name = "Developer", Color = "#3498DB", Permissions = new List<string> { "ViewAuditLog", "ManageRoles" }, UserIds = new List<string>() },
                new() { Name = "Content Creator", Color = "#E74C3C", Permissions = new List<string> { "CreateListings", "UploadMedia" }, UserIds = new List<string>() },
                new() { Name = "Beta Tester", Color = "#9B59B6", Permissions = new List<string>(), UserIds = new List<string>() }
            }
        };

        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            ServerPaths.EnsureDirectoryExists(directory, _logger);
        }
        File.WriteAllText(_configFilePath, json);
        _logger.LogInformation("Created default role configuration at {Path}", _configFilePath);
    }

    // Configuration classes
    private class RoleConfigRoot
    {
        public string? Description { get; set; }
        public SystemRoles? Roles { get; set; }
        public List<CustomRoleConfig>? CustomRoles { get; set; }
    }

    private class SystemRoles
    {
        public RoleUserList? Owners { get; set; }
        public RoleUserList? Admins { get; set; }
        public RoleUserList? Moderators { get; set; }
        public RoleUserList? Vip { get; set; }
        public RoleUserList? Verified { get; set; }
    }

    private class RoleUserList
    {
        public string? Description { get; set; }
        public List<string>? UserIds { get; set; }
    }

    private class CustomRoleConfig
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
        public List<string>? Permissions { get; set; }
        public List<string>? UserIds { get; set; }
    }
}
