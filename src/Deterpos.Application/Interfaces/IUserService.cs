using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Domain.Entities;

namespace Deterpos.Application.Interfaces;

public interface IUserService
{
    Task<User?> AuthenticateAsync(string username, string password);
    Task EnsureAdminUserExistsAsync();
    Task<IReadOnlyList<User>> GetAllUsersAsync(string? searchTerm = null);
    Task<User?> GetUserByIdAsync(int id);
    Task<User> CreateUserAsync(string username, string fullName, string email, string password, SystemUserRole role, UserPermissions? permissions = null);
    Task<User> UpdateUserAsync(int id, string fullName, string email, SystemUserRole role, bool isActive, UserPermissions? permissions = null);
    Task<bool> ChangePasswordAsync(int userId, string newPassword);
    Task<bool> ToggleUserStatusAsync(int userId);
    Task<UserPermissions> UpdatePermissionsAsync(int userId, UserPermissions permissions);

    Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync(int limit = 150);
    Task RecordAuditLogAsync(string username, string module, string action, string details);
}
