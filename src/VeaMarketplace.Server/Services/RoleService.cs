using VeaMarketplace.Server.Data;
using VeaMarketplace.Shared.DTOs;
using VeaMarketplace.Shared.Models;

namespace VeaMarketplace.Server.Services;

public class RoleService
{
    private readonly DatabaseService _db;

    public RoleService(DatabaseService db)
    {
        _db = db;
    }

    public List<CustomRoleDto> GetAllRoles()
    {
        return _db.CustomRoles
            .FindAll()
            .OrderByDescending(r => r.Position)
            .Select(MapToDto)
            .ToList();
    }

    public CustomRoleDto? GetRole(string roleId)
    {
        var role = _db.CustomRoles.FindById(roleId);
        return role != null ? MapToDto(role) : null;
    }

    public CustomRoleDto CreateRole(string name, string color, int position, List<string> permissions)
    {
        var role = new CustomRole
        {
            Name = name,
            Color = color,
            Position = position,
            Permissions = permissions
        };

        _db.CustomRoles.Insert(role);
        return MapToDto(role);
    }

    public CustomRoleDto? UpdateRole(string roleId, string? name, string? color, int? position, bool? isHoisted, List<string>? permissions)
    {
        var role = _db.CustomRoles.FindById(roleId);
        if (role == null) return null;

        if (name != null) role.Name = name;
        if (color != null) role.Color = color;
        if (position.HasValue) role.Position = position.Value;
        if (isHoisted.HasValue) role.IsHoisted = isHoisted.Value;
        if (permissions != null) role.Permissions = permissions;

        _db.CustomRoles.Update(role);
        return MapToDto(role);
    }

    public bool DeleteRole(string roleId)
    {
        // Remove role from all users first
        var users = _db.Users.Find(u => u.CustomRoleIds.Contains(roleId)).ToList();
        foreach (var user in users)
        {
            user.CustomRoleIds.Remove(roleId);
            _db.Users.Update(user);
        }

        return _db.CustomRoles.Delete(roleId);
    }

    public bool AssignRoleToUser(string userId, string roleId)
    {
        var user = _db.Users.FindById(userId);
        var role = _db.CustomRoles.FindById(roleId);

        if (user == null || role == null) return false;
        if (user.CustomRoleIds.Contains(roleId)) return true;

        user.CustomRoleIds.Add(roleId);
        _db.Users.Update(user);
        return true;
    }

    public bool RemoveRoleFromUser(string userId, string roleId)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) return false;

        user.CustomRoleIds.Remove(roleId);
        _db.Users.Update(user);
        return true;
    }

    public List<CustomRoleDto> GetUserRoles(string userId)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) return new List<CustomRoleDto>();

        return user.CustomRoleIds
            .Select(id => _db.CustomRoles.FindById(id))
            .Where(r => r != null)
            .OrderByDescending(r => r!.Position)
            .Select(r => MapToDto(r!))
            .ToList();
    }

    public bool UserHasPermission(string userId, string permission)
    {
        var user = _db.Users.FindById(userId);
        if (user == null) return false;

        // Check if user has Administrator permission (bypasses all checks)
        var roles = user.CustomRoleIds
            .Select(id => _db.CustomRoles.FindById(id))
            .Where(r => r != null)
            .ToList();

        return roles.Any(r =>
            r!.Permissions.Contains(RolePermissions.Administrator) ||
            r.Permissions.Contains(permission));
    }

    private static CustomRoleDto MapToDto(CustomRole role)
    {
        return new CustomRoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Color = role.Color,
            Position = role.Position,
            IsHoisted = role.IsHoisted,
            Permissions = role.Permissions
        };
    }
}
