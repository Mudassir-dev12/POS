using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Seeding;

/// <summary>
/// Implements idempotent seed operations for ShopPOS with realistic retail master data,
/// standard EAN-13 barcodes, purchases, counter sales, and inventory stock movements.
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
        // 1. Seed Roles (for schema/FK compatibility)
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
            ownerUser.IsActive = true;
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
                unit.IsActive = true;
                unit.IsDeleted = false;
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

        // 5. Seed Default ShopSetting
        var shopSetting = await _context.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (shopSetting == null)
        {
            await _context.ShopSettings.AddAsync(new ShopSetting
            {
                ShopName = "My Mart & Retail Store",
                Address = "Plot 14-B, Commercial Avenue, Phase 5",
                Phone = "0300-1234567",
                TaxNumber = "TRN-9876543",
                CurrencySymbol = "Rs",
                CurrencyDecimals = 2,
                ReceiptFooter = "Thank you for shopping with us! Please come again.",
                ReceiptWidthMm = 80,
                DefaultTaxPercent = 0m,
                AllowSellingBelowMinPrice = false,
                PricesIncludeTax = false,
                CashRoundingStep = 0m,
                AllowReturnWithoutReceipt = false,
                AutoPrintReceipt = true,
                OpenCashDrawerOnCashSale = false,
                ReceiptCopies = 1
            }, cancellationToken);
        }

        // 6. Seed Default NumberSequences
        var defaultPrefixes = new[] { "INV", "HOLD", "SRET", "PUR", "SPAY", "PRET", "ADJ" };
        foreach (var prefix in defaultPrefixes)
        {
            var seq = await _context.NumberSequences.FirstOrDefaultAsync(n => n.Key == prefix, cancellationToken);
            if (seq == null)
            {
                await _context.NumberSequences.AddAsync(new NumberSequence { Key = prefix, LastNumber = 0 }, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 7. Seed Fresh Retail Dataset (Categories, Brands, Suppliers, Products, Barcodes, Inventory)
        await SeedFreshRetailCatalogAsync(cancellationToken);
    }

    private async Task SeedFreshRetailCatalogAsync(CancellationToken cancellationToken)
    {
        bool hasActiveProducts = await _context.Products.AnyAsync(p => !p.IsDeleted && p.IsActive, cancellationToken);
        if (hasActiveProducts) return;

        var pcsUnit = await _context.Units.FirstAsync(u => u.ShortName == "pcs", cancellationToken);
        var kgUnit = await _context.Units.FirstAsync(u => u.ShortName == "kg", cancellationToken);
        var litreUnit = await _context.Units.FirstAsync(u => u.ShortName == "litre", cancellationToken);
        var boxUnit = await _context.Units.FirstAsync(u => u.ShortName == "box", cancellationToken);
        var dozenUnit = await _context.Units.FirstAsync(u => u.ShortName == "dozen", cancellationToken);

        var retailTier = await _context.PriceLevels.FirstAsync(pl => pl.Name == "Retail", cancellationToken);
        var wholesaleTier = await _context.PriceLevels.FirstAsync(pl => pl.Name == "Wholesale", cancellationToken);

        // --- Categories ---
        var beveragesCat = await EnsureCategoryAsync("Beverages", null, cancellationToken);
        var softDrinksCat = await EnsureCategoryAsync("Soft Drinks & Sodas", beveragesCat, cancellationToken);
        var waterCat = await EnsureCategoryAsync("Water & Juices", beveragesCat, cancellationToken);

        var groceryCat = await EnsureCategoryAsync("Grocery & Staples", null, cancellationToken);
        var floursCat = await EnsureCategoryAsync("Flours & Grains", groceryCat, cancellationToken);
        var oilsCat = await EnsureCategoryAsync("Cooking Oils & Ghee", groceryCat, cancellationToken);
        var spicesCat = await EnsureCategoryAsync("Spices & Seasonings", groceryCat, cancellationToken);

        var snacksCat = await EnsureCategoryAsync("Snacks & Confectionery", null, cancellationToken);
        var biscuitsCat = await EnsureCategoryAsync("Biscuits & Cookies", snacksCat, cancellationToken);
        var chipsCat = await EnsureCategoryAsync("Crisps & Chips", snacksCat, cancellationToken);
        var sweetsCat = await EnsureCategoryAsync("Chocolates & Sweets", snacksCat, cancellationToken);

        var dairyCat = await EnsureCategoryAsync("Dairy & Breakfast", null, cancellationToken);
        var milkCat = await EnsureCategoryAsync("Milk & Cream", dairyCat, cancellationToken);
        var teaCat = await EnsureCategoryAsync("Tea & Coffee", dairyCat, cancellationToken);

        var personalCareCat = await EnsureCategoryAsync("Household & Personal Care", null, cancellationToken);
        var soapCat = await EnsureCategoryAsync("Soaps & Shampoos", personalCareCat, cancellationToken);
        var cleaningCat = await EnsureCategoryAsync("Laundry & Cleaning", personalCareCat, cancellationToken);

        // --- Brands ---
        var cokeBrand = await EnsureBrandAsync("Coca-Cola Company", "Global Beverage Leader", cancellationToken);
        var pepsiBrand = await EnsureBrandAsync("PepsiCo", "Beverages and Snack Foods", cancellationToken);
        var nestleBrand = await EnsureBrandAsync("Nestle", "Nutrition, Health and Wellness", cancellationToken);
        var unileverBrand = await EnsureBrandAsync("Unilever", "Consumer Goods and Hygiene", cancellationToken);
        var nationalBrand = await EnsureBrandAsync("National Foods", "Spices, Pickles and Recipe Mixes", cancellationToken);
        var shanBrand = await EnsureBrandAsync("Shan Foods", "Premium Recipe Mixes and Spices", cancellationToken);
        var tapalBrand = await EnsureBrandAsync("Tapal Tea", "Tea Masters since 1947", cancellationToken);
        var daldaBrand = await EnsureBrandAsync("Dalda Foods", "Purity and Quality Cooking Oils", cancellationToken);
        var ebmBrand = await EnsureBrandAsync("English Biscuit Chocolates", "Pakistan's Favorite Biscuits", cancellationToken);
        var dettolBrand = await EnsureBrandAsync("Dettol (Reckitt)", "Antiseptic and Disinfection Care", cancellationToken);

        // --- Suppliers ---
        var metroSupplier = await EnsureSupplierAsync(
            "Metro Wholesale FMCG Distribution", "Tariq Mehmood", "0300-5551234", "orders@metrowholesale.pk", 15000.00m, cancellationToken);
        var premierSupplier = await EnsureSupplierAsync(
            "Premier Consumer Goods Distributors", "Imran Khan", "0321-4449876", "info@premierdistributors.pk", 8500.00m, cancellationToken);
        var beverageSupplier = await EnsureSupplierAsync(
            "National Beverage & Food Supply Co.", "Zafar Iqbal", "0333-8882345", "sales@nationalbev.pk", 0.00m, cancellationToken);

        // --- 25 Realistic Products with Valid GS1 EAN-13 Barcodes ---
        var catalogData = new[]
        {
            // Beverages
            ("Coca-Cola Can 330ml", "P000001", "5449000000996", softDrinksCat, cokeBrand, pcsUnit, 65.00m, 90.00m, 80.00m, 78.00m, 24m, 96m),
            ("Sprite Bottle 1.5L", "P000002", "5449000011527", softDrinksCat, cokeBrand, pcsUnit, 140.00m, 180.00m, 165.00m, 155.00m, 12m, 48m),
            ("Pepsi Cola 500ml", "P000003", "0012000000133", softDrinksCat, pepsiBrand, pcsUnit, 70.00m, 100.00m, 85.00m, 82.00m, 20m, 60m),
            ("Nestle Pure Life Water 1.5L", "P000004", "8964000000021", waterCat, nestleBrand, pcsUnit, 60.00m, 90.00m, 80.00m, 75.00m, 24m, 72m),
            ("Lipton Yellow Label Tea 475g", "P000005", "8714100768924", teaCat, unileverBrand, boxUnit, 650.00m, 820.00m, 750.00m, 720.00m, 10m, 30m),
            ("Tapal Danedar Tea 430g", "P000006", "8961014010189", teaCat, tapalBrand, boxUnit, 620.00m, 790.00m, 720.00m, 690.00m, 10m, 35m),

            // Grocery & Staples
            ("Dalda Cooking Oil 1L Pouch", "P000007", "8961002000017", oilsCat, daldaBrand, litreUnit, 460.00m, 530.00m, 490.00m, 480.00m, 15m, 50m),
            ("Dalda Banaspati Ghee 1kg", "P000008", "8961002000024", oilsCat, daldaBrand, kgUnit, 470.00m, 540.00m, 500.00m, 490.00m, 15m, 40m),
            ("National Recipe Mix Biryani 50g", "P000009", "8961011000015", spicesCat, nationalBrand, pcsUnit, 75.00m, 110.00m, 95.00m, 88.00m, 30m, 100m),
            ("Shan Special Biryani Masala 50g", "P000010", "7888210000160", spicesCat, shanBrand, pcsUnit, 78.00m, 115.00m, 98.00m, 90.00m, 30m, 80m),
            ("National Pink Himalayan Salt 800g", "P000011", "8961011000459", spicesCat, nationalBrand, pcsUnit, 55.00m, 80.00m, 70.00m, 64.00m, 20m, 50m),
            ("Super Basmati Premium Rice 5kg", "P000012", "2000000000128", floursCat, nationalBrand, kgUnit, 1450.00m, 1850.00m, 1650.00m, 1580.00m, 6m, 20m),
            ("Fine Wheat Chakki Atta 10kg", "P000013", "2000000000135", floursCat, nationalBrand, kgUnit, 1180.00m, 1420.00m, 1300.00m, 1260.00m, 10m, 30m),

            // Snacks & Confectionery
            ("Lays Classic Salted Chips 42g", "P000014", "8964000300015", chipsCat, pepsiBrand, pcsUnit, 45.00m, 60.00m, 55.00m, 50.00m, 36m, 120m),
            ("Kurkure Chutney Chaska 45g", "P000015", "8964000300053", chipsCat, pepsiBrand, pcsUnit, 45.00m, 60.00m, 55.00m, 50.00m, 36m, 96m),
            ("Sooper Egg & Milk Cookies 112g", "P000016", "8961005000014", biscuitsCat, ebmBrand, pcsUnit, 50.00m, 70.00m, 60.00m, 56.00m, 24m, 84m),
            ("Rio Chocolate Cream Sandwich 100g", "P000017", "8961005000052", biscuitsCat, ebmBrand, pcsUnit, 48.00m, 65.00m, 58.00m, 54.00m, 24m, 72m),
            ("KitKat 4-Finger Milk Chocolate 41.5g", "P000018", "7613035368143", sweetsCat, nestleBrand, pcsUnit, 135.00m, 180.00m, 160.00m, 150.00m, 20m, 60m),

            // Dairy & Personal Care
            ("Nestle Everyday Milk Powder 850g", "P000019", "8964000002049", milkCat, nestleBrand, boxUnit, 980.00m, 1220.00m, 1100.00m, 1060.00m, 8m, 24m),
            ("Olpers Full Cream Pure Milk 1L", "P000020", "8964000100011", milkCat, nestleBrand, litreUnit, 240.00m, 290.00m, 270.00m, 260.00m, 24m, 60m),
            ("Dettol Original Antiseptic Soap 100g", "P000021", "8961007000012", soapCat, dettolBrand, pcsUnit, 115.00m, 150.00m, 135.00m, 128.00m, 20m, 60m),
            ("Lifebuoy Total 10 Soap Bar 115g", "P000022", "8961001000018", soapCat, unileverBrand, pcsUnit, 105.00m, 140.00m, 125.00m, 118.00m, 20m, 60m),
            ("Sunsilk Soft & Smooth Shampoo 180ml", "P000023", "8961001000551", soapCat, unileverBrand, pcsUnit, 280.00m, 360.00m, 330.00m, 310.00m, 10m, 30m),
            ("Surf Excel Washing Powder 1kg", "P000024", "8961001000803", cleaningCat, unileverBrand, kgUnit, 460.00m, 560.00m, 510.00m, 495.00m, 12m, 36m),
            ("Fresh Bananas (Local Farm)", "P000025", "2000000000258", groceryCat, nationalBrand, dozenUnit, 120.00m, 180.00m, 150.00m, 140.00m, 10m, 25m)
        };

        var createdProducts = new List<Product>();

        foreach (var (name, sku, barcode, category, brand, unit, cost, sale, minSale, wholesale, reorder, initialStock) in catalogData)
        {
            var product = new Product
            {
                Name = name,
                Sku = sku,
                Barcode = barcode,
                CategoryId = category.Id,
                BrandId = brand.Id,
                UnitId = unit.Id,
                CostPrice = cost,
                SalePrice = sale,
                MinSalePrice = minSale,
                TaxPercent = 0m,
                ReorderLevel = reorder,
                TrackStock = true,
                IsActive = true,
                IsDeleted = false
            };

            await _context.Products.AddAsync(product, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Tier Prices: Retail & Wholesale
            await _context.ProductPrices.AddAsync(new ProductPrice
            {
                ProductId = product.Id,
                PriceLevelId = retailTier.Id,
                Price = sale
            }, cancellationToken);

            await _context.ProductPrices.AddAsync(new ProductPrice
            {
                ProductId = product.Id,
                PriceLevelId = wholesaleTier.Id,
                Price = wholesale
            }, cancellationToken);

            // Opening Stock Movement
            await _context.StockMovements.AddAsync(new StockMovement
            {
                ProductId = product.Id,
                MovementType = StockMovementType.Opening,
                Quantity = initialStock,
                UnitCost = cost,
                MovementDate = DateTime.UtcNow.AddDays(-15),
                Note = "Initial inventory setup"
            }, cancellationToken);

            createdProducts.Add(product);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Audit Logs
        await _context.AuditLogs.AddAsync(new AuditLog
        {
            EntityName = "System",
            Action = "DatabaseSeeded",
            Timestamp = DateTime.UtcNow,
            UserId = 1,
            Details = "Fresh retail master data catalog, standard barcodes, and initial stock seeded successfully."
        }, cancellationToken);

await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> EnsureCategoryAsync(string name, Category? parent, CancellationToken ct)
    {
        var cat = await _context.Categories
            .FirstOrDefaultAsync(c => c.Name == name && c.ParentCategoryId == (parent != null ? parent.Id : (int?)null), ct);
        if (cat == null)
        {
            cat = new Category
            {
                Name = name,
                ParentCategoryId = parent?.Id,
                IsActive = true,
                IsDeleted = false
            };
            await _context.Categories.AddAsync(cat, ct);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            cat.IsActive = true;
            cat.IsDeleted = false;
        }
        return cat;
    }

    private async Task<Brand> EnsureBrandAsync(string name, string description, CancellationToken ct)
    {
        var brand = await _context.Brands.FirstOrDefaultAsync(b => b.Name == name, ct);
        if (brand == null)
        {
            brand = new Brand
            {
                Name = name,
                Description = description,
                IsActive = true,
                IsDeleted = false
            };
            await _context.Brands.AddAsync(brand, ct);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            brand.IsActive = true;
            brand.IsDeleted = false;
        }
        return brand;
    }

    private async Task<Supplier> EnsureSupplierAsync(string name, string contact, string phone, string email, decimal openingBal, CancellationToken ct)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name == name, ct);
        if (supplier == null)
        {
            supplier = new Supplier
            {
                Name = name,
                ContactPerson = contact,
                Phone = phone,
                Email = email,
                OpeningBalance = openingBal,
                IsActive = true,
                IsDeleted = false
            };
            await _context.Suppliers.AddAsync(supplier, ct);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            supplier.IsActive = true;
            supplier.IsDeleted = false;
        }
        return supplier;
    }
}
