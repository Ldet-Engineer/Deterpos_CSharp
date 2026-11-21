using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Data;

public class DeterposDbContext : DbContext, IApplicationDbContext
{
    public DeterposDbContext(DbContextOptions<DeterposDbContext> options) : base(options)
    {
    }

    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleDetail> SaleDetails => Set<SaleDetail>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<CashRegisterSequence> CashRegisterSequences => Set<CashRegisterSequence>();
    public DbSet<CompanyFiscalProfile> CompanyFiscalProfiles => Set<CompanyFiscalProfile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseDetail> PurchaseDetails => Set<PurchaseDetail>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<OperatingExpense> OperatingExpenses => Set<OperatingExpense>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermissions> UserPermissions => Set<UserPermissions>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationDetail> QuotationDetails => Set<QuotationDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Currencies
        modelBuilder.Entity<Currency>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.CurrentRateToUSD).HasPrecision(18, 4);

            entity.HasData(
                new Currency
                {
                    Id = 1,
                    Code = "USD",
                    Name = "Dólar Estadounidense",
                    Symbol = "$",
                    IsBaseCurrency = true,
                    IsPrimarySecondary = false,
                    IsActive = true,
                    DecimalPlaces = 2,
                    CurrentRateToUSD = 1.0000m,
                    LastRateUpdate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Currency
                {
                    Id = 2,
                    Code = "VES",
                    Name = "Bolívar Digital",
                    Symbol = "Bs.",
                    IsBaseCurrency = false,
                    IsPrimarySecondary = true,
                    IsActive = true,
                    DecimalPlaces = 2,
                    CurrentRateToUSD = 55.4000m,
                    LastRateUpdate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Currency
                {
                    Id = 3,
                    Code = "EUR",
                    Name = "Euro",
                    Symbol = "€",
                    IsBaseCurrency = false,
                    IsPrimarySecondary = false,
                    IsActive = false,
                    DecimalPlaces = 2,
                    CurrentRateToUSD = 0.9500m,
                    LastRateUpdate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Currency
                {
                    Id = 4,
                    Code = "COP",
                    Name = "Peso Colombiano",
                    Symbol = "Col$",
                    IsBaseCurrency = false,
                    IsPrimarySecondary = false,
                    IsActive = false,
                    DecimalPlaces = 0,
                    CurrentRateToUSD = 4200.0000m,
                    LastRateUpdate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        });

        // ExchangeRate
        modelBuilder.Entity<ExchangeRate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CurrencyCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.RateToUSD).HasPrecision(18, 4);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.Property(e => e.RegisteredBy).HasMaxLength(50);
        });

        // Categories
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasData(
                new Category { Id = 1, Name = "Alimentos", Description = "Víveres y productos de canasta básica" },
                new Category { Id = 2, Name = "Bebidas", Description = "Refrescos, jugos y aguas" },
                new Category { Id = 3, Name = "Limpieza", Description = "Detergentes y productos de higiene del hogar" },
                new Category { Id = 4, Name = "Snacks", Description = "Golosinas y galletas" },
                new Category { Id = 5, Name = "Cuidado Personal", Description = "Champú, jabones y aseo" },
                new Category { Id = 6, Name = "General", Description = "Artículos varios" }
            );
        });

        // Products
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Barcode).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CostUSD).HasPrecision(18, 4);
            entity.Property(e => e.PriceUSD).HasPrecision(18, 4);
            entity.Property(e => e.CurrentStock).HasPrecision(18, 2);
            entity.Property(e => e.MinimumStock).HasPrecision(18, 2);

            entity.HasData(
                new Product
                {
                    Id = 1,
                    Code = "PROD-001",
                    Barcode = "7591001001015",
                    Name = "Harina de Maíz Precocida 1Kg",
                    Description = "Harina blanca enriquecida",
                    CategoryName = "Alimentos",
                    CostUSD = 0.9500m,
                    PriceUSD = 1.3000m,
                    CurrentStock = 65.00m,
                    MinimumStock = 15.00m,
                    VatRate = VatRateType.Exento0,
                    IsActive = true
                },
                new Product
                {
                    Id = 2,
                    Code = "PROD-002",
                    Barcode = "7591002002028",
                    Name = "Arroz Blanco Tradicional 1Kg",
                    Description = "Arroz de grano largo tipo 1",
                    CategoryName = "Alimentos",
                    CostUSD = 0.8500m,
                    PriceUSD = 1.2000m,
                    CurrentStock = 45.00m,
                    MinimumStock = 10.00m,
                    VatRate = VatRateType.Exento0,
                    IsActive = true
                },
                new Product
                {
                    Id = 3,
                    Code = "PROD-003",
                    Barcode = "7591003003031",
                    Name = "Refresco Cola 2 Litros",
                    Description = "Bebida gaseosa sabor cola",
                    CategoryName = "Bebidas",
                    CostUSD = 1.5000m,
                    PriceUSD = 2.2000m,
                    CurrentStock = 30.00m,
                    MinimumStock = 12.00m,
                    VatRate = VatRateType.General16,
                    IsActive = true
                },
                new Product
                {
                    Id = 4,
                    Code = "PROD-004",
                    Barcode = "7591004004044",
                    Name = "Detergente Multiuso Floral 1Kg",
                    Description = "Detergente en polvo para ropa",
                    CategoryName = "Limpieza",
                    CostUSD = 1.2000m,
                    PriceUSD = 1.8500m,
                    CurrentStock = 28.00m,
                    MinimumStock = 8.00m,
                    VatRate = VatRateType.General16,
                    IsActive = true
                },
                new Product
                {
                    Id = 5,
                    Code = "PROD-005",
                    Barcode = "7591005005057",
                    Name = "Aceite Vegetal Comestible 1L",
                    Description = "Aceite puro de soya",
                    CategoryName = "Alimentos",
                    CostUSD = 1.9000m,
                    PriceUSD = 2.6000m,
                    CurrentStock = 6.00m,
                    MinimumStock = 10.00m, // Stock bajo para alerta
                    VatRate = VatRateType.Exento0,
                    IsActive = true
                },
                new Product
                {
                    Id = 6,
                    Code = "PROD-006",
                    Barcode = "7591006006060",
                    Name = "Café Molido Premium 250g",
                    Description = "Café tostado y molido selección especial",
                    CategoryName = "Alimentos",
                    CostUSD = 1.6500m,
                    PriceUSD = 2.4500m,
                    CurrentStock = 18.00m,
                    MinimumStock = 5.00m,
                    VatRate = VatRateType.Exento0,
                    IsActive = true
                }
            );
        });

        // Customers
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DocumentNumber).IsUnique();
            entity.Property(e => e.DocumentNumber).HasMaxLength(30).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreditLimitUSD).HasPrecision(18, 2);
            entity.Property(e => e.CurrentBalanceUSD).HasPrecision(18, 2);
        });

        // CashRegisters (Caja 1 inicial)
        modelBuilder.Entity<CashRegister>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TerminalIdentifier).HasMaxLength(50).IsRequired();

            entity.HasData(
                new CashRegister
                {
                    Id = 1,
                    Name = "Caja Principal - Servidor",
                    TerminalIdentifier = "POS-01",
                    IsActive = true
                }
            );
        });

        // CashShifts
        modelBuilder.Entity<CashShift>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OpeningAmountUSD).HasPrecision(18, 2);
            entity.Property(e => e.OpeningAmountVES).HasPrecision(18, 2);
            entity.Property(e => e.ExpectedAmountUSD).HasPrecision(18, 2);
            entity.Property(e => e.ExpectedAmountVES).HasPrecision(18, 2);
            entity.Property(e => e.ActualAmountUSD).HasPrecision(18, 2);
            entity.Property(e => e.ActualAmountVES).HasPrecision(18, 2);

            entity.HasMany(e => e.Movements)
                  .WithOne()
                  .HasForeignKey(m => m.CashShiftId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CashMovements
        modelBuilder.Entity<CashMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountUSD).HasPrecision(18, 2);
            entity.Property(e => e.AmountVES).HasPrecision(18, 2);
            entity.Property(e => e.Description).HasMaxLength(250).IsRequired();
            entity.Property(e => e.AuthorizedBy).HasMaxLength(100);
        });

        // Sales & Details (Cumplimiento SENIAT)
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ControlNumber).HasMaxLength(50);
            entity.Property(e => e.FiscalPrinterSerial).HasMaxLength(50);
            entity.Property(e => e.FiscalAuditNumber).HasMaxLength(50);
            entity.Property(e => e.AffectedInvoiceNumber).HasMaxLength(50);
            entity.Property(e => e.AffectedFiscalSerial).HasMaxLength(50);

            entity.Property(e => e.SubtotalUSD).HasPrecision(18, 4);
            entity.Property(e => e.BaseGeneralUSD).HasPrecision(18, 4);
            entity.Property(e => e.IvaGeneralUSD).HasPrecision(18, 4);
            entity.Property(e => e.BaseReducidaUSD).HasPrecision(18, 4);
            entity.Property(e => e.IvaReducidaUSD).HasPrecision(18, 4);
            entity.Property(e => e.MontoExentoUSD).HasPrecision(18, 4);
            entity.Property(e => e.TaxAmountUSD).HasPrecision(18, 4);
            entity.Property(e => e.BaseIgtfUSD).HasPrecision(18, 4);
            entity.Property(e => e.IgtfAmountUSD).HasPrecision(18, 4);
            entity.Property(e => e.TotalUSD).HasPrecision(18, 4);
            entity.Property(e => e.AppliedRateVES).HasPrecision(18, 4);

            entity.HasMany(e => e.Details)
                  .WithOne()
                  .HasForeignKey(d => d.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Payments)
                  .WithOne()
                  .HasForeignKey(p => p.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SaleDetail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.UnitCostUSD).HasPrecision(18, 4);
            entity.Property(e => e.UnitPriceUSD).HasPrecision(18, 4);
            entity.Property(e => e.TotalPriceUSD).HasPrecision(18, 4);
            entity.Property(e => e.VatAmountUSD).HasPrecision(18, 4);
        });

        modelBuilder.Entity<SalePayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountOriginal).HasPrecision(18, 4);
            entity.Property(e => e.ExchangeRateApplied).HasPrecision(18, 4);
            entity.Property(e => e.AmountEquivalentUSD).HasPrecision(18, 4);
        });

        // CashRegisterSequence
        modelBuilder.Entity<CashRegisterSequence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CashRegisterId, e.Prefix }).IsUnique();
        });

        // CompanyFiscalProfile (Datos SENIAT de la Empresa)
        modelBuilder.Entity<CompanyFiscalProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BusinessName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TradeName).HasMaxLength(150);
            entity.Property(e => e.RifPrefix).HasMaxLength(2).IsRequired();
            entity.Property(e => e.RifNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FiscalAddress).HasMaxLength(300).IsRequired();
            entity.Property(e => e.IgtfPercentage).HasPrecision(5, 2);

            entity.HasData(
                new CompanyFiscalProfile
                {
                    Id = 1,
                    BusinessName = "Deterpos C.A.",
                    TradeName = "Deterpos",
                    RifPrefix = "J",
                    RifNumber = "409182730",
                    FiscalAddress = "Av. Principal, Edificio Empresarial, Piso 1, Caracas, Venezuela",
                    Phone = "0212-5551234",
                    Email = "contacto@deterpos.com",
                    IsSpecialTaxpayer = true,
                    IgtfPercentage = 3.00m,
                    PrinterBrand = FiscalPrinterBrand.TheFactoryHKA,
                    PortName = "COM1",
                    BaudRate = 9600
                }
            );
        });

        // Purchases & Details (Compras y Recepción de Mercancía)
        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ControlNumber).HasMaxLength(50);
            entity.Property(e => e.RegisteredBy).HasMaxLength(100);

            entity.Property(e => e.SubtotalUSD).HasPrecision(18, 4);
            entity.Property(e => e.BaseGeneralUSD).HasPrecision(18, 4);
            entity.Property(e => e.IvaGeneralUSD).HasPrecision(18, 4);
            entity.Property(e => e.BaseReducidaUSD).HasPrecision(18, 4);
            entity.Property(e => e.IvaReducidaUSD).HasPrecision(18, 4);
            entity.Property(e => e.MontoExentoUSD).HasPrecision(18, 4);
            entity.Property(e => e.TaxAmountUSD).HasPrecision(18, 4);
            entity.Property(e => e.TotalUSD).HasPrecision(18, 4);
            entity.Property(e => e.AppliedRateVES).HasPrecision(18, 4);

            entity.HasMany(e => e.Details)
                  .WithOne()
                  .HasForeignKey(d => d.PurchaseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseDetail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.UnitCostUSD).HasPrecision(18, 4);
            entity.Property(e => e.TotalPriceUSD).HasPrecision(18, 4);
            entity.Property(e => e.VatAmountUSD).HasPrecision(18, 4);
            entity.Property(e => e.PreviousCostUSD).HasPrecision(18, 4);
            entity.Property(e => e.NewCostUSD).HasPrecision(18, 4);
            entity.Property(e => e.PreviousStock).HasPrecision(18, 3);
            entity.Property(e => e.NewStock).HasPrecision(18, 3);
        });

        // CustomerPayments (Abonos de Clientes a CxC)
        modelBuilder.Entity<CustomerPayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountUSD).HasPrecision(18, 4);
            entity.Property(e => e.AmountOriginal).HasPrecision(18, 4);
            entity.Property(e => e.ExchangeRateApplied).HasPrecision(18, 4);
            entity.Property(e => e.RemainingBalanceUSD).HasPrecision(18, 4);
            entity.Property(e => e.CurrencyCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.ReceivedBy).HasMaxLength(100);
        });

        // SupplierPayments (Pagos a Proveedores CxP)
        modelBuilder.Entity<SupplierPayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountUSD).HasPrecision(18, 4);
            entity.Property(e => e.AmountOriginal).HasPrecision(18, 4);
            entity.Property(e => e.ExchangeRateApplied).HasPrecision(18, 4);
            entity.Property(e => e.RemainingDebtUSD).HasPrecision(18, 4);
            entity.Property(e => e.CurrencyCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.AuthorizedBy).HasMaxLength(100);
        });

        // OperatingExpenses (Gastos Operativos y Salidas de Caja Chica)
        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<OperatingExpense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(250).IsRequired();
            entity.Property(e => e.Beneficiary).HasMaxLength(150);
            entity.Property(e => e.ReceiptNumber).HasMaxLength(100);
            entity.Property(e => e.CurrencyCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.AmountOriginal).HasPrecision(18, 4);
            entity.Property(e => e.ExchangeRateApplied).HasPrecision(18, 4);
            entity.Property(e => e.AmountUSD).HasPrecision(18, 4);
            entity.Property(e => e.AuthorizedBy).HasMaxLength(100);

            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.ExpenseCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Security, Users & Audit
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.PasswordSalt).IsRequired();

            entity.HasOne(e => e.Permissions)
                  .WithOne(p => p.User)
                  .HasForeignKey<UserPermissions>(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPermissions>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Module).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(50);
        });

        // Quotations (Presupuestos / Proformas Comerciales)
        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QuotationNumber).IsUnique();
            entity.Property(e => e.QuotationNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerDocument).HasMaxLength(30);
            entity.Property(e => e.CustomerName).HasMaxLength(150);
            entity.Property(e => e.SubtotalUSD).HasPrecision(18, 4);
            entity.Property(e => e.TaxAmountUSD).HasPrecision(18, 4);
            entity.Property(e => e.TotalUSD).HasPrecision(18, 4);
            entity.Property(e => e.ExchangeRateApplied).HasPrecision(18, 4);

            entity.HasMany(e => e.Details)
                  .WithOne()
                  .HasForeignKey(d => d.QuotationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuotationDetail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.UnitCostUSD).HasPrecision(18, 4);
            entity.Property(e => e.UnitPriceUSD).HasPrecision(18, 4);
            entity.Property(e => e.TotalPriceUSD).HasPrecision(18, 4);
            entity.Property(e => e.VatAmountUSD).HasPrecision(18, 4);
        });
    }
}
