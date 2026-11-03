using System.IO;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Repositories;
using ShopPOS.Data.Seeding;
using ShopPOS.Data.Services;
using ShopPOS.UI.Services;
using ShopPOS.UI.ViewModels.Sales;
using Xunit;

namespace ShopPOS.Tests;

public class StructuralRemovalsTests
{
    private static (SqliteConnection connection, DbContextOptions<AppDbContext> options) CreateInMemoryDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        return (connection, options);
    }

    [Fact]
    public async Task Migration_PreRemovalDatabase_MigratesAndCreatesBackup_PreservingProductsPurchasesMovements()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"shoppos_mig_struct_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            // 1. Migrate up to 20261101045810_Phase5C_ReceiptPrinting (schema before Shifts and Customers were dropped)
            using (var ctx = new AppDbContext(options))
            {
                var migrator = ctx.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20261101045810_Phase5C_ReceiptPrinting");

                // Populate old schema tables via raw SQL
                using var conn = (SqliteConnection)ctx.Database.GetDbConnection();
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Roles (Id, Name, CreatedAt, IsDeleted) 
                    VALUES (1, 'Owner', '2026-11-01 10:00:00', 0);

                    INSERT INTO Users (Id, Username, FullName, PasswordHash, RoleId, IsActive, MustChangePassword, CreatedAt, IsDeleted) 
                    VALUES (1, 'owner', 'Owner', 'hash', 1, 1, 0, '2026-11-01 10:00:00', 0);

                    INSERT INTO Categories (Id, Name, CreatedAt, IsDeleted, IsActive) 
                    VALUES (1, 'General', '2026-11-01 10:00:00', 0, 1);

                    INSERT INTO Units (Id, Name, ShortName, CreatedAt, IsDeleted, IsActive) 
                    VALUES (1, 'Piece', 'pc', '2026-11-01 10:00:00', 0, 1);

                    INSERT INTO Products (Id, Name, Sku, CostPrice, SalePrice, TaxPercent, ReorderLevel, TrackStock, CategoryId, UnitId, IsActive, CreatedAt, IsDeleted) 
                    VALUES (1, 'Old Product', 'SKU-OLD-01', 1000, 2500, 0, 5000, 1, 1, 1, 1, '2026-11-01 10:00:00', 0);

                    INSERT INTO Suppliers (Id, Name, Phone, OpeningBalance, IsActive, CreatedAt, IsDeleted) 
                    VALUES (1, 'Old Supplier', '555-1111', 0, 1, '2026-11-01 10:00:00', 0);

                    INSERT INTO Purchases (Id, PurchaseNumber, SupplierId, PurchaseDate, SubTotal, DiscountAmount, TaxAmount, FreightAmount, GrandTotal, PaidAmount, Status, CreatedAt, IsDeleted, CreatedByUserId) 
                    VALUES (1, 'PUR-OLD-01', 1, '2026-11-01 10:00:00', 10000, 0, 0, 0, 10000, 10000, 2, '2026-11-01 10:00:00', 0, 1);

                    INSERT INTO PurchaseItems (Id, PurchaseId, ProductId, ProductName, Sku, Quantity, UnitCost, DiscountPercent, TaxPercent, LineTotal, CreatedAt, IsDeleted) 
                    VALUES (1, 1, 1, 'Old Product', 'SKU-OLD-01', 10000, 1000, 0, 0, 10000, '2026-11-01 10:00:00', 0);

                    INSERT INTO StockMovements (Id, ProductId, MovementType, Quantity, UnitCost, MovementDate, ReferenceType, ReferenceId, Note, CreatedAt, IsDeleted, CreatedByUserId) 
                    VALUES (1, 1, 1, 10000, 1000, '2026-11-01 10:00:00', 'Purchase', 1, 'Initial purchase', '2026-11-01 10:00:00', 0, 1);

                    INSERT INTO Customers (Id, Name, Phone, OpeningBalance, CreditLimit, IsActive, CreatedAt, IsDeleted) 
                    VALUES (1, 'Old Customer', '555-2222', 5000, 100000, 1, '2026-11-01 10:00:00', 0);

                    INSERT INTO Shifts (Id, ShiftNumber, OpenedAt, OpeningCash, ExpectedCash, Status, CreatedAt, IsDeleted, CreatedByUserId) 
                    VALUES (1, 'SHF-OLD-01', '2026-11-01 08:00:00', 5000, 5000, 1, '2026-11-01 08:00:00', 0, 1);

                    INSERT INTO CashMovements (Id, ShiftId, Type, Amount, Reason, MovedAt, CreatedAt, IsDeleted, CreatedByUserId) 
                    VALUES (1, 1, 1, 2000, 'Morning float', '2026-11-01 08:30:00', '2026-11-01 08:30:00', 0, 1);

                    INSERT INTO Sales (Id, SaleNumber, SaleDate, CustomerId, ShiftId, Status, SubTotal, LineDiscountTotal, BillDiscountAmount, TaxAmount, RoundOff, GrandTotal, PaidAmount, DueAmount, ChangeAmount, TotalCost, CreatedAt, IsDeleted, CreatedByUserId) 
                    VALUES (1, 'INV-OLD-01', '2026-11-01 11:00:00', 1, 1, 2, 5000, 0, 0, 0, 0, 5000, 3000, 2000, 0, 2000, '2026-11-01 11:00:00', 0, 1);
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Trigger pre-migration backup helper
            string? backupPath = DatabaseBackupHelper.CreatePreMigrationBackup(tempDbPath, "20261102000450_Remove_Shifts_And_Customers");
            backupPath.Should().NotBeNullOrEmpty();
            File.Exists(backupPath!).Should().BeTrue();
            new FileInfo(backupPath!).Length.Should().BeGreaterThan(0);

            // 3. Apply destructive migration (Remove_Shifts_And_Customers)
            using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();
            }

            // 4. Assert dropped tables & columns via sqlite_master and table_info
            using (var verifyCtx = new AppDbContext(options))
            {
                using var conn = (SqliteConnection)verifyCtx.Database.GetDbConnection();
                await conn.OpenAsync();

                // Tables Customers, Shifts, CashMovements must not exist
                using var tableCmd = conn.CreateCommand();
                tableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Customers', 'Shifts', 'CashMovements');";
                using var rdr = await tableCmd.ExecuteReaderAsync();
                rdr.HasRows.Should().BeFalse("Customers, Shifts, and CashMovements tables must be dropped");
                rdr.Close();

                // Sales table must NOT have CustomerId or ShiftId columns
                using var colCmd = conn.CreateCommand();
                colCmd.CommandText = "PRAGMA table_info(Sales);";
                var salesColumns = new List<string>();
                using (var colRdr = await colCmd.ExecuteReaderAsync())
                {
                    while (await colRdr.ReadAsync())
                    {
                        salesColumns.Add(colRdr.GetString(1));
                    }
                }
                salesColumns.Should().NotContain("CustomerId");
                salesColumns.Should().NotContain("ShiftId");
                salesColumns.Should().Contain("DueAmount"); // Preserved for old sales history

                // All Products, Purchases, PurchaseItems, Suppliers, StockMovements preserved
                var prod = await verifyCtx.Products.FindAsync(1);
                prod.Should().NotBeNull();
                prod!.Name.Should().Be("Old Product");

                var purch = await verifyCtx.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == 1);
                purch.Should().NotBeNull();
                purch!.Items.Should().HaveCount(1);

                var supp = await verifyCtx.Suppliers.FindAsync(1);
                supp.Should().NotBeNull();

                var mov = await verifyCtx.StockMovements.FindAsync(1);
                mov.Should().NotBeNull();

                var oldSale = await verifyCtx.Sales.FindAsync(1);
                oldSale.Should().NotBeNull();
                oldSale!.DueAmount.Should().Be(20.00m); // $20.00 historical due preserved
            }

            // Clean up backup file
            if (File.Exists(backupPath))
            {
                try { File.Delete(backupPath); } catch { }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task SalesList_FilteredBreakdown_CashReflectsPaymentsMinusChange_AndMatchesTotal()
    {
        var (connection, options) = CreateInMemoryDb();
        using var conn = connection;
        using var ctx = new AppDbContext(options);
        await ctx.Database.MigrateAsync();

        var seeder = new DataSeeder(ctx);
        await seeder.SeedAsync();

        var saleRepo = new SaleRepository(ctx);

        // Sale 1: Cash sale with change ($100 tendered, $80 grand total -> $20 change; effective cash = $80)
        var s1 = new Sale
        {
            SaleNumber = "INV-000001",
            SaleDate = DateTime.Today,
            Status = SaleStatus.Completed,
            SubTotal = 80m,
            GrandTotal = 80m,
            PaidAmount = 80m,
            ChangeAmount = 20m,
            DueAmount = 0m,
            CreatedByUserId = 1
        };
        s1.Payments.Add(new SalePayment { Method = PaymentMethod.Cash, Amount = 100m, TenderedAmount = 100m, PaidAt = DateTime.Now, CreatedByUserId = 1 });
        ctx.Sales.Add(s1);

        // Sale 2: Card sale ($50 grand total, paid $50 card)
        var s2 = new Sale
        {
            SaleNumber = "INV-000002",
            SaleDate = DateTime.Today,
            Status = SaleStatus.Completed,
            SubTotal = 50m,
            GrandTotal = 50m,
            PaidAmount = 50m,
            ChangeAmount = 0m,
            DueAmount = 0m,
            CreatedByUserId = 1
        };
        s2.Payments.Add(new SalePayment { Method = PaymentMethod.Card, Amount = 50m, TenderedAmount = 50m, PaidAt = DateTime.Now, CreatedByUserId = 1 });
        ctx.Sales.Add(s2);

        // Sale 3: Bank sale ($30) + MobileWallet ($20) = $50 grand total
        var s3 = new Sale
        {
            SaleNumber = "INV-000003",
            SaleDate = DateTime.Today,
            Status = SaleStatus.Completed,
            SubTotal = 50m,
            GrandTotal = 50m,
            PaidAmount = 50m,
            ChangeAmount = 0m,
            DueAmount = 0m,
            CreatedByUserId = 1
        };
        s3.Payments.Add(new SalePayment { Method = PaymentMethod.Bank, Amount = 30m, TenderedAmount = 30m, PaidAt = DateTime.Now, CreatedByUserId = 1 });
        s3.Payments.Add(new SalePayment { Method = PaymentMethod.MobileWallet, Amount = 20m, TenderedAmount = 20m, PaidAt = DateTime.Now, CreatedByUserId = 1 });
        ctx.Sales.Add(s3);

        // Sale 4: Cancelled sale ($40 Cash) - should be excluded from breakdown!
        var s4 = new Sale
        {
            SaleNumber = "INV-000004",
            SaleDate = DateTime.Today,
            Status = SaleStatus.Cancelled,
            SubTotal = 40m,
            GrandTotal = 40m,
            PaidAmount = 40m,
            CreatedByUserId = 1
        };
        s4.Payments.Add(new SalePayment { Method = PaymentMethod.Cash, Amount = 40m, TenderedAmount = 40m, PaidAt = DateTime.Now, CreatedByUserId = 1 });
        ctx.Sales.Add(s4);

        await ctx.SaveChangesAsync();

        var query = new SaleSearchDto
        {
            DateFrom = DateTime.Today,
            DateTo = DateTime.Today.AddDays(1).AddTicks(-1)
        };

        var breakdown = await saleRepo.GetFilteredPaymentBreakdownAsync(query);

        breakdown.SalesCount.Should().Be(3);
        breakdown.TotalCash.Should().Be(80.00m); // Cash paid ($80) minus change ($20) handled: applied was 80
        breakdown.TotalCard.Should().Be(50.00m);
        breakdown.TotalBank.Should().Be(30.00m);
        breakdown.TotalMobileWallet.Should().Be(20.00m);
        breakdown.TotalOther.Should().Be(0m);
        (breakdown.TotalCash + breakdown.TotalCard + breakdown.TotalBank + breakdown.TotalMobileWallet + breakdown.TotalOther)
            .Should().Be(180.00m); // 80 + 50 + 50 = 180 total completed sales
    }

    [Fact]
    public async Task PosSale_CompletesWithoutShiftOrCustomer_GeneratesStockMovementAndInvoice()
    {
        var (connection, options) = CreateInMemoryDb();
        using var conn = connection;
        using var ctx = new AppDbContext(options);
        await ctx.Database.MigrateAsync();

        var seeder = new DataSeeder(ctx);
        await seeder.SeedAsync();

        var cat = await ctx.Categories.FirstAsync();
        var unit = await ctx.Units.FirstAsync();
        var prod = new Product
        {
            Name = "Walk-in Item",
            Sku = "WALK-01",
            Barcode = "555000111",
            CostPrice = 5.00m,
            SalePrice = 12.00m,
            TaxPercent = 0m,
            TrackStock = true,
            CategoryId = cat.Id,
            UnitId = unit.Id,
            IsActive = true
        };
        ctx.Products.Add(prod);
        await ctx.SaveChangesAsync();

        // 10 units initial stock
        ctx.StockMovements.Add(new StockMovement
        {
            ProductId = prod.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 10m,
            UnitCost = 5.00m,
            MovementDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var slQuery = new StockLedgerQuery(ctx);
        var seqService = new NumberSequenceService(ctx);
        var runner = new TransactionRunner(ctx);
        var audit = new Mock<IAuditLogService>();
        var userProvider = new CurrentUserProvider();
        var calc = new SaleCalculator();
        var settingService = new ShopSettingService(new ShopSettingRepository(ctx));

        var saleService = new SaleService(
            new SaleRepository(ctx),
            new ProductRepository(ctx),
            new Repository<StockMovement>(ctx),
            slQuery,
            settingService,
            seqService,
            runner,
            audit.Object,
            userProvider,
            calc);

        // Act: Complete sale with zero shift and zero customer
        var completeDto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = prod.Id, Quantity = 2m, UnitPrice = 12.00m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 24.00m, TenderedAmount = 24.00m }
            }
        };

        var result = await saleService.CompleteSaleAsync(completeDto);

        // Assert
        result.Success.Should().BeTrue();
        var sale = result.Data!;
        sale.SaleNumber.Should().StartWith("INV-");
        sale.Status.Should().Be(SaleStatus.Completed);
        sale.GrandTotal.Should().Be(24.00m);
        sale.PaidAmount.Should().Be(24.00m);
        sale.DueAmount.Should().Be(0m);

        // Verify stock movement
        decimal stockAfter = await slQuery.GetCurrentStockAsync(prod.Id);
        stockAfter.Should().Be(8m); // 10 - 2 = 8

        var mov = await ctx.StockMovements.FirstOrDefaultAsync(m => m.ReferenceType == "Sale" && m.ReferenceId == sale.Id);
        mov.Should().NotBeNull();
        mov!.Quantity.Should().Be(-2m);
    }

    [Fact]
    public async Task FullPaymentRule_RejectsUnderpayment_AcceptsExactOrOverpaymentWithChange()
    {
        var (connection, options) = CreateInMemoryDb();
        using var conn = connection;
        using var ctx = new AppDbContext(options);
        await ctx.Database.MigrateAsync();

        var seeder = new DataSeeder(ctx);
        await seeder.SeedAsync();

        var cat = await ctx.Categories.FirstAsync();
        var unit = await ctx.Units.FirstAsync();
        var prod = new Product
        {
            Name = "Payment Test Item",
            Sku = "PAY-01",
            CostPrice = 10m,
            SalePrice = 50m,
            TaxPercent = 0m,
            TrackStock = false,
            CategoryId = cat.Id,
            UnitId = unit.Id,
            IsActive = true
        };
        ctx.Products.Add(prod);
        await ctx.SaveChangesAsync();

        var saleService = new SaleService(
            new SaleRepository(ctx),
            new ProductRepository(ctx),
            new Repository<StockMovement>(ctx),
            new StockLedgerQuery(ctx),
            new ShopSettingService(new ShopSettingRepository(ctx)),
            new NumberSequenceService(ctx),
            new TransactionRunner(ctx),
            new Mock<IAuditLogService>().Object,
            new CurrentUserProvider(),
            new SaleCalculator());

        // 1. Underpayment: GrandTotal = $50, paid $40 -> rejected
        var underpaidDto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = prod.Id, Quantity = 1m, UnitPrice = 50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 40m }
            }
        };
        var underpaidRes = await saleService.CompleteSaleAsync(underpaidDto);
        underpaidRes.Success.Should().BeFalse();
        underpaidRes.ErrorMessage.Should().Contain("All sales must be paid in full at completion");

        // 2. Exact payment: GrandTotal = $50, paid $50 -> accepted, change = $0
        var exactDto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = prod.Id, Quantity = 1m, UnitPrice = 50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Card, Amount = 50m }
            }
        };
        var exactRes = await saleService.CompleteSaleAsync(exactDto);
        exactRes.Success.Should().BeTrue();
        exactRes.Data!.DueAmount.Should().Be(0m);
        exactRes.Data!.ChangeAmount.Should().Be(0m);

        // 3. Cash overpayment: GrandTotal = $50, tendered $60 -> accepted, change = $10
        var overpaidDto = new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto>
            {
                new() { ProductId = prod.Id, Quantity = 1m, UnitPrice = 50m, TaxPercent = 0m }
            },
            Payments = new List<SalePaymentDto>
            {
                new() { Method = PaymentMethod.Cash, Amount = 60m, TenderedAmount = 60m }
            }
        };
        var overpaidRes = await saleService.CompleteSaleAsync(overpaidDto);
        overpaidRes.Success.Should().BeTrue();
        overpaidRes.Data!.DueAmount.Should().Be(0m);
        overpaidRes.Data!.ChangeAmount.Should().Be(10m);
        overpaidRes.Data!.PaidAmount.Should().Be(50m);
    }

    [Fact]
    public async Task SaleCancellation_AllowedTodayOnly_DisallowedForPastDate_AndDisallowedIfReturnsExist()
    {
        var (connection, options) = CreateInMemoryDb();
        using var conn = connection;
        using var ctx = new AppDbContext(options);
        await ctx.Database.MigrateAsync();

        var seeder = new DataSeeder(ctx);
        await seeder.SeedAsync();

        var cat = await ctx.Categories.FirstAsync();
        var unit = await ctx.Units.FirstAsync();
        var prod = new Product
        {
            Name = "Cancellation Test Item",
            Sku = "CANC-01",
            CostPrice = 5m,
            SalePrice = 10m,
            TrackStock = true,
            CategoryId = cat.Id,
            UnitId = unit.Id,
            IsActive = true
        };
        ctx.Products.Add(prod);
        await ctx.SaveChangesAsync();

        ctx.StockMovements.Add(new StockMovement
        {
            ProductId = prod.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 20m,
            UnitCost = 5m,
            MovementDate = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        var slQuery = new StockLedgerQuery(ctx);
        var seqService = new NumberSequenceService(ctx);
        var runner = new TransactionRunner(ctx);
        var audit = new Mock<IAuditLogService>();
        var userProvider = new CurrentUserProvider();
        var calc = new SaleCalculator();
        var settingService = new ShopSettingService(new ShopSettingRepository(ctx));

        var saleRepo = new SaleRepository(ctx);
        var returnRepo = new SaleReturnRepository(ctx);
        var prodRepo = new ProductRepository(ctx);
        var movRepo = new Repository<StockMovement>(ctx);

        var saleService = new SaleService(saleRepo, prodRepo, movRepo, slQuery, settingService, seqService, runner, audit.Object, userProvider, calc);
        var returnService = new SaleReturnService(returnRepo, saleRepo, prodRepo, movRepo, slQuery, settingService, seqService, runner, audit.Object, userProvider, calc);

        // Create Sale 1 (today)
        var s1Res = await saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto> { new() { ProductId = prod.Id, Quantity = 2m, UnitPrice = 10m } },
            Payments = new List<SalePaymentDto> { new() { Method = PaymentMethod.Cash, Amount = 20m } }
        });
        s1Res.Success.Should().BeTrue();
        int s1Id = s1Res.Data!.Id;

        // 1. Sale dated today CAN be cancelled -> stock restored
        var cancelTodayRes = await saleService.CancelSaleAsync(s1Id, "Customer decided not to purchase");
        cancelTodayRes.Success.Should().BeTrue();
        decimal stockAfterCancel = await slQuery.GetCurrentStockAsync(prod.Id);
        stockAfterCancel.Should().Be(20m); // Fully restored to 20

        // Create Sale 2 (past date)
        var s2Res = await saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto> { new() { ProductId = prod.Id, Quantity = 2m, UnitPrice = 10m } },
            Payments = new List<SalePaymentDto> { new() { Method = PaymentMethod.Cash, Amount = 20m } }
        });
        int s2Id = s2Res.Data!.Id;

        // Backdate Sale 2 to yesterday
        var sale2Entity = await ctx.Sales.FindAsync(s2Id);
        sale2Entity!.SaleDate = DateTime.Today.AddDays(-1);
        await ctx.SaveChangesAsync();

        // 2. Sale dated yesterday CANNOT be cancelled
        var cancelPastRes = await saleService.CancelSaleAsync(s2Id, "Try cancel yesterday");
        cancelPastRes.Success.Should().BeFalse();
        cancelPastRes.ErrorMessage.Should().Contain("Sales dated before today cannot be cancelled");

        // Create Sale 3 (today, but has a return posted)
        var s3Res = await saleService.CompleteSaleAsync(new CompleteSaleDto
        {
            Items = new List<SaleLineItemDto> { new() { ProductId = prod.Id, Quantity = 4m, UnitPrice = 10m } },
            Payments = new List<SalePaymentDto> { new() { Method = PaymentMethod.Cash, Amount = 40m } }
        });
        int s3Id = s3Res.Data!.Id;

        var retRes = await returnService.CreateSaleReturnAsync(new CreateSaleReturnDto
        {
            SaleId = s3Id,
            RefundMethod = PaymentMethod.Cash,
            Reason = "Return item",
            Items = new List<CreateSaleReturnItemDto>
            {
                new() { SaleItemId = s3Res.Data!.Items[0].Id, ProductId = prod.Id, Quantity = 1m, ReturnToStock = true }
            }
        });
        retRes.Success.Should().BeTrue();

        // 3. Sale with return CANNOT be cancelled
        var cancelWithReturnRes = await saleService.CancelSaleAsync(s3Id, "Try cancel with return");
        cancelWithReturnRes.Success.Should().BeFalse();
        cancelWithReturnRes.ErrorMessage.Should().Contain("cannot be cancelled because it has posted returns");
    }

    [Fact]
    public async Task PosPriceLevel_DropdownSelection_RecalculatesCartLinesToTierPrice_OrFallsBackToSalePrice()
    {
        var (connection, options) = CreateInMemoryDb();
        using var conn = connection;
        using var ctx = new AppDbContext(options);
        await ctx.Database.MigrateAsync();

        var seeder = new DataSeeder(ctx);
        await seeder.SeedAsync();

        var cat = await ctx.Categories.FirstAsync();
        var unit = await ctx.Units.FirstAsync();

        // Level 1 = Retail (default), Level 2 = Wholesale
        var wholesaleLevel = await ctx.PriceLevels.FirstOrDefaultAsync(p => p.Name == "Wholesale");
        if (wholesaleLevel == null)
        {
            wholesaleLevel = new PriceLevel { Name = "Wholesale" };
            ctx.PriceLevels.Add(wholesaleLevel);
            await ctx.SaveChangesAsync();
        }

        // Product 1: Has a wholesale tier ($18 vs retail $25)
        var prod1 = new Product
        {
            Name = "Tiered Product",
            Sku = "TIER-01",
            Barcode = "7770001",
            CostPrice = 10m,
            SalePrice = 25m,
            TaxPercent = 0m,
            TrackStock = true,
            CategoryId = cat.Id,
            UnitId = unit.Id,
            IsActive = true
        };
        ctx.Products.Add(prod1);

        // Product 2: Does NOT have a wholesale tier (retail $15)
        var prod2 = new Product
        {
            Name = "Non-Tiered Product",
            Sku = "NOTIER-01",
            Barcode = "7770002",
            CostPrice = 8m,
            SalePrice = 15m,
            TaxPercent = 0m,
            TrackStock = true,
            CategoryId = cat.Id,
            UnitId = unit.Id,
            IsActive = true
        };
        ctx.Products.Add(prod2);
        await ctx.SaveChangesAsync();

        // Add Wholesale tier for prod1
        ctx.ProductPrices.Add(new ProductPrice
        {
            ProductId = prod1.Id,
            PriceLevelId = wholesaleLevel.Id,
            Price = 18m
        });
        await ctx.SaveChangesAsync();

        // Setup mock services for POS ViewModel
        var mockProductService = new Mock<IProductService>();
        mockProductService.Setup(p => p.GetProductByIdAsync(prod1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDto>.Ok(new ProductDto
            {
                Id = prod1.Id,
                Name = prod1.Name,
                SalePrice = 25m,
                ProductPrices = new List<ProductPriceDto>
                {
                    new() { PriceLevelId = wholesaleLevel.Id, Price = 18m }
                }
            }));

        mockProductService.Setup(p => p.GetProductByIdAsync(prod2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProductDto>.Ok(new ProductDto
            {
                Id = prod2.Id,
                Name = prod2.Name,
                SalePrice = 15m,
                ProductPrices = new List<ProductPriceDto>()
            }));

        var mockPriceLevelService = new Mock<IPriceLevelService>();
        mockPriceLevelService.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<PriceLevelDto>>.Ok(new List<PriceLevelDto>
            {
                new() { Id = 1, Name = "Retail" },
                new() { Id = wholesaleLevel.Id, Name = "Wholesale" }
            }));

        var mockLookup = new Mock<IProductLookupService>();
        var mockSetting = new Mock<IShopSettingService>();
        mockSetting.Setup(s => s.GetSettingsAsync()).ReturnsAsync(new ShopSetting { CurrencySymbol = "Rs" });

        var mockStockQuery = new Mock<IStockLedgerQuery>();
        mockStockQuery.Setup(q => q.GetCurrentStockAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(100m);

        var vm = new PosSaleViewModel(
            new Mock<ISaleService>().Object,
            new SaleCalculator(),
            mockLookup.Object,
            mockProductService.Object,
            mockPriceLevelService.Object,
            mockStockQuery.Object,
            mockSetting.Object,
            new Mock<IDialogService>().Object,
            new Mock<INavigationService>().Object,
            new Mock<IReceiptPrinterService>().Object);

        await vm.InitializeAsync();

        // Add lines under Retail pricing
        await vm.AddProductToCartAsync(prod1.Id, 2m);
        await vm.AddProductToCartAsync(prod2.Id, 1m);
        vm.GrandTotal.Should().Be(65m);

        // Act: Change price level to Wholesale
        vm.SelectedPriceLevel = vm.PriceLevels.First(p => p.Id == wholesaleLevel.Id);
        await vm.UpdateCartPricesForSelectedPriceLevelAsync(wholesaleLevel.Id);

        // Assert: Prod1 recalculated to $18 ($36 total), Prod2 fell back to $15 ($15 total) -> Grand total = $51
        var line1 = vm.CartItems.First(i => i.ProductId == prod1.Id);
        line1.UnitPrice.Should().Be(18m);
        line1.LineTotal.Should().Be(36m);

        var line2 = vm.CartItems.First(i => i.ProductId == prod2.Id);
        line2.UnitPrice.Should().Be(15m); // Fallback to retail
        line2.LineTotal.Should().Be(15m);

        vm.GrandTotal.Should().Be(51m);
    }
}
