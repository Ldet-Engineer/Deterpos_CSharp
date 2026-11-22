using Deterpos.Application.Common;
using Deterpos.Application.DTOs;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Deterpos.Tests;

public class DatabaseConnectionAndModuleTests
{
    private readonly ITestOutputHelper _output;
    private readonly DeterposDbContextFactory _factory;

    public DatabaseConnectionAndModuleTests(ITestOutputHelper output)
    {
        _output = output;
        var config = new DatabaseConfigurationService();
        _factory = new DeterposDbContextFactory(config);
    }

    private DeterposDbContext CreateDbContext()
    {
        return _factory.CreateDbContext();
    }

    [Fact]
    public async Task Test01_PostgreSql_Connection_CanConnect()
    {
        await using var context = CreateDbContext();
        bool canConnect = await context.Database.CanConnectAsync();
        _output.WriteLine($"PostgreSQL Connection Status: {canConnect}");
        Assert.True(canConnect, "No se pudo conectar a PostgreSQL en localhost:5432.");
    }

    [Fact]
    public async Task Test02_Verify_Products_And_Categories()
    {
        await using var context = CreateDbContext();
        var categories = await context.Categories.ToListAsync();
        var products = await context.Products.ToListAsync();

        _output.WriteLine($"Categorías encontradas: {categories.Count}");
        foreach (var c in categories)
        {
            _output.WriteLine($" - [{c.Id}] {c.Name}");
        }

        _output.WriteLine($"Productos encontrados: {products.Count}");
        foreach (var p in products)
        {
            _output.WriteLine($" - [{p.Id}] {p.Code} - {p.Name} | Stock: {p.CurrentStock} | Precio: ${p.PriceUSD:N2} | IVA: {p.VatRate}");
        }

        Assert.NotEmpty(categories);
        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task Test03_Verify_Customers_And_Suppliers()
    {
        await using var context = CreateDbContext();
        var customers = await context.Customers.ToListAsync();
        var suppliers = await context.Suppliers.ToListAsync();

        _output.WriteLine($"Clientes registrados: {customers.Count}");
        foreach (var c in customers)
        {
            _output.WriteLine($" - [{c.Id}] {c.DocumentNumber} - {c.FullName} | Saldo: ${c.CurrentBalanceUSD:N2}");
        }

        _output.WriteLine($"Proveedores registrados: {suppliers.Count}");
        foreach (var s in suppliers)
        {
            _output.WriteLine($" - [{s.Id}] {s.DocumentNumber} - {s.CompanyName} | Deuda: ${s.CurrentDebtUSD:N2}");
        }

        Assert.NotEmpty(customers);
    }

    [Fact]
    public async Task Test04_Verify_CashRegister_And_Shifts()
    {
        await using var context = CreateDbContext();
        var registers = await context.CashRegisters.ToListAsync();
        var shifts = await context.CashShifts.OrderByDescending(s => s.Id).ToListAsync();
        var sequences = await context.CashRegisterSequences.ToListAsync();

        _output.WriteLine($"Cajas registradoras: {registers.Count}");
        foreach (var r in registers)
        {
            _output.WriteLine($" - [{r.Id}] {r.Name} ({r.TerminalIdentifier})");
        }

        _output.WriteLine($"Turnos de caja: {shifts.Count}");
        foreach (var s in shifts)
        {
            _output.WriteLine($" - Turno #{s.Id} | CajaId: {s.CashRegisterId} | Cajero: {s.CashierUsername} | Estado: {s.Status} | Esperado USD: ${s.ExpectedAmountUSD:N2}");
        }

        _output.WriteLine($"Secuencias de correlativos: {sequences.Count}");
        foreach (var seq in sequences)
        {
            _output.WriteLine($" - Caja {seq.CashRegisterId} | Prefijo: {seq.Prefix} | Último N°: {seq.CurrentNumber}");
        }

        Assert.NotEmpty(registers);
    }

    [Fact]
    public async Task Test05_Verify_Purchases()
    {
        await using var context = CreateDbContext();
        var purchases = await context.Purchases
            .Include(p => p.Details)
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        _output.WriteLine($"Compras registradas: {purchases.Count}");
        foreach (var p in purchases)
        {
            _output.WriteLine($" - Compra #{p.Id} | Doc: {p.InvoiceNumber} | Total: ${p.TotalUSD:N2} | Items: {p.Details.Count}");
        }
    }

    [Fact]
    public async Task Test06_Verify_Sales_And_Fiscal_Data()
    {
        await using var context = CreateDbContext();
        var sales = await context.Sales
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .OrderByDescending(s => s.Id)
            .ToListAsync();

        _output.WriteLine($"Ventas registradas: {sales.Count}");
        foreach (var s in sales)
        {
            _output.WriteLine($" - Venta #{s.Id} | Factura: {s.InvoiceNumber} | Total: ${s.TotalUSD:N2} | Reporte Z: {s.FiscalZReportNumber} | Pagos: {s.Payments.Count}");
        }
    }

    [Fact]
    public async Task Test07_Verify_Currencies()
    {
        await using var context = CreateDbContext();
        var currencies = await context.Currencies.ToListAsync();
        _output.WriteLine($"Monedas configuradas: {currencies.Count}");
        foreach (var c in currencies)
        {
            _output.WriteLine($" - {c.Code} ({c.Symbol}): Tasa a USD = {c.CurrentRateToUSD} | Base: {c.IsBaseCurrency} | Principal: {c.IsPrimarySecondary}");
        }
        Assert.NotEmpty(currencies);
    }

    [Fact]
    public async Task Test08_Simulate_ProcessSale_To_Identify_InnerException()
    {
        var saleService = new SaleService(_factory);
        await using var context = CreateDbContext();

        var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Status == CashShiftStatus.Abierta);
        if (shift == null)
        {
            _output.WriteLine("No hay turno abierto. Se creará uno temporal para la prueba.");
            shift = new CashShift
            {
                CashRegisterId = 1,
                CashierUsername = "Administrador",
                OpenedAt = DateTime.UtcNow,
                OpeningAmountUSD = 50,
                OpeningAmountVES = 0,
                ExpectedAmountUSD = 50,
                ExpectedAmountVES = 0,
                Status = CashShiftStatus.Abierta
            };
            context.CashShifts.Add(shift);
            await context.SaveChangesAsync();
        }

        var customer = await context.Customers.FirstOrDefaultAsync(c => c.DocumentNumber == "V-00000000");
        var product = await context.Products.FirstOrDefaultAsync(p => p.Code == "PROD-005") ?? await context.Products.FirstAsync();

        var saleDraft = new Sale
        {
            CashRegisterId = shift.CashRegisterId,
            CashShiftId = shift.Id,
            CashierUsername = shift.CashierUsername,
            CustomerId = customer?.Id,
            FiscalPrinterSerial = "Z4A9876543",
            Notes = "Test Simulation"
        };

        var details = new List<SaleDetail>
        {
            new SaleDetail
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.Name,
                Quantity = 1,
                UnitCostUSD = product.CostUSD,
                UnitPriceUSD = product.PriceUSD,
                VatRate = product.VatRate
            }
        };

