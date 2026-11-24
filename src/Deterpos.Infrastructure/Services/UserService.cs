using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public UserService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public UserService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        await using var context = GetContext();
        var user = await context.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower() && u.IsActive);

        if (user == null) return null;

        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            await RecordAuditLogAsync(username, "Seguridad", "IntentoFallido", "Contraseña incorrecta");
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await RecordAuditLogAsync(user.Username, "Seguridad", "InicioSesion", "Autenticación exitosa");
        return user;
    }

    public async Task EnsureAdminUserExistsAsync()
    {
        await using var context = GetContext();
        if (!await context.Users.AnyAsync())
        {
            await SeedDefaultAdminUserAsync(context);
        }
    }

    public async Task<IReadOnlyList<User>> GetAllUsersAsync(string? searchTerm = null)
    {
        await using var context = GetContext();

        // Si no hay usuarios en el sistema, sembramos el Administrador por defecto
        if (!await context.Users.AnyAsync())
        {
            await SeedDefaultAdminUserAsync(context);
        }

        var query = context.Users
            .Include(u => u.Permissions)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        return await query.OrderBy(u => u.Id).ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        await using var context = GetContext();
        return await context.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User> CreateUserAsync(string username, string fullName, string email, string password, SystemUserRole role, UserPermissions? permissions = null)
    {
        await using var context = GetContext();

        var cleanUsername = username.Trim();
        if (await context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower()))
        {
            throw new InvalidOperationException($"El nombre de usuario '{cleanUsername}' ya existe.");
        }

        var (hash, salt) = HashPassword(password);

        var newUser = new User
        {
            Username = cleanUsername,
            FullName = fullName.Trim(),
            Email = email?.Trim() ?? string.Empty,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(newUser);
        await context.SaveChangesAsync();

        var userPerms = permissions ?? CreateDefaultPermissionsForRole(role);
        userPerms.UserId = newUser.Id;
        context.UserPermissions.Add(userPerms);
        await context.SaveChangesAsync();

        newUser.Permissions = userPerms;

        await RecordAuditLogAsync("Admin", "Seguridad", "CrearUsuario", $"Usuario creado: {newUser.Username} ({newUser.Role})");
        return newUser;
    }

    public async Task<User> UpdateUserAsync(int id, string fullName, string email, SystemUserRole role, bool isActive, UserPermissions? permissions = null)
    {
        await using var context = GetContext();
        var user = await context.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            throw new InvalidOperationException("El usuario especificado no existe.");
        }

        user.FullName = fullName.Trim();
        user.Email = email?.Trim() ?? string.Empty;
        user.Role = role;
        user.IsActive = isActive;

        if (permissions != null && user.Permissions != null)
        {
            user.Permissions.CanOpenCashRegister = permissions.CanOpenCashRegister;
            user.Permissions.CanCloseCashRegister = permissions.CanCloseCashRegister;
            user.Permissions.CanPerformBlindCount = permissions.CanPerformBlindCount;
            user.Permissions.CanEmitInvoices = permissions.CanEmitInvoices;
            user.Permissions.CanEmitCreditNotes = permissions.CanEmitCreditNotes;
            user.Permissions.CanCancelSales = permissions.CanCancelSales;
            user.Permissions.CanManagePurchases = permissions.CanManagePurchases;
            user.Permissions.CanManageCredits = permissions.CanManageCredits;
            user.Permissions.CanManageExpenses = permissions.CanManageExpenses;
            user.Permissions.CanViewFiscalBooks = permissions.CanViewFiscalBooks;
            user.Permissions.CanViewProductCosts = permissions.CanViewProductCosts;
            user.Permissions.CanUpdateExchangeRates = permissions.CanUpdateExchangeRates;
            user.Permissions.CanManageUsers = permissions.CanManageUsers;
        }

        await context.SaveChangesAsync();
        await RecordAuditLogAsync("Admin", "Seguridad", "ModificarUsuario", $"Usuario editado: {user.Username} ({user.Role})");
        return user;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string newPassword)
    {
        await using var context = GetContext();
        var user = await context.Users.FindAsync(userId);
        if (user == null) return false;

        var (hash, salt) = HashPassword(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = false;

        await context.SaveChangesAsync();
        await RecordAuditLogAsync(user.Username, "Seguridad", "CambioClave", "Contraseña cambiada exitosamente");
        return true;
    }

    public async Task<bool> ToggleUserStatusAsync(int userId)
    {
        await using var context = GetContext();
        var user = await context.Users.FindAsync(userId);
        if (user == null) return false;

        // No permitir desactivar al superadmin principal si es ID 1
        if (user.Id == 1 && user.IsActive)
        {
            throw new InvalidOperationException("No se puede desactivar la cuenta principal de Administrador.");
        }

        user.IsActive = !user.IsActive;
        await context.SaveChangesAsync();

        string action = user.IsActive ? "ActivarUsuario" : "BloquearUsuario";
        await RecordAuditLogAsync("Admin", "Seguridad", action, $"Estado de {user.Username} cambiado a {(user.IsActive ? "Activo" : "Bloqueado")}");
        return true;
    }

    public async Task<UserPermissions> UpdatePermissionsAsync(int userId, UserPermissions permissions)
    {
        await using var context = GetContext();
        var existing = await context.UserPermissions.FirstOrDefaultAsync(p => p.UserId == userId);

        if (existing == null)
        {
            permissions.UserId = userId;
            context.UserPermissions.Add(permissions);
            await context.SaveChangesAsync();
            return permissions;
        }

        existing.CanOpenCashRegister = permissions.CanOpenCashRegister;
        existing.CanCloseCashRegister = permissions.CanCloseCashRegister;
        existing.CanPerformBlindCount = permissions.CanPerformBlindCount;
        existing.CanEmitInvoices = permissions.CanEmitInvoices;
        existing.CanEmitCreditNotes = permissions.CanEmitCreditNotes;
        existing.CanCancelSales = permissions.CanCancelSales;
        existing.CanManagePurchases = permissions.CanManagePurchases;
        existing.CanManageCredits = permissions.CanManageCredits;
        existing.CanManageExpenses = permissions.CanManageExpenses;
        existing.CanViewFiscalBooks = permissions.CanViewFiscalBooks;
        existing.CanViewProductCosts = permissions.CanViewProductCosts;
        existing.CanUpdateExchangeRates = permissions.CanUpdateExchangeRates;
        existing.CanManageUsers = permissions.CanManageUsers;

        await context.SaveChangesAsync();
        await RecordAuditLogAsync("Admin", "Seguridad", "ModificarPermisos", $"Permisos actualizados para usuario ID {userId}");
        return existing;
    }

    public async Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync(int limit = 150)
    {
        await using var context = GetContext();
        return await context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task RecordAuditLogAsync(string username, string module, string action, string details)
    {
        try
        {
            await using var context = GetContext();
            context.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                Username = string.IsNullOrWhiteSpace(username) ? "Sistema" : username,
                Module = module,
                Action = action,
                Details = details,
                IpAddress = "127.0.0.1"
            });
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al registrar auditoría: {ex.Message}");
        }
    }

    private static (string hash, string salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
        string salt = Convert.ToBase64String(saltBytes);

        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        string hash = Convert.ToBase64String(hashBytes);
        return (hash, salt);
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        byte[] saltBytes = Convert.FromBase64String(storedSalt);
        byte[] computedHashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        string computedHash = Convert.ToBase64String(computedHashBytes);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(storedHash));
    }

    public static UserPermissions CreateDefaultPermissionsForRole(SystemUserRole role)
    {
        return role switch
        {
            SystemUserRole.Administrador => new UserPermissions
            {
                CanOpenCashRegister = true,
                CanCloseCashRegister = true,
                CanPerformBlindCount = true,
                CanEmitInvoices = true,
                CanEmitCreditNotes = true,
                CanCancelSales = true,
                CanManagePurchases = true,
                CanManageCredits = true,
                CanManageExpenses = true,
                CanViewFiscalBooks = true,
                CanViewProductCosts = true,
                CanUpdateExchangeRates = true,
                CanManageUsers = true
            },
            SystemUserRole.Supervisor => new UserPermissions
            {
                CanOpenCashRegister = true,
                CanCloseCashRegister = true,
                CanPerformBlindCount = true,
                CanEmitInvoices = true,
                CanEmitCreditNotes = true,
                CanCancelSales = true,
                CanManagePurchases = true,
                CanManageCredits = true,
                CanManageExpenses = true,
                CanViewFiscalBooks = true,
                CanViewProductCosts = true,
                CanUpdateExchangeRates = false,
                CanManageUsers = false
            },
            SystemUserRole.Contador => new UserPermissions
            {
                CanOpenCashRegister = false,
                CanCloseCashRegister = false,
                CanPerformBlindCount = false,
                CanEmitInvoices = false,
                CanEmitCreditNotes = false,
                CanCancelSales = false,
                CanManagePurchases = true,
                CanManageCredits = true,
                CanManageExpenses = true,
                CanViewFiscalBooks = true,
                CanViewProductCosts = true,
                CanUpdateExchangeRates = false,
                CanManageUsers = false
            },
            SystemUserRole.Vendedor => new UserPermissions
            {
                CanOpenCashRegister = false,
                CanCloseCashRegister = false,
                CanPerformBlindCount = false,
                CanEmitInvoices = true,
                CanEmitCreditNotes = false,
                CanCancelSales = false,
                CanManagePurchases = false,
                CanManageCredits = true,
                CanManageExpenses = false,
                CanViewFiscalBooks = false,
                CanViewProductCosts = false,
                CanUpdateExchangeRates = false,
                CanManageUsers = false
            },
            _ => new UserPermissions // Cajero
            {
                CanOpenCashRegister = true,
                CanCloseCashRegister = true,
                CanPerformBlindCount = true,
                CanEmitInvoices = true,
                CanEmitCreditNotes = false,
                CanCancelSales = false,
                CanManagePurchases = false,
                CanManageCredits = true,
                CanManageExpenses = false,
                CanViewFiscalBooks = false,
                CanViewProductCosts = false,
                CanUpdateExchangeRates = false,
                CanManageUsers = false
            }
        };
    }

    private static async Task SeedDefaultAdminUserAsync(DeterposDbContext context)
    {
        var (hash, salt) = HashPassword("Admin1234!");

        var admin = new User
        {
            Username = "admin",
            FullName = "Administrador del Sistema",
            Email = "admin@deterpos.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = SystemUserRole.Administrador,
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var perms = CreateDefaultPermissionsForRole(SystemUserRole.Administrador);
        perms.UserId = admin.Id;
        context.UserPermissions.Add(perms);
        await context.SaveChangesAsync();

        context.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            Username = "Sistema",
            Module = "Seguridad",
            Action = "Inicializacion",
            Details = "Usuario administrador maestro creado por defecto.",
            IpAddress = "127.0.0.1"
        });
        await context.SaveChangesAsync();
    }
}
