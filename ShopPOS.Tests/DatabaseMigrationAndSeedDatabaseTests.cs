using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Services;
using ShopPOS.Data;
using ShopPOS.Data.Interceptors;
using ShopPOS.Data.Seeding;

namespace ShopPOS.Tests;

public class DatabaseMigrationAndSeedDatabaseTests
{
    private static (SqliteConnection connection, DbContextOptions<AppDbContext> options) CreateInMemoryDatabase()
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
    public async Task MigrationsAndSeeder_ShouldApplyAndCreateInitialSystemData()
    {
        // Arrange
        var (connection, options) = CreateInMemoryDatabase();
        using var conn = connection;
        using var context = new AppDbContext(options);

        // Act 1: Apply migrations
        await context.Database.MigrateAsync();

        // Act 2: Seed initial reference data
        var seeder = new DataSeeder(context);
        await seeder.SeedAsync();

        // Assert Roles
        var roles = await context.Roles.ToListAsync();
        roles.Should().HaveCount(3);
        roles.Select(r => r.Name).Should().Contain([RoleDefaults.Admin, RoleDefaults.Manager, RoleDefaults.Cashier]);

        // Assert Seeded Owner User
        var ownerUser = await context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == 1);
        ownerUser.Should().NotBeNull();
        ownerUser!.FullName.Should().Be("Owner");
        ownerUser.Username.Should().Be("owner");
        ownerUser.Role.Should().NotBeNull();
        ownerUser.IsActive.Should().BeTrue();

        // Assert Units: pcs, kg, g, litre, box, dozen
        var units = await context.Units.ToListAsync();
        units.Should().HaveCount(6);
        units.Select(u => u.ShortName).Should().Contain(["pcs", "kg", "g", "litre", "box", "dozen"]);

