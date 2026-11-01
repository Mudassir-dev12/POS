using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Seeding;

/// <summary>
/// Implements idempotent seed operations for ShopPOS.
/// </summary>
public class DataSeeder : IDataSeeder
{
    private readonly AppDbContext _context;

    public DataSeeder(AppDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // 1. Seed Roles (keep for database schema/FK compatibility)
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleDefaults.Admin, cancellationToken);
        if (adminRole == null)
        {
            adminRole = new Role { Name = RoleDefaults.Admin };
            await _context.Roles.AddAsync(adminRole, cancellationToken);
        }

        var managerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleDefaults.Manager, cancellationToken);
        if (managerRole == null)
        {
            managerRole = new Role { Name = RoleDefaults.Manager };
            await _context.Roles.AddAsync(managerRole, cancellationToken);
        }

        var cashierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleDefaults.Cashier, cancellationToken);
        if (cashierRole == null)
        {
            cashierRole = new Role { Name = RoleDefaults.Cashier };
            await _context.Roles.AddAsync(cashierRole, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 2. Seed Single Fixed Local User (Id = 1, Owner)
        var ownerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == 1, cancellationToken);
        if (ownerUser == null)
        {
            ownerUser = new User
            {
                Id = 1,
                Username = "owner",
                PasswordHash = string.Empty,
                FullName = "Owner",
                RoleId = adminRole.Id,
                IsActive = true,
                MustChangePassword = false
            };
            await _context.Users.AddAsync(ownerUser, cancellationToken);
        }
        else
        {
            ownerUser.FullName = "Owner";
            ownerUser.MustChangePassword = false;
        }

        // 3. Seed Default Units: pcs, kg, g, litre, box, dozen
        var defaultUnits = new (string Name, string ShortName, bool AllowDecimal)[]
        {
            ("Pieces", "pcs", false),
            ("Kilogram", "kg", true),
            ("Gram", "g", true),
            ("Litre", "litre", true),
            ("Box", "box", false),
            ("Dozen", "dozen", false)
        };

        foreach (var (name, shortName, allowDecimal) in defaultUnits)
        {
            var unit = await _context.Units.FirstOrDefaultAsync(u => u.ShortName == shortName, cancellationToken);
            if (unit == null)
            {
                await _context.Units.AddAsync(new Unit
                {
                    Name = name,
                    ShortName = shortName,
                    AllowDecimal = allowDecimal,
                    IsActive = true
                }, cancellationToken);
            }
            else
            {
                unit.AllowDecimal = allowDecimal;
            }
        }

        // 4. Seed Default Category: General
        var genCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "General" && c.ParentCategoryId == null, cancellationToken);
        if (genCat == null)
        {
            genCat = new Category
            {
                Name = "General",
                ParentCategoryId = null,
                IsActive = true
            };
            await _context.Categories.AddAsync(genCat, cancellationToken);
        }

        // 5. Seed Price Levels (Retail, Wholesale)
        var retailLevel = await _context.PriceLevels.FirstOrDefaultAsync(pl => pl.Name == "Retail", cancellationToken);
        if (retailLevel == null)
        {
            retailLevel = new PriceLevel { Name = "Retail" };
            await _context.PriceLevels.AddAsync(retailLevel, cancellationToken);
        }

        var wholesaleLevel = await _context.PriceLevels.FirstOrDefaultAsync(pl => pl.Name == "Wholesale", cancellationToken);
        if (wholesaleLevel == null)
        {
            wholesaleLevel = new PriceLevel { Name = "Wholesale" };
            await _context.PriceLevels.AddAsync(wholesaleLevel, cancellationToken);
        }

        // 6. Seed Default ShopSetting
        bool settingExists = await _context.ShopSettings.AnyAsync(cancellationToken);
        if (!settingExists)
        {
            await _context.ShopSettings.AddAsync(new ShopSetting
            {
                ShopName = "My Mart & Retail Store",
                Address = "123 Market Street",
                Phone = "555-0100",
                TaxNumber = "TAX-123456",
                CurrencySymbol = "$",
                ReceiptFooter = "Thank you for shopping with us!",
                ReceiptWidthMm = 80,
                DefaultTaxPercent = 0m
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

#if DEBUG
        // Seed sample master data in debug mode if catalog is empty
        await SeedDebugSampleDataAsync(cancellationToken);
#endif
    }

    private async Task SeedDebugSampleDataAsync(CancellationToken cancellationToken)
    {
        bool hasProducts = await _context.Products.AnyAsync(cancellationToken);
        if (hasProducts) return;

        var pcsUnit = await _context.Units.FirstOrDefaultAsync(u => u.ShortName == "pcs", cancellationToken);
        var kgUnit = await _context.Units.FirstOrDefaultAsync(u => u.ShortName == "kg", cancellationToken);
        if (pcsUnit == null || kgUnit == null) return;

        // Sample Categories
        var beveragesCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Beverages", cancellationToken);
        if (beveragesCat == null)
        {
            beveragesCat = new Category { Name = "Beverages", IsActive = true };
            await _context.Categories.AddAsync(beveragesCat, cancellationToken);
        }

        var coldDrinksCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Cold Drinks", cancellationToken);
        if (coldDrinksCat == null)
        {
            coldDrinksCat = new Category { Name = "Cold Drinks", ParentCategory = beveragesCat, IsActive = true };
            await _context.Categories.AddAsync(coldDrinksCat, cancellationToken);
        }

        var bakeryCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Bakery & Fresh", cancellationToken);
        if (bakeryCat == null)
        {
            bakeryCat = new Category { Name = "Bakery & Fresh", IsActive = true };
            await _context.Categories.AddAsync(bakeryCat, cancellationToken);
        }

        // Sample Brands
        var cocaColaBrand = await _context.Brands.FirstOrDefaultAsync(b => b.Name == "Coca-Cola", cancellationToken);
        if (cocaColaBrand == null)
        {
            cocaColaBrand = new Brand { Name = "Coca-Cola", Description = "Global Beverage Company", IsActive = true };
            await _context.Brands.AddAsync(cocaColaBrand, cancellationToken);
        }

        var genericBrand = await _context.Brands.FirstOrDefaultAsync(b => b.Name == "Bakery Pride", cancellationToken);
        if (genericBrand == null)
        {
            genericBrand = new Brand { Name = "Bakery Pride", Description = "Daily fresh in-house bakery", IsActive = true };
            await _context.Brands.AddAsync(genericBrand, cancellationToken);
        }

        // Sample Suppliers
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name == "Metro Wholesale Dist.", cancellationToken);
        if (supplier == null)
        {
            supplier = new Supplier
            {
                Name = "Metro Wholesale Dist.",
                ContactPerson = "John Vendor",
                Phone = "555-8888",
                Email = "orders@metrowholesale.example",
                TaxNumber = "SUP-998877",
                Address = "Industrial Zone 4, Cargo Way",
                OpeningBalance = 250.00m, // We owe them $250
                IsActive = true
            };
            await _context.Suppliers.AddAsync(supplier, cancellationToken);
        }

        // Sample Customers
        var retailCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == "555-1212", cancellationToken);
        if (retailCustomer == null)
        {
            retailCustomer = new Customer
            {
                Name = "Alice Freeman",
                Phone = "555-1212",
                Email = "alice@example.com",
                Address = "45 Elm Street",
                CreditLimit = 500.00m,
                OpeningBalance = 45.00m, // She owes us $45
                IsActive = true
            };
            await _context.Customers.AddAsync(retailCustomer, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Sample Products with Opening Stock Movements
        var prod1 = new Product
        {
            Name = "Coca-Cola 500ml",
            Sku = "P000001",
            Barcode = "5449000000996",
            Category = coldDrinksCat,
            Brand = cocaColaBrand,
            Unit = pcsUnit,
            CostPrice = 1.10m,
            SalePrice = 1.75m,
            MinSalePrice = 1.50m,
            TaxPercent = 5.0m,
            ReorderLevel = 15m,
            TrackStock = true,
            IsActive = true
        };
        await _context.Products.AddAsync(prod1, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Add alternate barcode for prod1
        await _context.ProductBarcodes.AddAsync(new ProductBarcode
        {
            ProductId = prod1.Id,
            Barcode = "5449000000996",
            IsPrimary = true
        }, cancellationToken);

        await _context.ProductBarcodes.AddAsync(new ProductBarcode
        {
            ProductId = prod1.Id,
            Barcode = "5449000000997",
            IsPrimary = false
        }, cancellationToken);

        // Opening Stock Movement
        await _context.StockMovements.AddAsync(new StockMovement
        {
            ProductId = prod1.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 40m,
            UnitCost = prod1.CostPrice,
            MovementDate = DateTime.UtcNow,
            Note = "Initial sample stock"
        }, cancellationToken);

        var prod2 = new Product
        {
            Name = "Sourdough Bread Loaf",
            Sku = "P000002",
            Barcode = "7891000000012",
            Category = bakeryCat,
            Brand = genericBrand,
            Unit = pcsUnit,
            CostPrice = 2.00m,
            SalePrice = 3.50m,
            MinSalePrice = 3.00m,
            TaxPercent = 0m,
            ReorderLevel = 5m,
            TrackStock = true,
            IsActive = true
        };
        await _context.Products.AddAsync(prod2, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.ProductBarcodes.AddAsync(new ProductBarcode
        {
            ProductId = prod2.Id,
            Barcode = "7891000000012",
            IsPrimary = true
        }, cancellationToken);

        await _context.StockMovements.AddAsync(new StockMovement
        {
            ProductId = prod2.Id,
            MovementType = StockMovementType.Opening,
            Quantity = 12m,
            UnitCost = prod2.CostPrice,
            MovementDate = DateTime.UtcNow,
            Note = "Initial bakery stock"
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
