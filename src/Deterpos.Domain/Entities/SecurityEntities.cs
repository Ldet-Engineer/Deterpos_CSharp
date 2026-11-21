using System;

namespace Deterpos.Domain.Entities;

public enum SystemUserRole
{
    Administrador,
    Supervisor,
    Cajero,
    Vendedor,
    Contador
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public SystemUserRole Role { get; set; } = SystemUserRole.Cajero;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserPermissions? Permissions { get; set; }
}

public class UserPermissions
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public bool CanOpenCashRegister { get; set; } = true;
    public bool CanCloseCashRegister { get; set; } = true;
    public bool CanPerformBlindCount { get; set; } = true;
    public bool CanEmitInvoices { get; set; } = true;
    public bool CanEmitCreditNotes { get; set; } = false;
    public bool CanCancelSales { get; set; } = false;
    public bool CanManagePurchases { get; set; } = false;
    public bool CanManageCredits { get; set; } = true;
    public bool CanManageExpenses { get; set; } = false;
    public bool CanViewFiscalBooks { get; set; } = false;
    public bool CanViewProductCosts { get; set; } = false;
    public bool CanUpdateExchangeRates { get; set; } = false;
    public bool CanManageUsers { get; set; } = false;
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Username { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "127.0.0.1";
}