        // Assert Default Category: General
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == "General");
        category.Should().NotBeNull();

        // Assert Default ShopSetting row
        var settings = await context.ShopSettings.FirstOrDefaultAsync();
        settings.Should().NotBeNull();
        settings!.ShopName.Should().Be("My Mart & Retail Store");
        settings.CurrencySymbol.Should().Be("$");
        settings.ReceiptWidthMm.Should().Be(80);
    }

    [Fact]
    public async Task TemporaryDatabaseFile_ShouldApplyMigrationsAndSeedData()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"shoppos_test_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            using (var context = new AppDbContext(options))
            {
                await context.Database.MigrateAsync();

                var seeder = new DataSeeder(context);
                await seeder.SeedAsync();

                // Verify seeded contents in temporary database
                var ownerUser = await context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == 1);
                ownerUser.Should().NotBeNull();
                ownerUser!.FullName.Should().Be("Owner");
                ownerUser.Username.Should().Be("owner");
                ownerUser.Role.Should().NotBeNull();
                ownerUser.IsActive.Should().BeTrue();

                var roles = await context.Roles.ToListAsync();
                roles.Should().HaveCount(3);

                var units = await context.Units.ToListAsync();
                units.Should().HaveCount(6);

                var setting = await context.ShopSettings.FirstOrDefaultAsync();
                setting.Should().NotBeNull();
            }
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { /* ignore on file lock */ }
            }
        }
    }

    [Fact]
    public async Task MoneyAndQuantityConverters_ShouldStoreMinorUnitsAndThousandthsInSqlite()
    {
        // Arrange
        var (connection, options) = CreateInMemoryDatabase();
        using var conn = connection;

        int productId;
        using (var context = new AppDbContext(options))
        {
            await context.Database.MigrateAsync();

            var category = new Category { Name = "Dairy" };
            var unit = new Unit { Name = "Kilogram", ShortName = "kg" };
            await context.Categories.AddAsync(category);
            await context.Units.AddAsync(unit);
            await context.SaveChangesAsync();

            var product = new Product
            {
                Name = "Organic Cheddar Cheese",
                Sku = "CHK-001",
                Barcode = "1234567890123",
                CategoryId = category.Id,
                UnitId = unit.Id,
                CostPrice = 14.50m,    // Should be stored in SQLite as 1450 (minor units x100)
                SalePrice = 19.99m,    // Should be stored in SQLite as 1999 (minor units x100)
                ReorderLevel = 2.500m  // Should be stored in SQLite as 2500 (thousandths x1000)
            };

            await context.Products.AddAsync(product);
            await context.SaveChangesAsync();
            productId = product.Id;
        }

        // Act 1: Query raw SQLite storage using direct command
        long rawCostPrice;
        long rawSalePrice;
        long rawReorderLevel;

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT CostPrice, SalePrice, ReorderLevel FROM Products WHERE Id = {productId}";
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                (await reader.ReadAsync()).Should().BeTrue();
                rawCostPrice = reader.GetInt64(0);
                rawSalePrice = reader.GetInt64(1);
                rawReorderLevel = reader.GetInt64(2);
            }
        }

        // Assert SQLite raw integer storage
        rawCostPrice.Should().Be(1450);   // 14.50 * 100
        rawSalePrice.Should().Be(1999);   // 19.99 * 100
        rawReorderLevel.Should().Be(2500); // 2.500 * 1000

        // Act 2: Query via EF Core entity and verify conversion back to decimal
        using (var context2 = new AppDbContext(options))
        {
            var loaded = await context2.Products.FindAsync(productId);
            loaded.Should().NotBeNull();
            loaded!.CostPrice.Should().Be(14.50m);
            loaded.SalePrice.Should().Be(19.99m);
            loaded.ReorderLevel.Should().Be(2.500m);
        }
    }

    [Fact]
    public async Task SoftDeleteFilter_ShouldExcludeDeletedEntitiesAutomatically()
    {
        // Arrange
        var (connection, options) = CreateInMemoryDatabase();
        using var conn = connection;

        int customerId;
        using (var context = new AppDbContext(options))
        {
            await context.Database.MigrateAsync();

            var customer = new Customer
            {
                Name = "Alice Retail",
                Phone = "555-9876",
                IsDeleted = false
            };

            await context.Customers.AddAsync(customer);
            await context.SaveChangesAsync();
            customerId = customer.Id;

            // Act: Soft delete customer
            customer.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        // Assert: Standard query filter excludes deleted entities
        using (var queryContext = new AppDbContext(options))
        {
            var activeCustomers = await queryContext.Customers.ToListAsync();
            activeCustomers.Should().NotContain(c => c.Id == customerId);

            // Assert: Query with IgnoreQueryFilters includes soft deleted entities
            var allCustomers = await queryContext.Customers.IgnoreQueryFilters().ToListAsync();
            allCustomers.Should().Contain(c => c.Id == customerId && c.IsDeleted);
        }
    }

    [Fact]
    public async Task Phase5_Sales_Migration_AppliesPreservingAllExistingDataWithoutLoss()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"shoppos_mig_test_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(new SqlitePragmaInterceptor())
                .Options;

            // 1. Apply migrations up to RemoveStockCount (migration just before Phase5_Sales)
            using (var ctx = new AppDbContext(options))
            {
                var migrator = ctx.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20261101005706_RemoveStockCount");

                var role = new Role { Name = "Owner" };
                ctx.Roles.Add(role);

                var user = new User
                {
                    Username = "owner",
                    FullName = "Owner",
                    Role = role,
                    IsActive = true
                };
                ctx.Users.Add(user);

                var supplier = new Supplier
                {
                    Name = "Migration Test Supplier",
                    Phone = "123-555-7890",
                    IsActive = true
                };
                ctx.Suppliers.Add(supplier);

                var customer = new Customer
                {
                    Name = "Migration Customer",
                    Phone = "555-4321",
                    IsActive = true
                };
                ctx.Customers.Add(customer);

                var cat = new Category { Name = "Migration Test Category", IsActive = true };
                var unit = new Unit { Name = "Piece", ShortName = "pc", IsActive = true };
                ctx.Categories.Add(cat);
                ctx.Units.Add(unit);

                var product = new Product
                {
                    Name = "Migration Test Product",
                    Sku = "MIG-P5-001",
                    CostPrice = 25m,
                    SalePrice = 45m,
                    TrackStock = true,
                    Category = cat,
                    Unit = unit,
                    IsActive = true
                };
                ctx.Products.Add(product);

                var purchase = new Purchase
                {
                    PurchaseNumber = "PUR-MIG-P5",
                    Supplier = supplier,
                    PurchaseDate = DateTime.Now,
                    SubTotal = 250m,
                    GrandTotal = 250m,
                    Status = PurchaseStatus.Posted,
                    PaidAmount = 250m
                };
                purchase.Items.Add(new PurchaseItem
                {
                    Product = product,
                    Quantity = 10m,
                    UnitCost = 25m,
                    LineTotal = 250m
                });
                ctx.Purchases.Add(purchase);

                var mov = new StockMovement
                {
                    Product = product,
                    MovementType = StockMovementType.Purchase,
                    Quantity = 10m,
                    UnitCost = 25m,
                    MovementDate = DateTime.Now,
                    Note = "Pre-migration stock"
                };
                ctx.StockMovements.Add(mov);

                await ctx.SaveChangesAsync();
            }

            // 2. Apply all pending migrations (Phase5_Sales)
            using (var ctx = new AppDbContext(options))
            {
                await ctx.Database.MigrateAsync();
            }

            // 3. Verify all existing data is preserved without loss
            using (var verifyCtx = new AppDbContext(options))
            {
                var prod = await verifyCtx.Products.FirstOrDefaultAsync(p => p.Sku == "MIG-P5-001");
                prod.Should().NotBeNull();
                prod!.CostPrice.Should().Be(25m);
                prod.SalePrice.Should().Be(45m);

                var supp = await verifyCtx.Suppliers.FirstOrDefaultAsync(s => s.Name == "Migration Test Supplier");
                supp.Should().NotBeNull();

                var cust = await verifyCtx.Customers.FirstOrDefaultAsync(c => c.Name == "Migration Customer");
                cust.Should().NotBeNull();

                var purch = await verifyCtx.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.PurchaseNumber == "PUR-MIG-P5");
                purch.Should().NotBeNull();
                purch!.Items.Should().HaveCount(1);
                purch.GrandTotal.Should().Be(250m);

                var movements = await verifyCtx.StockMovements.Where(m => m.ProductId == prod.Id).ToListAsync();
                movements.Should().HaveCount(1);
                movements[0].Quantity.Should().Be(10m);

                // 4. Verify Phase 5 new tables exist and can accept records
                var shift = new Shift
                {
                    ShiftNumber = "SHF-MIG-01",
                    OpenedAt = DateTime.Now,
                    OpeningCash = 50m,
                    Status = ShiftStatus.Open,
                    CreatedByUserId = 1
                };
                verifyCtx.Shifts.Add(shift);
                await verifyCtx.SaveChangesAsync();

                shift.Id.Should().BeGreaterThan(0);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