        decimal baseAmount = product.PriceUSD;
        decimal tax = product.VatRate == VatRateType.General16 ? decimal.Round(baseAmount * 0.16m, 4) : 0m;
        decimal total = baseAmount + tax;

        var payments = new List<SalePayment>
        {
            new SalePayment
            {
                Method = PaymentMethodType.EfectivoUSD,
                CurrencyCode = "USD",
                AmountOriginal = total,
                ExchangeRateApplied = 1.0000m,
                AmountEquivalentUSD = total,
                ReferenceNumber = "TEST-REF"
            }
        };

        try
        {
            _output.WriteLine($"Intentando simular ProcessSaleAsync con ShiftId={shift.Id}, ProductId={product.Id}, Total={total} USD...");
            var completedSale = await saleService.ProcessSaleAsync(saleDraft, details, payments, 55.40m, true);
            _output.WriteLine($"¡Venta procesada exitosamente! Factura: {completedSale.InvoiceNumber}, Total: ${completedSale.TotalUSD}");
            Assert.NotNull(completedSale);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"ERROR CAPTURADO: {ex.Message}");
            var currentEx = ex;
            int level = 1;
            while (currentEx.InnerException != null)
            {
                currentEx = currentEx.InnerException;
                _output.WriteLine($"INNER EXCEPTION [{level}]: {currentEx.GetType().FullName}: {currentEx.Message}");
                level++;
            }
            throw;
        }
    }

    [Fact]
    public void Test09_ParkedTickets_Manager_Park_And_Restore()
    {
        ParkedTicketsManager.ClearAll();
        Assert.Equal(0, ParkedTicketsManager.Count);

        var items = new List<Deterpos.Application.Common.ParkedCartItemDraft>
        {
            new() { ProductId = 1, ProductCode = "PROD-001", ProductName = "Harina", Quantity = 2, UnitPriceUSD = 1.30m, VatRate = VatRateType.Exento0 },
            new() { ProductId = 2, ProductCode = "PROD-002", ProductName = "Arroz", Quantity = 3, UnitPriceUSD = 1.20m, VatRate = VatRateType.Exento0 }
        };

        var ticket = ParkedTicketsManager.ParkCart("Ticket Espera Test #1", 1, "V-00000000", "Cliente Genérico", 55.40m, items);
        Assert.NotNull(ticket);
        Assert.Equal(1, ParkedTicketsManager.Count);
        _output.WriteLine($"Ticket en espera creado con éxito: ID #{ticket.Id} - Total: ${ticket.TotalUSD:N2}");

        var restored = ParkedTicketsManager.Restore(ticket.Id);
        Assert.NotNull(restored);
        Assert.Equal(2, restored.Items.Count);
        Assert.Equal(0, ParkedTicketsManager.Count);
        _output.WriteLine($"Ticket #{ticket.Id} recuperado del almacén temporal con éxito.");
    }

    [Fact]
    public async Task Test10_Quotations_Create_And_Verify_NoStockImpact()
    {
        var quotationService = new QuotationService(_factory);
        await using var context = CreateDbContext();

        var product = await context.Products.FirstAsync();
        decimal initialStock = product.CurrentStock;

        var draft = new Quotation
        {
            CustomerDocument = "V-12345678",
            CustomerName = "Empresa Cliente Test C.A.",
            CustomerPhone = "0414-1234567",
            ExchangeRateApplied = 55.40m,
            ValidUntil = DateTime.UtcNow.AddDays(7),
            Notes = "Cotización de prueba para verificación automatizada"
        };

        var details = new List<QuotationDetail>
        {
            new()
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.Name,
                Quantity = 5,
                UnitCostUSD = product.CostUSD,
                UnitPriceUSD = product.PriceUSD,
                VatRate = product.VatRate
            }
        };

        var saved = await quotationService.CreateQuotationAsync(draft, details);
        _output.WriteLine($"Cotización creada: N° {saved.QuotationNumber} | Total: ${saved.TotalUSD:N2} | Total VES: {saved.TotalVES:N2} Bs.");

        Assert.NotNull(saved);
        Assert.StartsWith("COT-", saved.QuotationNumber);
        Assert.Equal(QuotationStatus.Pendiente, saved.Status);

        // Verificar que el stock NO fue descontado (la cotización no es fiscal ni mueve almacén)
        var verifyProduct = await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
        Assert.NotNull(verifyProduct);
        Assert.Equal(initialStock, verifyProduct.CurrentStock);
        _output.WriteLine($"Stock inalterado verificado: {verifyProduct.CurrentStock} (Igual a inicial {initialStock})");

        var allQuotes = await quotationService.GetAllQuotationsAsync();
        Assert.Contains(allQuotes, q => q.QuotationNumber == saved.QuotationNumber);
    }

    [Fact]
    public async Task Test11_CreditSale_And_CreditNote_Reduces_CustomerBalance_And_Updates_Statement()
    {
        var saleService = new SaleService(_factory);
        var creditService = new CreditService(_factory);
        await using var context = CreateDbContext();

        var customer = await context.Customers.FirstOrDefaultAsync(c => c.DocumentNumber == "V-12345678");
        if (customer == null)
        {
            customer = new Customer
            {
                DocumentNumber = "V-12345678",
                FullName = "Cliente de Prueba Crédito",
                Phone = "0412-0000000",
                CreditLimitUSD = 1000m,
                CurrentBalanceUSD = 0m
            };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
        }

        var product = await context.Products.FirstAsync();
        var shift = await context.CashShifts.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
        int shiftId = shift?.Id ?? 1;

        decimal initialBalance = customer.CurrentBalanceUSD;
        decimal saleQty = 2m;
        decimal unitPrice = 10.00m;
        decimal saleTotal = saleQty * unitPrice;

        // 1. Procesar Venta a Crédito
        var saleDraft = new Sale
        {
            CashRegisterId = 1,
            CashShiftId = shiftId,
            CashierUsername = "TestRunner",
            CustomerId = customer.Id,
            FiscalPrinterSerial = "Z4A9876543",
            Notes = "Venta Test Crédito"
        };

        var details = new List<SaleDetail>
        {
            new()
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.Name,
                Quantity = saleQty,
                UnitCostUSD = product.CostUSD,
                UnitPriceUSD = unitPrice,
                VatRate = VatRateType.Exento0
            }
        };

        var payments = new List<SalePayment>
        {
            new()
            {
                Method = PaymentMethodType.Credito,
                CurrencyCode = "USD",
                AmountOriginal = saleTotal,
                ExchangeRateApplied = 1.0000m,
                AmountEquivalentUSD = saleTotal,
                ReferenceNumber = "CRED-TEST"
            }
        };

        var completedSale = await saleService.ProcessSaleAsync(saleDraft, details, payments, 55.40m, isSpecialTaxpayer: false);
        Assert.NotNull(completedSale);

        await using var verifyContext1 = CreateDbContext();
        var custAfterSale = await verifyContext1.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customer.Id);
        Assert.NotNull(custAfterSale);
        Assert.Equal(initialBalance + saleTotal, custAfterSale.CurrentBalanceUSD);
        _output.WriteLine($"Venta a crédito procesada: Factura {completedSale.InvoiceNumber} | Deuda incrementada: ${custAfterSale.CurrentBalanceUSD:N2}");

        // 2. Procesar Devolución (Nota de Crédito) sobre esa venta a crédito (sin egresar efectivo)
        var returnDrafts = new List<CreditNoteItemDraft>
        {
            new()
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.Name,
                OriginalQuantity = saleQty,
                ReturnQuantity = 1m, // Devolver 1 unidad = $10
                UnitPriceUSD = unitPrice,
                VatRate = VatRateType.Exento0
            }
        };

        var creditNote = await saleService.ProcessCreditNoteAsync(
            completedSale.Id,
            returnDrafts,
            "Devolución parcial de prueba",
            refundToCashRegister: false, // Debe acreditar a cuenta del cliente
            shiftId,
            "TestRunner");

        Assert.NotNull(creditNote);
        _output.WriteLine($"Nota de Crédito emitida: {creditNote.InvoiceNumber} | Monto acreditado: ${creditNote.TotalUSD:N2}");

        // 3. Verificar que la deuda del cliente DISMINUYÓ en exactamente el total de la Nota de Crédito
        await using var verifyContext2 = CreateDbContext();
        var custAfterNC = await verifyContext2.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customer.Id);
        Assert.NotNull(custAfterNC);
        decimal expectedBalance = initialBalance + saleTotal - creditNote.TotalUSD;
        Assert.Equal(expectedBalance, custAfterNC.CurrentBalanceUSD);
        _output.WriteLine($"Saldo deudor actualizado verificado: ${custAfterNC.CurrentBalanceUSD:N2} (Reducido por NC)");

        // 4. Verificar Estado de Cuenta
        var statement = await creditService.GetCustomerStatementAsync(customer.Id, 55.40m);
        Assert.NotEmpty(statement);

        var ncItem = statement.FirstOrDefault(i => i.DocumentNumber == creditNote.InvoiceNumber);
        Assert.NotNull(ncItem);
        Assert.Equal("Nota de Crédito Fiscal", ncItem.DocumentType);
        Assert.Equal(creditNote.TotalUSD, ncItem.CreditUSD); // En el HABER
        Assert.Equal(0m, ncItem.DebitUSD); // 0 en el DEBE
        _output.WriteLine($"Estado de cuenta verificado: {ncItem.DocumentType} {ncItem.DocumentNumber} imputado correctamente en Crédito/Haber por ${ncItem.CreditUSD:N2}");
    }

    [Fact]
    public async Task Test12_CashRegister_CalculatesMovements_Including_Abono_Devolucion_PagoProveedor()
    {
        var cashService = new CashRegisterService(_factory);
        await using var context = CreateDbContext();

        var shift = await cashService.GetActiveShiftAsync(1);
        if (shift == null)
        {
            shift = await cashService.OpenShiftAsync(1, "TestRunner", 100m, 5000m, "Turno de prueba");
        }

        decimal initialExpectedUSD = shift.ExpectedAmountUSD;

        // Registrar AbonoCliente ($50 entrada)
        var movAbono = await cashService.RegisterMovementAsync(
            shift.Id,
            TransactionType.AbonoCliente,
            50m,
            2770m,
            "Abono Cliente Test",
            "Supervisor");
        Assert.NotNull(movAbono);

        // Registrar EgresoDevolucion ($20 salida)
        var movDev = await cashService.RegisterMovementAsync(
            shift.Id,
            TransactionType.EgresoDevolucion,
            20m,
            1108m,
            "Devolución Reembolso Efectivo Test",
            "Supervisor");
        Assert.NotNull(movDev);

        // Registrar PagoProveedor ($15 salida)
        var movPagoProv = await cashService.RegisterMovementAsync(
            shift.Id,
            TransactionType.PagoProveedor,
            15m,
            831m,
            "Pago Proveedor CxP Test",
            "Supervisor");
        Assert.NotNull(movPagoProv);

        // Consultar el turno activo y verificar que el saldo esperado incluye los 3 movimientos (+50 -20 -15 = +15)
        var updatedShift = await cashService.GetActiveShiftAsync(1);
        Assert.NotNull(updatedShift);

        decimal netExpectedUSD = initialExpectedUSD + 50m - 20m - 15m;
        Assert.Equal(netExpectedUSD, updatedShift.ExpectedAmountUSD);

        _output.WriteLine($"Arqueo teórico de caja verificado: Saldo esperado ${updatedShift.ExpectedAmountUSD:N2} (Entrada Abono: +$50, Salida Devolución: -$20, Salida Proveedor: -$15).");
    }

    [Fact]
    public async Task Test13_Inventory_Kardex_Tracks_Movements()
    {
        var config = new DatabaseConfigurationService();
        await config.InitializeDatabaseAsync();

        var kardexService = new KardexService(_factory);
        var productService = new ProductService(_factory);

        // 1. Obtener o crear un producto exclusivo para la prueba de kárdex
        await using var initContext = CreateDbContext();
        var product = await initContext.Products.FirstOrDefaultAsync(p => p.Code == "TEST-KDX-01");
        if (product == null)
        {
            var cat = await initContext.Categories.FirstAsync();
            product = new Product
            {
                Code = "TEST-KDX-01",
                Barcode = "75999990001",
                Name = "Producto Prueba Kárdex Deterpos",
                Description = "Item para pruebas de trazabilidad y kárdex",
                CategoryName = cat.Name,
                CurrentStock = 10m,
                MinimumStock = 2m,
                CostUSD = 15m,
                PriceUSD = 25m,
                VatRate = VatRateType.General16,
                IsActive = true
            };
            initContext.Products.Add(product);
            await initContext.SaveChangesAsync();
        }

        // Consultar stock actual de la base de datos
        var currentProd = await productService.GetProductByIdAsync(product.Id);
        Assert.NotNull(currentProd);
        var stockBefore = currentProd.CurrentStock;

        // 2. Realizar un ajuste positivo de stock (+5 unidades)
        var stockPositive = stockBefore + 5m;
        await productService.AdjustStockAsync(product.Id, stockPositive, "Ajuste de Conteo Físico Test", "Auditor");

        // 3. Realizar un ajuste negativo de stock (-2 unidades)
        var stockNegative = stockPositive - 2m;
        await productService.AdjustStockAsync(product.Id, stockNegative, "Merma / Daño en Almacén", "Auditor");

        // 4. Consultar el Kárdex del producto mediante KardexService
        var kardex = await kardexService.GetProductKardexAsync(product.Id);

        _output.WriteLine($"Movimientos en Kárdex para [{product.Code}] {product.Name}: {kardex.Count}");
        foreach (var m in kardex)
        {
            _output.WriteLine($" - {m.MovementDate:yyyy-MM-dd HH:mm} | Tipo: {m.MovementType} | Doc: {m.DocumentNumber} | Entrada: {m.QuantityIn} | Salida: {m.QuantityOut} | Stock Res: {m.ResultingStock} | CPP: ${m.ResultingUnitCostUSD:N2}");
        }

        Assert.NotEmpty(kardex);

        // 5. Verificar que el último movimiento refleja exactamente el stock final esperado
        var lastMovement = kardex.Last();
        Assert.Equal(stockNegative, lastMovement.ResultingStock);

        // 6. Verificar que contiene tanto un AjustePositivo como un AjusteNegativo
        Assert.Contains(kardex, m => m.MovementType == InventoryMovementType.AjustePositivo && m.QuantityIn == 5m);
        Assert.Contains(kardex, m => m.MovementType == InventoryMovementType.AjusteNegativo && m.QuantityOut == 2m);
    }

    [Fact]
    public async Task Test14_Fiscal_Export_TaxSummary_Excel()
    {
        var config = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(config);
        var fiscalService = new FiscalBookService(factory);
        var company = new CompanyFiscalProfile
        {
            BusinessName = "DISTRIBUIDORA DETERPOS C.A.",
            RifPrefix = "J",
            RifNumber = "501234567",
            FiscalAddress = "Av. Las Delicias, Centro Empresarial, Maracay, Aragua"
        };

        int year = DateTime.Now.Year;
        int month = DateTime.Now.Month;
        string tempFile = Path.Combine(Path.GetTempPath(), $"Test_TaxSummary_{Guid.NewGuid():N}.csv");

        try
        {
            var resultPath = await fiscalService.ExportTaxSummaryToCsvAsync(year, month, tempFile, company);
            _output.WriteLine($"Archivo de Resumen Tributario generado en: {resultPath}");

            Assert.True(File.Exists(resultPath), "El archivo exportado no existe.");
            var content = await File.ReadAllTextAsync(resultPath, System.Text.Encoding.UTF8);
            Assert.NotEmpty(content);
            Assert.Contains("RESUMEN TRIBUTARIO FISCAL SENIAT", content);
            Assert.Contains("DETERMINACIÓN DE DÉBITOS FISCALES (VENTAS)", content);
            Assert.Contains("DETERMINACIÓN DE CRÉDITOS FISCALES (COMPRAS)", content);
            Assert.Contains("AUTOLIQUIDACIÓN Y RESULTADO DE LA FORMA 30", content);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task Test15_Database_Backup_Service_Create_And_Test()
    {
        var config = new DatabaseConfigurationService();
        var backupService = new DatabaseBackupService(config);

        var p = backupService.GetCurrentConnectionParams();
        var testResult = await backupService.TestConnectionAsync(p);
        _output.WriteLine($"TestConnection: Success={testResult.Success}, Msg={testResult.Message}, Tables={testResult.TablesCount}");
        Assert.True(testResult.Success, $"Fallo de conexión a PostgreSQL: {testResult.Message}");

        string tempBackupFile = Path.Combine(Path.GetTempPath(), $"Test_Backup_{Guid.NewGuid():N}.sql");
        try
        {
            var backupResult = await backupService.CreateBackupAsync(tempBackupFile, p);
            _output.WriteLine($"BackupResult: Success={backupResult.Success}, Msg={backupResult.Message}, Size={backupResult.FileSizeBytes}");

            Assert.True(backupResult.Success, $"Fallo creando respaldo: {backupResult.Message}");
            Assert.True(File.Exists(tempBackupFile), "El archivo de respaldo SQL no fue generado.");
            Assert.True(new FileInfo(tempBackupFile).Length > 0, "El archivo de respaldo SQL está vacío.");
        }
        finally
        {
            if (File.Exists(tempBackupFile))
            {
                File.Delete(tempBackupFile);
            }
        }
    }
}
