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
        if (product.CurrentStock < 10)
        {
            product.CurrentStock = 100;
            await context.SaveChangesAsync();
        }

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
        if (product.CurrentStock < 10)
        {
            product.CurrentStock = 100;
            await context.SaveChangesAsync();
        }
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
        if (product.CurrentStock < 10)
        {
            product.CurrentStock = 100;
            await context.SaveChangesAsync();
        }
        var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Status == CashShiftStatus.Abierta);
        if (shift == null)
        {
            shift = new CashShift
            {
                CashRegisterId = 1,
                CashierUsername = "TestRunner",
                OpeningAmountUSD = 100m,
                OpeningAmountVES = 5540m,
                ExpectedAmountUSD = 100m,
                ExpectedAmountVES = 5540m,
                Status = CashShiftStatus.Abierta,
                OpenedAt = DateTime.UtcNow
            };
            context.CashShifts.Add(shift);
            await context.SaveChangesAsync();
        }
        int shiftId = shift.Id;

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

        var openShifts = await context.CashShifts.Where(s => s.CashRegisterId == 1 && s.Status == CashShiftStatus.Abierta).ToListAsync();
        foreach (var s in openShifts)
        {
            s.Status = CashShiftStatus.Cerrada;
            s.ClosedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();

        var shift = await cashService.OpenShiftAsync(1, "TestRunner", 100m, 5000m, "Turno de prueba");
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

    [Fact]
    public void Test16_Cashea_Installments_Calculation_And_Levels()
    {
        var config = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(config);
        var casheaService = new CasheaService(factory);

        decimal totalAmount = 100.00m;
        decimal bcvRate = 55.40m;

        // 1. Probar Nivel 1 (60% Inicial / 40% Financiado en 3 cuotas)
        var planN1 = casheaService.CalculatePlan(totalAmount, 1, bcvRate);
        Assert.Equal(60.00m, planN1.DownPaymentUSD);
        Assert.Equal(40.00m, planN1.FinancedAmountUSD);
        Assert.Equal(3, planN1.Installments.Count);
        decimal sumQuotasN1 = planN1.Installments.Sum(i => i.AmountUSD);
        Assert.Equal(planN1.FinancedAmountUSD, sumQuotasN1);
        _output.WriteLine($"Nivel 1: Inicial=${planN1.DownPaymentUSD}, Financiado=${planN1.FinancedAmountUSD}, Cuotas={string.Join(", ", planN1.Installments.Select(i => $"${i.AmountUSD}"))}");

        // 2. Probar Nivel 2 (50% Inicial / 50% Financiado)
        var planN2 = casheaService.CalculatePlan(totalAmount, 2, bcvRate);
        Assert.Equal(50.00m, planN2.DownPaymentUSD);
        Assert.Equal(50.00m, planN2.FinancedAmountUSD);
        Assert.Equal(3, planN2.Installments.Count);
        decimal sumQuotasN2 = planN2.Installments.Sum(i => i.AmountUSD);
        Assert.Equal(planN2.FinancedAmountUSD, sumQuotasN2);

        // 3. Probar Nivel 3 (40% Inicial / 60% Financiado) - El más popular
        var planN3 = casheaService.CalculatePlan(totalAmount, 3, bcvRate);
        Assert.Equal(40.00m, planN3.DownPaymentUSD);
        Assert.Equal(60.00m, planN3.FinancedAmountUSD);
        Assert.Equal(3, planN3.Installments.Count);
        Assert.All(planN3.Installments, i => Assert.Equal(20.00m, i.AmountUSD));
        Assert.Equal(3.00m, planN3.EstimatedCommissionUSD); // 5% de $60 = $3.00

        // 4. Probar Inicial personalizada ($35.00 inicial)
        var planCustom = casheaService.CalculatePlan(totalAmount, 3, bcvRate, 35.00m);
        Assert.Equal(35.00m, planCustom.DownPaymentUSD);
        Assert.Equal(65.00m, planCustom.FinancedAmountUSD);
        decimal sumCustom = planCustom.Installments.Sum(i => i.AmountUSD);
        Assert.Equal(65.00m, sumCustom);
    }

    [Fact]
    public async Task Test17_Cashea_Order_Registration_Metrics_And_Settlement()
    {
        var config = new DatabaseConfigurationService();
        await config.InitializeDatabaseAsync();
        var factory = new DeterposDbContextFactory(config);
        var casheaService = new CasheaService(factory);

        string testOrderNum = $"CSH-TEST-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var order = new CasheaOrder
        {
            OrderNumber = testOrderNum,
            CustomerDocument = "V-28999888",
            CustomerName = "Cliente Prueba Cashea",
            CasheaLevel = 3,
            TotalSaleUSD = 80.00m,
            DownPaymentAmountUSD = 32.00m, // 40% inicial
            FinancedAmountUSD = 48.00m, // 60% financiado
            ExchangeRateApplied = 55.40m,
            TotalSaleVES = 80.00m * 55.40m,
            DownPaymentAmountVES = 32.00m * 55.40m,
            FinancedAmountVES = 48.00m * 55.40m,
            DownPaymentMethod = PaymentMethodType.PuntoDeVenta,
            DownPaymentReference = "POS-987123",
            InstallmentsCount = 3,
            InstallmentAmountUSD = 16.00m,
            InstallmentAmountVES = 16.00m * 55.40m,
            EstimatedCommissionPercentage = 5.0m,
            EstimatedCommissionUSD = 2.40m, // 5% de $48
            Status = CasheaOrderStatus.PorLiquidar,
            CreatedAt = DateTime.UtcNow
        };

        // 1. Registrar orden en base de datos
        var created = await casheaService.RegisterOrderAsync(order);
        Assert.True(created.Id > 0, "No se generó el Id para CasheaOrder.");
        _output.WriteLine($"Orden Cashea creada con Id: {created.Id}, Número: {created.OrderNumber}");

        // 2. Consultar la orden registrada
        var orders = await casheaService.GetOrdersAsync(search: testOrderNum);
        Assert.NotEmpty(orders);
        var fetched = orders.First();
        Assert.Equal(CasheaOrderStatus.PorLiquidar, fetched.Status);
        Assert.Equal(48.00m, fetched.FinancedAmountUSD);

        // 3. Consultar métricas
        var metrics = await casheaService.GetMetricsAsync();
        Assert.True(metrics.TotalOrdersCount > 0);
        Assert.True(metrics.TotalPendingSettlementUSD >= 48.00m);
        _output.WriteLine($"Métricas Cashea: Total={metrics.TotalOrdersCount}, Pendiente=${metrics.TotalPendingSettlementUSD:N2}, Liquidado=${metrics.TotalSettledUSD:N2}");

        // 4. Conciliar orden (marcar como liquidada en banco)
        string bankRef = "BANESCO-TRF-554433";
        bool settled = await casheaService.MarkOrderSettledAsync(created.Id, bankRef, DateTime.UtcNow);
        Assert.True(settled, "Fallo al conciliar orden Cashea.");

        // 5. Verificar estado actualizado
        var ordersAfter = await casheaService.GetOrdersAsync(search: testOrderNum);
        var updated = ordersAfter.First();
        Assert.Equal(CasheaOrderStatus.LiquidadaEnBanco, updated.Status);
        Assert.Equal(bankRef, updated.SettlementBankReference);
        _output.WriteLine($"Orden conciliar exitosamente: Estado={updated.Status}, Ref={updated.SettlementBankReference}");
    }

    [Fact]
    public async Task Test18_Pos_Cashea_Sale_Registration_And_Events()
    {
        var config = new DatabaseConfigurationService();
        await config.InitializeDatabaseAsync();
        var factory = new DeterposDbContextFactory(config);
        var saleService = new SaleService(factory);
        var casheaService = new CasheaService(factory);

        bool casheaEventFired = false;
        Deterpos.Application.Common.AppEvents.CasheaChanged += () => casheaEventFired = true;

        // Crear venta simulada en POS con 40% inicial y 60% Cashea
        decimal total = 50.00m;
        decimal bcv = 55.40m;
        decimal down = 20.00m;
        decimal financed = 30.00m;
        string orderNum = $"CSH-POS-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        await using var ctx = factory.CreateDbContext();
        var prod = await ctx.Products.FirstAsync(p => p.IsActive);
        if (prod.CurrentStock < 10)
        {
            prod.CurrentStock = 100;
            await ctx.SaveChangesAsync();
        }
        var shift = await ctx.CashShifts.FirstAsync(s => s.Status == CashShiftStatus.Abierta);

        var saleDraft = new Sale
        {
            CashRegisterId = 1,
            CashShiftId = shift.Id,
            CashierUsername = "TestCashier",
            FiscalPrinterSerial = "TESTFISCAL01",
            Notes = "Test POS Cashea BNPL"
        };

        var details = new List<SaleDetail>
        {
            new SaleDetail
            {
                ProductId = prod.Id,
                ProductCode = prod.Code,
                ProductName = prod.Name,
                Quantity = 1.0m,
                UnitPriceUSD = total,
                UnitCostUSD = prod.CostUSD,
                VatRate = VatRateType.General16
            }
        };

        var payments = new List<SalePayment>
        {
            new SalePayment
            {
                Method = PaymentMethodType.PuntoDeVenta,
                CurrencyCode = "VES",
                AmountOriginal = down * bcv,
                ExchangeRateApplied = bcv,
                AmountEquivalentUSD = down,
                ReferenceNumber = "POS-12345"
            },
            new SalePayment
            {
                Method = PaymentMethodType.Cashea,
                CurrencyCode = "USD",
                AmountOriginal = financed,
                ExchangeRateApplied = 1.0m,
                AmountEquivalentUSD = financed,
                ReferenceNumber = orderNum
            }
        };

        var completedSale = await saleService.ProcessSaleAsync(saleDraft, details, payments, bcv, false);
        Assert.True(completedSale.Id > 0);

        // Registrar orden Cashea como lo hace PosView
        var casheaOrder = new CasheaOrder
        {
            SaleId = completedSale.Id,
            OrderNumber = orderNum,
            CustomerDocument = "V-12345678",
            CustomerName = "Cliente Test POS",
            CasheaLevel = 3,
            TotalSaleUSD = completedSale.TotalUSD,
            DownPaymentAmountUSD = down,
            FinancedAmountUSD = financed,
            ExchangeRateApplied = bcv,
            TotalSaleVES = completedSale.TotalUSD * bcv,
            DownPaymentAmountVES = down * bcv,
            FinancedAmountVES = financed * bcv,
            DownPaymentMethod = PaymentMethodType.PuntoDeVenta,
            DownPaymentReference = "POS-12345",
            InstallmentsCount = 3,
            InstallmentAmountUSD = decimal.Round(financed / 3m, 2),
            InstallmentAmountVES = decimal.Round((financed * bcv) / 3m, 2),
            EstimatedCommissionPercentage = 5.0m,
            EstimatedCommissionUSD = decimal.Round(financed * 0.05m, 2),
            Status = CasheaOrderStatus.PorLiquidar,
            CreatedAt = DateTime.UtcNow
        };

        await casheaService.RegisterOrderAsync(casheaOrder);

        // Verificar que el evento global AppEvents.CasheaChanged fue emitido
        Assert.True(casheaEventFired, "El evento AppEvents.CasheaChanged debe dispararse al registrar la orden Cashea.");

        // Verificar que la orden existe en base de datos
        var orders = await casheaService.GetOrdersAsync(search: orderNum);
        Assert.Single(orders);
        var found = orders.First();
        Assert.Equal(completedSale.Id, found.SaleId);
        Assert.Equal(financed, found.FinancedAmountUSD);
        Assert.Equal(down, found.DownPaymentAmountUSD);
        _output.WriteLine($"Orden POS #{found.OrderNumber} verificada con éxito: Venta #{found.SaleId}, Financiado: ${found.FinancedAmountUSD:N2}, Inicial: ${found.DownPaymentAmountUSD:N2}");
    }

    [Fact]
    public async Task Test19_Sale_Fails_When_Quantity_Exceeds_Stock()
    {
        var config = new DatabaseConfigurationService();
        await config.InitializeDatabaseAsync();
        var factory = new DeterposDbContextFactory(config);
        var saleService = new SaleService(factory);

        await using var ctx = factory.CreateDbContext();
        var prod = await ctx.Products.FirstAsync(p => p.IsActive);
        if (prod.CurrentStock < 10)
        {
            prod.CurrentStock = 100;
            await ctx.SaveChangesAsync();
        }
        var shift = await ctx.CashShifts.FirstAsync(s => s.Status == CashShiftStatus.Abierta);

        decimal stockBefore = prod.CurrentStock;
        decimal excessQuantity = stockBefore + 15.00m; // Excede intencionalmente el stock disponible
        decimal bcv = 55.40m;

        var saleDraft = new Sale
        {
            CashRegisterId = 1,
            CashShiftId = shift.Id,
            CashierUsername = "TestCashier",
            FiscalPrinterSerial = "TESTFISCAL02",
            Notes = "Intento de venta con exceso de stock"
        };

        var details = new List<SaleDetail>
        {
            new SaleDetail
            {
                ProductId = prod.Id,
                ProductCode = prod.Code,
                ProductName = prod.Name,
                Quantity = excessQuantity,
                UnitPriceUSD = prod.PriceUSD,
                UnitCostUSD = prod.CostUSD,
                VatRate = prod.VatRate
            }
        };

        decimal totalUSD = excessQuantity * prod.PriceUSD;
        var payments = new List<SalePayment>
        {
            new SalePayment
            {
                Method = PaymentMethodType.EfectivoUSD,
                CurrencyCode = "USD",
                AmountOriginal = totalUSD,
                ExchangeRateApplied = 1.0m,
                AmountEquivalentUSD = totalUSD,
                ReferenceNumber = "TEST-EXCESS"
            }
        };

        // 1. Debe arrojar InvalidOperationException por stock insuficiente
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await saleService.ProcessSaleAsync(saleDraft, details, payments, bcv, false);
        });

        _output.WriteLine($"Excepción capturada correctamente: {ex.Message}");
        Assert.Contains("Stock insuficiente", ex.Message, StringComparison.OrdinalIgnoreCase);

        // 2. Verificar que en la base de datos el stock no fue modificado
        await using var verifyCtx = factory.CreateDbContext();
        var prodAfter = await verifyCtx.Products.FindAsync(prod.Id);
        Assert.NotNull(prodAfter);
        Assert.Equal(stockBefore, prodAfter.CurrentStock);
        _output.WriteLine($"Stock verificado inalterado en base de datos: {prodAfter.CurrentStock:G29} unidades.");
    }

    [Fact]
    public async Task Test20_FiscalBookService_Calculates_Daily_And_Weekly_Profit_Breakdown()
    {
        var config = new DatabaseConfigurationService();
        await config.InitializeDatabaseAsync();
        var factory = new DeterposDbContextFactory(config);
        var fiscalService = new FiscalBookService(factory);

        var start = new DateTime(2026, 11, 1);
        var end = new DateTime(2026, 11, 30);

        var analytics = await fiscalService.GetSalesAnalyticsAsync(start, end);

        Assert.NotNull(analytics);
        Assert.NotNull(analytics.DailyBreakdown);
        Assert.NotNull(analytics.WeeklyBreakdown);

        _output.WriteLine($"Desglose diario calculado: {analytics.DailyBreakdown.Count} días con actividad.");
        _output.WriteLine($"Desglose semanal calculado: {analytics.WeeklyBreakdown.Count} semanas con actividad.");

        if (analytics.DailyBreakdown.Count > 0)
        {
            var firstDay = analytics.DailyBreakdown.First();
            _output.WriteLine($"Día: {firstDay.PeriodLabel} -> Ventas: ${firstDay.TotalSalesUSD:N2}, Costo: ${firstDay.CostOfGoodsSoldUSD:N2}, U. Bruta: ${firstDay.GrossProfitUSD:N2}, Gastos: ${firstDay.OperatingExpensesUSD:N2}, Mermas: ${firstDay.InventoryShrinkageUSD:N2}, U. Neta: ${firstDay.NetProfitUSD:N2}");
            Assert.Equal(firstDay.GrossProfitUSD, firstDay.TotalSalesUSD - firstDay.CostOfGoodsSoldUSD);
            Assert.Equal(firstDay.NetProfitUSD, firstDay.GrossProfitUSD - firstDay.OperatingExpensesUSD - firstDay.InventoryShrinkageUSD);
        }

        // Probar exportación a archivo CSV temporal
        string tempCsv = Path.Combine(Path.GetTempPath(), $"test_profit_breakdown_{Guid.NewGuid():N}.csv");
        try
        {
            var generatedPath = await fiscalService.ExportProfitBreakdownToCsvAsync(analytics.DailyBreakdown, tempCsv, "Test Período Noviembre 2026");
            Assert.True(File.Exists(generatedPath), "El archivo CSV de desglose de ganancias debe crearse.");
            string content = await File.ReadAllTextAsync(generatedPath);
            Assert.Contains("ESTADO DE RESULTADOS DESGLOSADO", content);
            Assert.Contains("TOTALES GENERALES", content);
            _output.WriteLine($"CSV de desglose exportado exitosamente con tamaño: {new FileInfo(tempCsv).Length} bytes.");
        }
        finally
        {
            if (File.Exists(tempCsv))
            {
                File.Delete(tempCsv);
            }
        }
    }

    [Fact]
    public async Task Test_ProcessSale_FailsWhenCashRegisterIsClosed()
    {
        var saleService = new SaleService(_factory);
        await using var ctx = _factory.CreateDbContext();

        var prod = await ctx.Products.FirstAsync(p => p.IsActive);

        var saleDraft = new Sale
        {
            CashRegisterId = 1,
            CashShiftId = -9999,
            CashierUsername = "TestCashier",
            FiscalPrinterSerial = "TESTFISCAL01",
            Notes = "Intento de venta con caja cerrada"
        };

        var details = new List<SaleDetail>
        {
            new()
            {
                ProductId = prod.Id,
                ProductCode = prod.Code,
                ProductName = prod.Name,
                Quantity = 1.0m,
                UnitPriceUSD = prod.PriceUSD,
                UnitCostUSD = prod.CostUSD,
                VatRate = prod.VatRate
            }
        };

        var payments = new List<SalePayment>
        {
            new()
            {
                Method = PaymentMethodType.EfectivoUSD,
                CurrencyCode = "USD",
                AmountOriginal = prod.PriceUSD,
                ExchangeRateApplied = 1.0000m,
                AmountEquivalentUSD = prod.PriceUSD,
                ReferenceNumber = "CASH-TEST"
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await saleService.ProcessSaleAsync(saleDraft, details, payments, 55.40m, false);
        });

        Assert.Contains("CERRADA", ex.Message);
        _output.WriteLine($"Validación exitosa: El intento de facturación con caja cerrada fue rechazado: {ex.Message}");
    }

    [Fact]
    public async Task Test23_CustomerAndSupplierPayment_DoesNotCrossContaminateCurrenciesInCashShift()
    {
        var creditService = new CreditService(_factory);
        await using var ctx = _factory.CreateDbContext();

        var shift = new CashShift
        {
            CashRegisterId = 1,
            CashierUsername = "TestAudit",
            OpeningAmountUSD = 100m,
            OpeningAmountVES = 5000m,
            ExpectedAmountUSD = 100m,
            ExpectedAmountVES = 5000m,
            Status = CashShiftStatus.Abierta,
            OpenedAt = DateTime.UtcNow
        };
        ctx.CashShifts.Add(shift);

        var customer = new Customer
        {
            DocumentNumber = $"V-{Guid.NewGuid():N}"[..10],
            FullName = "Cliente Prueba Monedas",
            CurrentBalanceUSD = 200m
        };
        ctx.Customers.Add(customer);

        var supplier = new Supplier
        {
            DocumentNumber = $"J-{Guid.NewGuid():N}"[..10],
            CompanyName = "Proveedor Prueba Monedas",
            CurrentDebtUSD = 200m
        };
        ctx.Suppliers.Add(supplier);
        await ctx.SaveChangesAsync();

        // 1. Abono en USD de $25
        await creditService.ProcessCustomerPaymentAsync(
            customer.Id, 25m, 25m, "USD", 60m, PaymentMethodType.EfectivoUSD, "REF-USD", "Abono USD",
            sendToCashShift: true, currentCashShiftId: shift.Id, cashierUsername: "TestAudit");

        await using var verifyCtx1 = _factory.CreateDbContext();
        var shiftAfterUsd = await verifyCtx1.CashShifts.FindAsync(shift.Id);
        Assert.NotNull(shiftAfterUsd);
        Assert.Equal(125m, shiftAfterUsd.ExpectedAmountUSD);
        Assert.Equal(5000m, shiftAfterUsd.ExpectedAmountVES); // VES debe ser exactamente 5000 (sin contaminación cruzada)

        var movUsd = await verifyCtx1.CashMovements.OrderByDescending(m => m.Id).FirstAsync();
        Assert.Equal(25m, movUsd.AmountUSD);
        Assert.Equal(0m, movUsd.AmountVES);

        // 2. Abono en VES de 1200 Bs.
        await creditService.ProcessCustomerPaymentAsync(
            customer.Id, 20m, 1200m, "VES", 60m, PaymentMethodType.EfectivoVES, "REF-VES", "Abono VES",
            sendToCashShift: true, currentCashShiftId: shift.Id, cashierUsername: "TestAudit");

        await using var verifyCtx2 = _factory.CreateDbContext();
        var shiftAfterVes = await verifyCtx2.CashShifts.FindAsync(shift.Id);
        Assert.NotNull(shiftAfterVes);
        Assert.Equal(125m, shiftAfterVes.ExpectedAmountUSD); // USD debe permanecer intacto en 125
        Assert.Equal(6200m, shiftAfterVes.ExpectedAmountVES);

        var movVes = await verifyCtx2.CashMovements.OrderByDescending(m => m.Id).FirstAsync();
        Assert.Equal(0m, movVes.AmountUSD);
        Assert.Equal(1200m, movVes.AmountVES);

        // 3. Pago a Proveedor en USD de $15
        await creditService.ProcessSupplierPaymentAsync(
            supplier.Id, 15m, 15m, "USD", 60m, PaymentMethodType.EfectivoUSD, "REF-SUPP-USD", "Pago USD",
            payFromCashShift: true, currentCashShiftId: shift.Id, authorizedBy: "TestAudit");

        await using var verifyCtx3 = _factory.CreateDbContext();
        var shiftAfterSuppUsd = await verifyCtx3.CashShifts.FindAsync(shift.Id);
        Assert.NotNull(shiftAfterSuppUsd);
        Assert.Equal(110m, shiftAfterSuppUsd.ExpectedAmountUSD);
        Assert.Equal(6200m, shiftAfterSuppUsd.ExpectedAmountVES); // VES no debe ser alterado

        shiftAfterSuppUsd.Status = CashShiftStatus.Cerrada;
        shiftAfterSuppUsd.ClosedAt = DateTime.UtcNow;
        await verifyCtx3.SaveChangesAsync();

        _output.WriteLine("Test23 Superado: Se verificó la estricta separación de divisas USD y VES en turnos y movimientos de caja.");
    }

    [Fact]
    public async Task Test24_CreditNote_FailsCashRefundWhenShiftIsClosed()
    {
        var saleService = new SaleService(_factory);
        await using var ctx = _factory.CreateDbContext();

        var prod = await ctx.Products.FirstAsync(p => p.IsActive);
        if (prod.CurrentStock < 10)
        {
            prod.CurrentStock = 50;
            await ctx.SaveChangesAsync();
        }

        // Crear turno abierto para registrar la venta original
        var shift = new CashShift
        {
            CashRegisterId = 1,
            CashierUsername = "TestCashier",
            OpeningAmountUSD = 50m,
            ExpectedAmountUSD = 50m,
            Status = CashShiftStatus.Abierta,
            OpenedAt = DateTime.UtcNow
        };
        ctx.CashShifts.Add(shift);
        await ctx.SaveChangesAsync();

        var saleDraft = new Sale
        {
            CashRegisterId = 1,
            CashShiftId = shift.Id,
            CashierUsername = "TestCashier",
            FiscalPrinterSerial = "TESTFISCAL",
            Notes = "Venta previa a NC"
        };
        var details = new List<SaleDetail>
        {
            new()
            {
                ProductId = prod.Id,
                ProductCode = prod.Code,
                ProductName = prod.Name,
                Quantity = 1.0m,
                UnitPriceUSD = prod.PriceUSD,
                UnitCostUSD = prod.CostUSD,
                VatRate = VatRateType.Exento0
            }
        };
        var payments = new List<SalePayment>
        {
            new()
            {
                Method = PaymentMethodType.EfectivoUSD,
                CurrencyCode = "USD",
                AmountOriginal = prod.PriceUSD,
                ExchangeRateApplied = 1.0m,
                AmountEquivalentUSD = prod.PriceUSD,
                ReferenceNumber = "CASH-ORIGINAL"
            }
        };

        var sale = await saleService.ProcessSaleAsync(saleDraft, details, payments, 55.40m, false);

        // Ahora cerrar el turno
        shift.Status = CashShiftStatus.Cerrada;
        shift.ClosedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var returnItems = new List<CreditNoteItemDraft>
        {
            new()
            {
                ProductId = prod.Id,
                ProductCode = prod.Code,
                ProductName = prod.Name,
                OriginalQuantity = 1.0m,
                ReturnQuantity = 1.0m,
                UnitPriceUSD = prod.PriceUSD,
                VatRate = VatRateType.Exento0
            }
        };

        // Intentar reembolsar efectivo con turno cerrado o shiftId 0 debe lanzar InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await saleService.ProcessCreditNoteAsync(sale.Id, returnItems, "Prueba Devolución", refundToCashRegister: true, currentCashShiftId: 0, cashierUsername: "TestCashier");
        });

        Assert.Contains("turno", ex.Message.ToLower());
        _output.WriteLine($"Test24 Superado: Reembolso en efectivo denegado correctamente con caja cerrada: {ex.Message}");
    }

    [Fact]
    public async Task Test25_CancelSale_BlockedAfterZReport_AndRevertsOpenShiftCash()
    {
        var saleService = new SaleService(_factory);
        await using var ctx = _factory.CreateDbContext();

        var prod = await ctx.Products.FirstAsync(p => p.IsActive);
        if (prod.CurrentStock < 10)
        {
            prod.CurrentStock = 50;
            await ctx.SaveChangesAsync();
        }

        var shift = new CashShift
        {
            CashRegisterId = 1,
            CashierUsername = "TestCashier",
            OpeningAmountUSD = 100m,
            ExpectedAmountUSD = 100m,
            Status = CashShiftStatus.Abierta,
            OpenedAt = DateTime.UtcNow
        };
        ctx.CashShifts.Add(shift);
        await ctx.SaveChangesAsync();

        var saleDraft = new Sale
        {
            CashRegisterId = 1,
            CashShiftId = shift.Id,
            CashierUsername = "TestCashier",
            FiscalPrinterSerial = "TESTZ01",
            Notes = "Venta sujeta a reporte Z"
        };
        var details = new List<SaleDetail>
        {
            new()
            {
                ProductId = prod.Id,
                ProductCode = prod.Code,
                ProductName = prod.Name,
                Quantity = 1.0m,
                UnitPriceUSD = 10m,
                UnitCostUSD = prod.CostUSD,
                VatRate = VatRateType.Exento0
            }
        };
        var payments = new List<SalePayment>
        {
            new()
            {
                Method = PaymentMethodType.EfectivoUSD,
                CurrencyCode = "USD",
                AmountOriginal = 10m,
                ExchangeRateApplied = 1.0m,
                AmountEquivalentUSD = 10m,
                ReferenceNumber = "CASH-Z"
            }
        };

        var sale = await saleService.ProcessSaleAsync(saleDraft, details, payments, 55.40m, false);

        // 1. Simular reporte Z emitido (actualizando vía contexto rastreado)
        var dbSale = await ctx.Sales.FindAsync(sale.Id);
        Assert.NotNull(dbSale);
        dbSale.FiscalZReportNumber = 105;
        await ctx.SaveChangesAsync();

        var exZ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await saleService.CancelSaleAsync(sale.Id, "Intento anular factura declarada en Z");
        });
        Assert.Contains("Reporte Z", exZ.Message);

        // 2. Probar anulación válida de una factura sin Z que revierte caja abierta
        dbSale.FiscalZReportNumber = null;
        await ctx.SaveChangesAsync();

        bool cancelled = await saleService.CancelSaleAsync(sale.Id, "Anulación normal antes de Z");
        Assert.True(cancelled);

        await using var verifyCtx = _factory.CreateDbContext();
        var cancelledSale = await verifyCtx.Sales.FindAsync(sale.Id);
        Assert.Equal(SaleStatus.Anulada, cancelledSale!.Status);

        var shiftAfterCancel = await verifyCtx.CashShifts.FindAsync(shift.Id);
        Assert.Equal(100m, shiftAfterCancel!.ExpectedAmountUSD); // 100 + 10 - 10 = 100m

        // Cerrar el turno de prueba para no dejar turnos huérfanos
        shiftAfterCancel.Status = CashShiftStatus.Cerrada;
        shiftAfterCancel.ClosedAt = DateTime.UtcNow;
        await verifyCtx.SaveChangesAsync();

        _output.WriteLine("Test25 Superado: Se verificó el bloqueo de anulación post-Z y el reverso exacto de gaveta en turno abierto.");
    }

    [Fact]
    public async Task Test26_CancelPurchase_BlockedIfStockAlreadySold_AndLogsKardex()
    {
        var purchaseService = new PurchaseService(_factory);
        await using var ctx = _factory.CreateDbContext();

        var supplier = await ctx.Suppliers.FirstAsync();
        var prod = new Product
        {
            Code = $"TEST-PRD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Producto Prueba Anulación Compra",
            CostUSD = 5.0m,
            PriceUSD = 10.0m,
            CurrentStock = 0m,
            VatRate = VatRateType.Exento0,
            IsActive = true
        };
        ctx.Products.Add(prod);
        await ctx.SaveChangesAsync();

        var purchaseDraft = new Purchase
        {
            SupplierId = supplier.Id,
            InvoiceNumber = $"FAC-TEST-{Guid.NewGuid():N}"[..10],
            ControlNumber = "00-112233",
            Condition = PaymentCondition.Contado,
            RegisteredBy = "Supervisor"
        };
        var itemDrafts = new List<PurchaseItemDraft>
        {
            new()
            {
                ProductId = prod.Id,
                ProductCode = prod.Code,
                ProductName = prod.Name,
                Quantity = 10m,
                UnitCostUSD = 5.0m,
                VatRate = VatRateType.Exento0
            }
        };

        var purchase = await purchaseService.ProcessPurchaseAsync(purchaseDraft, itemDrafts, payFromCashShift: false, currentCashShiftId: null);

        // 1. Simular que se vendieron 6 unidades (quedan 4 unidades en stock, menor a 10)
        prod.CurrentStock = 4m;
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await purchaseService.CancelPurchaseAsync(purchase.Id, "Intento anular compra con mercancía ya vendida");
        });
        Assert.Contains("ya fue vendido", ex.Message);

        // 2. Restaurar stock a 10 y anular
        prod.CurrentStock = 10m;
        await ctx.SaveChangesAsync();

        bool cancelled = await purchaseService.CancelPurchaseAsync(purchase.Id, "Anulación autorizada");
        Assert.True(cancelled);

        await using var verifyCtx = _factory.CreateDbContext();
        var verifyProd = await verifyCtx.Products.FindAsync(prod.Id);
        Assert.Equal(0m, verifyProd!.CurrentStock);

        var kardexEntry = await verifyCtx.InventoryMovements
            .FirstOrDefaultAsync(m => m.ProductId == prod.Id && m.MovementType == InventoryMovementType.AnulacionCompra);
        Assert.NotNull(kardexEntry);

        _output.WriteLine("Test26 Superado: Protección de stock negativo en compras y registro Kárdex verificado.");
    }

    [Fact]
    public async Task Test27_BackupService_DirectSqlDumpGeneratesRealTableData()
    {
        var backupService = new DatabaseBackupService(new DatabaseConfigurationService());
        string tempBackupFileAuto = Path.Combine(Path.GetTempPath(), $"deterpos_test_backup_auto_{Guid.NewGuid():N}.sql");
        string tempBackupFileDirect = Path.Combine(Path.GetTempPath(), $"deterpos_test_backup_direct_{Guid.NewGuid():N}.sql");

        try
        {
            // 1. Probar CreateBackupAsync (detecta pg_dump/docker o fallback)
            var resultAuto = await backupService.CreateBackupAsync(tempBackupFileAuto);
            Assert.True(resultAuto.Success, $"Fallo la creación del respaldo automático: {resultAuto.Message}");
            Assert.True(File.Exists(tempBackupFileAuto));

            string contentAuto = await File.ReadAllTextAsync(tempBackupFileAuto);
            Assert.True(contentAuto.Length > 200, "El archivo de respaldo generado está vacío o incompleto.");
            Assert.True(contentAuto.Contains("PostgreSQL database dump") || contentAuto.Contains("DETERPOS BACKUP") || contentAuto.Contains("INSERT INTO"),
                "El respaldo no contiene encabezado reconocible de PostgreSQL ni datos SQL.");

            // 2. Probar explícitamente el volcado C# directo Npgsql
            var resultDirect = await backupService.CreateDirectSqlDumpAsync(tempBackupFileDirect);
            Assert.True(resultDirect.Success, $"Fallo el volcado directo SQL C#: {resultDirect.Message}");
            Assert.True(File.Exists(tempBackupFileDirect));

            string contentDirect = await File.ReadAllTextAsync(tempBackupFileDirect);
            Assert.Contains("BEGIN;", contentDirect);
            Assert.Contains("SET session_replication_role", contentDirect);
            Assert.Contains("INSERT INTO", contentDirect);
            Assert.Contains("COMMIT;", contentDirect);

            _output.WriteLine($"Test27 Superado: Respaldo automático ({resultAuto.FileSizeBytes:N0} bytes) y volcado directo C# Npgsql ({resultDirect.FileSizeBytes:N0} bytes) verificados exitosamente con datos reales.");
        }
        finally
        {
            if (File.Exists(tempBackupFileAuto))
            {
                File.Delete(tempBackupFileAuto);
            }
            if (File.Exists(tempBackupFileDirect))
            {
                File.Delete(tempBackupFileDirect);
            }
        }
    }
}
