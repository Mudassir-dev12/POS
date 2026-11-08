using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.DTOs.DataTools;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

public class DataToolsService : IDataToolsService
{
    private readonly AppDbContext _context;
    private readonly IBarcodeService _barcodeService;
    private readonly ISaleService _saleService;
    private readonly ISaleReturnService _saleReturnService;
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierPaymentService _supplierPaymentService;
    private readonly IPurchaseReturnService _purchaseReturnService;
    private readonly IStockLedgerQuery _stockLedger;
    private readonly IBackupService? _backupService;

    public DataToolsService(
        AppDbContext context,
        IBarcodeService barcodeService,
        ISaleService saleService,
        ISaleReturnService saleReturnService,
        IPurchaseService purchaseService,
        ISupplierPaymentService supplierPaymentService,
        IPurchaseReturnService purchaseReturnService,
        IStockLedgerQuery stockLedger,
        IBackupService? backupService = null)
    {
        _context = context;
        _barcodeService = barcodeService;
        _saleService = saleService;
        _saleReturnService = saleReturnService;
        _purchaseService = purchaseService;
        _supplierPaymentService = supplierPaymentService;
        _purchaseReturnService = purchaseReturnService;
        _stockLedger = stockLedger;
        _backupService = backupService;
    }

    public async Task<DataCountsDto> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        return new DataCountsDto
        {
            SalesCount = await _context.Sales.CountAsync(cancellationToken),
            SaleItemsCount = await _context.SaleItems.CountAsync(cancellationToken),
            SalePaymentsCount = await _context.SalePayments.CountAsync(cancellationToken),
            SaleReturnsCount = await _context.SaleReturns.CountAsync(cancellationToken),
            PurchasesCount = await _context.Purchases.CountAsync(cancellationToken),
            PurchaseItemsCount = await _context.PurchaseItems.CountAsync(cancellationToken),
            SupplierPaymentsCount = await _context.SupplierPayments.CountAsync(cancellationToken),
            PurchaseReturnsCount = await _context.PurchaseReturns.CountAsync(cancellationToken),
            StockAdjustmentsCount = await _context.StockAdjustments.CountAsync(cancellationToken),
            StockMovementsCount = await _context.StockMovements.CountAsync(cancellationToken),
            ExpensesCount = await _context.Expenses.CountAsync(cancellationToken),
            AuditLogsCount = await _context.AuditLogs.CountAsync(cancellationToken),

            ProductsCount = await _context.Products.CountAsync(cancellationToken),
            CategoriesCount = await _context.Categories.CountAsync(cancellationToken),
            BrandsCount = await _context.Brands.CountAsync(cancellationToken),
            UnitsCount = await _context.Units.CountAsync(cancellationToken),
            SuppliersCount = await _context.Suppliers.CountAsync(cancellationToken)
        };
    }

    public async Task<DataToolResultDto> ClearTransactionsAsync(string? backupDirectory = null, CancellationToken cancellationToken = default)
    {
        string backupPath;
        try
        {
            backupPath = await CreateBackupAsync("clear-transactions", backupDirectory, cancellationToken);
        }
        catch (Exception ex)
        {
            return new DataToolResultDto
            {
                Success = false,
                Error = $"Automatic database backup failed: {ex.Message}. Clear transactions was aborted to prevent data loss."
            };
        }

        var countsBefore = await GetCountsAsync(cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SalePayments;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SaleReturnItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SaleReturns;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SaleItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Sales;", cancellationToken);

            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SupplierPayments;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM PurchaseReturnItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM PurchaseReturns;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM PurchaseItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Purchases;", cancellationToken);

            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StockAdjustmentItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StockAdjustments;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StockMovements;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Expenses;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs;", cancellationToken);

            // Reset number sequences
            var prefixes = new[] { "INV", "HOLD", "SRET", "PUR", "SPAY", "PRET", "ADJ", "EXP" };
            foreach (var p in prefixes)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO NumberSequences ([Key], [LastNumber]) VALUES (@p0, 0) ON CONFLICT([Key]) DO UPDATE SET [LastNumber] = 0;",
                    new object[] { p },
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        });

        _context.ChangeTracker.Clear();
        var countsAfter = await GetCountsAsync(cancellationToken);

        return new DataToolResultDto
        {
            Success = true,
            Message = "All transaction history, stock ledger, expenses, and audit logs have been successfully cleared. Document number sequences reset to 1.",
            BackupFilePath = backupPath,
            DeletedRows = countsBefore.GetTransactionCounts(),
            KeptRows = countsAfter.GetMasterDataCounts()
        };
    }

    public async Task<DataToolResultDto> ClearEverythingAsync(string? backupDirectory = null, CancellationToken cancellationToken = default)
    {
        string backupPath;
        try
        {
            backupPath = await CreateBackupAsync("clear-everything", backupDirectory, cancellationToken);
        }
        catch (Exception ex)
        {
            return new DataToolResultDto
            {
                Success = false,
                Error = $"Automatic database backup failed: {ex.Message}. Clear everything was aborted to protect data."
            };
        }

        var countsBefore = await GetCountsAsync(cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // 1. Clear transactions first
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SalePayments;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SaleReturnItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SaleReturns;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SaleItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Sales;", cancellationToken);

            await _context.Database.ExecuteSqlRawAsync("DELETE FROM SupplierPayments;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM PurchaseReturnItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM PurchaseReturns;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM PurchaseItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Purchases;", cancellationToken);

            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StockAdjustmentItems;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StockAdjustments;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StockMovements;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Expenses;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs;", cancellationToken);

            // 2. Clear master data
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM ProductBarcodes;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM ProductPrices;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM ProductPriceHistories;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Products;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("UPDATE Categories SET ParentCategoryId = NULL;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Categories;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Brands;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Units;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Suppliers;", cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM ExpenseCategories;", cancellationToken);

            // 3. Reset number sequences
            var prefixes = new[] { "INV", "HOLD", "SRET", "PUR", "SPAY", "PRET", "ADJ", "EXP" };
            foreach (var p in prefixes)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO NumberSequences ([Key], [LastNumber]) VALUES (@p0, 0) ON CONFLICT([Key]) DO UPDATE SET [LastNumber] = 0;",
                    new object[] { p },
                    cancellationToken);
            }

            // 4. Re-seed default units
            var units = new[]
            {
                new Unit { Name = "Pieces", ShortName = "pcs", AllowDecimal = false, IsActive = true },
                new Unit { Name = "Kilogram", ShortName = "kg", AllowDecimal = true, IsActive = true },
                new Unit { Name = "Litre", ShortName = "litre", AllowDecimal = true, IsActive = true },
                new Unit { Name = "Box", ShortName = "box", AllowDecimal = false, IsActive = true },
                new Unit { Name = "Dozen", ShortName = "dozen", AllowDecimal = false, IsActive = true }
            };
            await _context.Units.AddRangeAsync(units, cancellationToken);

            // 5. Re-seed default General category
            var genCat = new Category { Name = "General", IsActive = true };
            await _context.Categories.AddAsync(genCat, cancellationToken);

            // 6. Ensure default price levels exist
            if (!await _context.PriceLevels.AnyAsync(p => p.Name == "Retail", cancellationToken))
            {
                await _context.PriceLevels.AddAsync(new PriceLevel { Name = "Retail" }, cancellationToken);
            }
            if (!await _context.PriceLevels.AnyAsync(p => p.Name == "Wholesale", cancellationToken))
            {
                await _context.PriceLevels.AddAsync(new PriceLevel { Name = "Wholesale" }, cancellationToken);
            }

            // 7. Re-seed default expense categories
            var expenseCats = new[]
            {
                new ExpenseCategory { Name = "Rent", IsActive = true },
                new ExpenseCategory { Name = "Electricity", IsActive = true },
                new ExpenseCategory { Name = "Salaries", IsActive = true },
                new ExpenseCategory { Name = "Transport", IsActive = true },
                new ExpenseCategory { Name = "Maintenance", IsActive = true },
                new ExpenseCategory { Name = "Internet and phone", IsActive = true },
                new ExpenseCategory { Name = "Packaging", IsActive = true },
                new ExpenseCategory { Name = "Other", IsActive = true }
            };
            await _context.ExpenseCategories.AddRangeAsync(expenseCats, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        _context.ChangeTracker.Clear();
        var countsAfter = await GetCountsAsync(cancellationToken);

        var deletedList = countsBefore.GetTransactionCounts();
        deletedList.AddRange(countsBefore.GetMasterDataCounts());

        return new DataToolResultDto
        {
            Success = true,
            Message = "Database cleared to fresh new shop state. Default units, General category, Retail/Wholesale price levels, and default expense categories re-seeded.",
            BackupFilePath = backupPath,
            DeletedRows = deletedList,
            KeptRows = countsAfter.GetMasterDataCounts()
        };
    }

    public async Task<DataToolResultDto> ClearAuditLogOnlyAsync(string? backupDirectory = null, CancellationToken cancellationToken = default)
    {
        string backupPath;
        try
        {
            backupPath = await CreateBackupAsync("clear-auditlog", backupDirectory, cancellationToken);
        }
        catch (Exception ex)
        {
            return new DataToolResultDto
            {
                Success = false,
                Error = $"Automatic database backup failed: {ex.Message}. Operation aborted."
            };
        }

        int countBefore = await _context.AuditLogs.CountAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs;", cancellationToken);
        _context.ChangeTracker.Clear();

        return new DataToolResultDto
        {
            Success = true,
            Message = $"Cleared {countBefore} audit log records. Master data and transactions remain completely untouched.",
            BackupFilePath = backupPath,
            DeletedRows = new List<TableCountDto> { new() { TableName = "Audit Logs", Count = countBefore } },
            KeptRows = (await GetCountsAsync(cancellationToken)).GetMasterDataCounts()
        };
    }

    public async Task<DemoDataResultDto> LoadDemoDataAsync(string? backupDirectory = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        // 1. Transaction check: must be empty
        int salesCount = await _context.Sales.CountAsync(cancellationToken);
        int purchasesCount = await _context.Purchases.CountAsync(cancellationToken);
        int adjustmentsCount = await _context.StockAdjustments.CountAsync(cancellationToken);
        int expensesCount = await _context.Expenses.CountAsync(cancellationToken);

        if (salesCount > 0 || purchasesCount > 0 || adjustmentsCount > 0 || expensesCount > 0)
        {
            return new DemoDataResultDto
            {
                Success = false,
                Error = "Transaction tables are not empty. Please run 'Clear transactions' first before loading demo data."
            };
        }

        // 2. Automated backup
        string backupPath;
        try
        {
            backupPath = await CreateBackupAsync("load-demo-data", backupDirectory, cancellationToken);
        }
        catch (Exception ex)
        {
            return new DemoDataResultDto
            {
                Success = false,
                Error = $"Automatic database backup failed: {ex.Message}. Demo data seeding aborted."
            };
        }

        progress?.Report("Creating demo categories, brands, and suppliers...");

        // 3. Master Data
        var unitPcs = await _context.Units.FirstOrDefaultAsync(u => u.ShortName.ToLower() == "pcs", cancellationToken)
                      ?? new Unit { Name = "Pieces", ShortName = "pcs", AllowDecimal = false, IsActive = true };
        var unitKg = await _context.Units.FirstOrDefaultAsync(u => u.ShortName.ToLower() == "kg", cancellationToken)
                     ?? new Unit { Name = "Kilogram", ShortName = "kg", AllowDecimal = true, IsActive = true };
        var unitLitre = await _context.Units.FirstOrDefaultAsync(u => u.ShortName.ToLower() == "litre", cancellationToken)
                        ?? new Unit { Name = "Litre", ShortName = "litre", AllowDecimal = true, IsActive = true };
        var unitBox = await _context.Units.FirstOrDefaultAsync(u => u.ShortName.ToLower() == "box", cancellationToken)
                      ?? new Unit { Name = "Box", ShortName = "box", AllowDecimal = false, IsActive = true };
        var unitDozen = await _context.Units.FirstOrDefaultAsync(u => u.ShortName.ToLower() == "dozen", cancellationToken)
                        ?? new Unit { Name = "Dozen", ShortName = "dozen", AllowDecimal = false, IsActive = true };

        if (unitPcs.Id == 0) _context.Units.Add(unitPcs);
        if (unitKg.Id == 0) _context.Units.Add(unitKg);
        if (unitLitre.Id == 0) _context.Units.Add(unitLitre);
        if (unitBox.Id == 0) _context.Units.Add(unitBox);
        if (unitDozen.Id == 0) _context.Units.Add(unitDozen);
        await _context.SaveChangesAsync(cancellationToken);

        // 6 Categories
        var catNames = new[] { "Beverages", "Snacks", "Dairy", "Household", "Personal care", "Stationery" };
        var categories = new Dictionary<string, Category>();
        foreach (var cName in catNames)
        {
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == cName, cancellationToken);
            if (cat == null)
            {
                cat = new Category { Name = cName, IsActive = true };
                _context.Categories.Add(cat);
            }
            categories[cName] = cat;
        }

        // 6 Brands
        var brandNames = new[] { "Nestle", "Coca-Cola", "PepsiCo", "Unilever", "National", "Dollar" };
        var brands = new Dictionary<string, Brand>();
        foreach (var bName in brandNames)
        {
            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.Name == bName, cancellationToken);
            if (brand == null)
            {
                brand = new Brand { Name = bName, IsActive = true };
                _context.Brands.Add(brand);
            }
            brands[bName] = brand;
        }

        // 5 Suppliers
        var suppliersData = new[]
        {
            new { Name = "Metro Wholesale Distribution", Phone = "0300-1112233" },
            new { Name = "Al-Rahman Food Distributors", Phone = "0311-2223344" },
            new { Name = "Prime Consumer Logistics", Phone = "0321-3334455" },
            new { Name = "City Beverage Suppliers", Phone = "0333-4445566" },
            new { Name = "United General Traders", Phone = "0345-5556677" }
        };
        var suppliers = new List<Supplier>();
        foreach (var sData in suppliersData)
        {
            var supp = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name == sData.Name, cancellationToken);
            if (supp == null)
            {
                supp = new Supplier { Name = sData.Name, Phone = sData.Phone, IsActive = true, OpeningBalance = 0m };
                _context.Suppliers.Add(supp);
            }
            suppliers.Add(supp);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 4. Products (~60 products across the 6 categories)
        progress?.Report("Creating 60 retail demo products with valid EAN-13 barcodes...");

        var productSpecs = GetDemoProductCatalogSpecs();
        var createdProducts = new List<Product>();

        int skuNum = 1;
        foreach (var spec in productSpecs)
        {
            var cat = categories[spec.Category];
            var brand = brands[spec.Brand];
            var unit = spec.Unit == "kg" ? unitKg : spec.Unit == "litre" ? unitLitre : spec.Unit == "box" ? unitBox : spec.Unit == "dozen" ? unitDozen : unitPcs;

            string sku = $"SKU-{skuNum:D4}";
            var existing = await _context.Products.Include(p => p.Unit).FirstOrDefaultAsync(p => p.Sku == sku || p.Name == spec.Name, cancellationToken);
            if (existing != null)
            {
                createdProducts.Add(existing);
                skuNum++;
                continue;
            }

            // Generate unique internal EAN-13 barcode using 200..299 prefix range
            string prefix = $"2{(skuNum % 90 + 10):D2}";
            string barcode = _barcodeService.GenerateEan13(prefix);

            var product = new Product
            {
                Name = spec.Name,
                Sku = sku,
                Barcode = barcode,
                CategoryId = cat.Id,
                BrandId = brand.Id,
                UnitId = unit.Id,
                CostPrice = spec.Cost,
                SalePrice = spec.SalePrice,
                MinSalePrice = spec.Cost + 5m,
                TaxPercent = spec.TaxPercent,
                ReorderLevel = spec.ReorderLevel,
                TrackStock = true,
                AllowDecimalQuantity = spec.AllowDecimal,
                IsActive = true
            };

            // Add secondary pack barcode for a few items
            if (spec.HasPackBarcode)
            {
                string packCode = _barcodeService.GenerateEan13($"2{(skuNum % 50 + 40):D2}");
                product.AdditionalBarcodes.Add(new ProductBarcode
                {
                    Barcode = packCode,
                    IsPrimary = false
                });
            }

            _context.Products.Add(product);
            createdProducts.Add(product);
            skuNum++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 5. Purchases (8 posted purchases over the last 30 days)
        progress?.Report("Receiving 8 initial inventory shipments from suppliers...");

        var baseDate = DateTime.Today.AddDays(-28);

        var purchaseDates = new[]
        {
            baseDate,
            baseDate.AddDays(4),
            baseDate.AddDays(8),
            baseDate.AddDays(12),
            baseDate.AddDays(16),
            baseDate.AddDays(20),
            baseDate.AddDays(23),
            baseDate.AddDays(26)
        };

        var postedPurchases = new List<PurchaseDetailDto>();
        for (int pIdx = 0; pIdx < purchaseDates.Length; pIdx++)
        {
            var pDate = purchaseDates[pIdx];
            var supplier = suppliers[pIdx % suppliers.Count];

            var shipmentProducts = createdProducts.Skip((pIdx * 7) % (createdProducts.Count - 10)).Take(10).ToList();

            var purchaseDto = new CreatePurchaseDto
            {
                SupplierId = supplier.Id,
                PurchaseDate = pDate,
                SupplierInvoiceNo = $"SUP-INV-{1000 + pIdx}",
                Notes = $"Demo wholesale order #{pIdx + 1}",
                Items = shipmentProducts.Select(p => new CreatePurchaseItemDto
                {
                    ProductId = p.Id,
                    Quantity = p.AllowDecimalQuantity ? 25m : 30m,
                    UnitCost = p.CostPrice,
                    DiscountPercent = 0m,
                    TaxPercent = p.TaxPercent
                }).ToList()
            };

            var createRes = await _purchaseService.CreatePurchaseDraftAsync(purchaseDto, 1, cancellationToken);
            if (createRes.Success && createRes.Data != null)
            {
                var postRes = await _purchaseService.PostPurchaseAsync(createRes.Data.Id, null, null, 1, cancellationToken);
                if (postRes.Success && postRes.Data != null)
                {
                    postedPurchases.Add(postRes.Data);

                    // Add supplier payment for some purchases
                    if (pIdx % 2 == 0)
                    {
                        decimal payAmount = Math.Round(postRes.Data.GrandTotal * 0.75m, 2);
                        await _supplierPaymentService.RecordPaymentAsync(new CreateSupplierPaymentDto
                        {
                            SupplierId = supplier.Id,
                            PurchaseId = postRes.Data.Id,
                            PaymentDate = pDate.AddDays(1),
                            Amount = payAmount,
                            Method = PaymentMethod.Bank,
                            Notes = $"Advance wire for {postRes.Data.PurchaseNumber}"
                        }, 1, cancellationToken);
                    }
                }
            }
        }

        // Add 1 Purchase Return on the first purchase
        if (postedPurchases.Count > 0 && postedPurchases[0].Items.Count > 0)
        {
            var firstP = postedPurchases[0];
            var itemToReturn = firstP.Items[0];
            await _purchaseReturnService.PostReturnAsync(new CreatePurchaseReturnDto
            {
                SupplierId = firstP.SupplierId,
                PurchaseId = firstP.Id,
                ReturnDate = purchaseDates[1],
                Notes = "Damaged outer packaging returned to vendor",
                Items = new List<CreatePurchaseReturnItemDto>
                {
                    new()
                    {
                        ProductId = itemToReturn.ProductId,
                        Quantity = 2m,
                        UnitCost = itemToReturn.UnitCost,
                        Note = "Damaged packaging"
                    }
                }
            }, 1, cancellationToken);
        }

        // 6. Sales (About 40 completed sales over the last 14 days)
        progress?.Report("Simulating 40 customer sales with varied payments, discounts, and returns...");

        var paymentMethodOptions = new[]
        {
            PaymentMethod.Cash,
            PaymentMethod.Card,
            PaymentMethod.MobileWallet,
            PaymentMethod.Bank
        };

        var completedSaleIds = new List<int>();
        var salesBaseDate = DateTime.Today.AddDays(-14);

        for (int sIdx = 1; sIdx <= 40; sIdx++)
        {
            var saleDate = salesBaseDate.AddHours(sIdx * 8);
            if (saleDate > DateTime.Now) saleDate = DateTime.Now.AddMinutes(-sIdx * 5);

            var eligibleProducts = createdProducts.Where(p => !p.Name.Contains("MilkPak") || sIdx < 10).Take(15).ToList();
            var saleItems = new List<SaleLineItemDto>();

            int itemP1 = (sIdx * 3) % eligibleProducts.Count;
            int itemP2 = (sIdx * 3 + 1) % eligibleProducts.Count;

            var prod1 = eligibleProducts[itemP1];
            decimal qty1 = prod1.AllowDecimalQuantity ? 1.5m : 2m;
            saleItems.Add(new SaleLineItemDto
            {
                ProductId = prod1.Id,
                Quantity = qty1,
                UnitPrice = prod1.SalePrice,
                DiscountPercent = sIdx % 5 == 0 ? 5m : 0m,
                TaxPercent = prod1.TaxPercent
            });

            var prod2 = eligibleProducts[itemP2];
            decimal qty2 = prod2.AllowDecimalQuantity ? 1m : 1m;
            saleItems.Add(new SaleLineItemDto
            {
                ProductId = prod2.Id,
                Quantity = qty2,
                UnitPrice = prod2.SalePrice,
                DiscountPercent = 0m,
                TaxPercent = prod2.TaxPercent
            });

            decimal rawTotal = saleItems.Sum(i => i.Quantity * i.UnitPrice * (1 - i.DiscountPercent / 100m));
            decimal taxTotal = saleItems.Sum(i => (i.Quantity * i.UnitPrice * (1 - i.DiscountPercent / 100m)) * (i.TaxPercent / 100m));
            decimal grandTotal = Math.Round(rawTotal + taxTotal, 2);

            var payMethod = paymentMethodOptions[sIdx % paymentMethodOptions.Length];
            var payments = new List<SalePaymentDto>();

            if (sIdx % 7 == 0) // Split payment
            {
                decimal half = Math.Round(grandTotal / 2m, 2);
                payments.Add(new SalePaymentDto { Method = PaymentMethod.Cash, Amount = half, TenderedAmount = half });
                payments.Add(new SalePaymentDto { Method = PaymentMethod.Card, Amount = grandTotal - half, TenderedAmount = grandTotal - half });
            }
            else
            {
                payments.Add(new SalePaymentDto { Method = payMethod, Amount = grandTotal, TenderedAmount = grandTotal });
            }

            var saleDto = new CompleteSaleDto
            {
                SaleDateOverride = saleDate,
                BillDiscountAmount = sIdx % 8 == 0 ? 20m : 0m,
                Items = saleItems,
                Payments = payments,
                Notes = $"Counter sale #{sIdx}"
            };

            var saleRes = await _saleService.CompleteSaleAsync(saleDto, cancellationToken);
            if (saleRes.Success && saleRes.Data != null)
            {
                completedSaleIds.Add(saleRes.Data.Id);
            }
        }

        // 3 Sale Returns (restocked to inventory)
        if (completedSaleIds.Count >= 3)
        {
            for (int rIdx = 0; rIdx < 3; rIdx++)
            {
                int sId = completedSaleIds[rIdx * 5 + 1];
                var saleDetails = await _saleService.GetSaleByIdAsync(sId, cancellationToken);
                if (saleDetails.Success && saleDetails.Data != null && saleDetails.Data.Items.Count > 0)
                {
                    var item = saleDetails.Data.Items[0];
                    await _saleReturnService.CreateSaleReturnAsync(new CreateSaleReturnDto
                    {
                        SaleId = sId,
                        RefundMethod = PaymentMethod.Cash,
                        Reason = "Customer exchange request",
                        ReturnDateOverride = saleDetails.Data.SaleDate.AddHours(2),
                        Items = new List<CreateSaleReturnItemDto>
                        {
                            new()
                            {
                                SaleItemId = item.Id,
                                Quantity = 1m,
                                Reason = "Customer changed mind",
                                ReturnToStock = true
                            }
                        }
                    }, cancellationToken);
                }
            }
        }

        // 2 Cancelled Sales (today)
        for (int cIdx = 0; cIdx < 2; cIdx++)
        {
            var p = createdProducts[cIdx + 4];
            var canSaleDto = new CompleteSaleDto
            {
                SaleDateOverride = DateTime.Now,
                Items = new List<SaleLineItemDto>
                {
                    new() { ProductId = p.Id, Quantity = 1m, UnitPrice = p.SalePrice, TaxPercent = p.TaxPercent }
                },
                Payments = new List<SalePaymentDto>
                {
                    new() { Method = PaymentMethod.Cash, Amount = p.SalePrice, TenderedAmount = p.SalePrice }
                },
                Notes = "Cancelled sale demo"
            };
            var cSaleRes = await _saleService.CompleteSaleAsync(canSaleDto, cancellationToken);
            if (cSaleRes.Success && cSaleRes.Data != null)
            {
                await _saleService.CancelSaleAsync(cSaleRes.Data.Id, "Customer realized they forgot their wallet", cancellationToken);
            }
        }

        // 7. Expenses (25 expenses over the last 30 days)
        progress?.Report("Recording 25 store operating expenses across utility and supply categories...");

        var expCats = await _context.ExpenseCategories.ToListAsync(cancellationToken);
        if (expCats.Count == 0)
        {
            expCats = new List<ExpenseCategory>
            {
                new() { Name = "Rent", IsActive = true },
                new() { Name = "Electricity", IsActive = true },
                new() { Name = "Salaries", IsActive = true },
                new() { Name = "Transport", IsActive = true },
                new() { Name = "Maintenance", IsActive = true },
                new() { Name = "Internet and phone", IsActive = true },
                new() { Name = "Packaging", IsActive = true },
                new() { Name = "Other", IsActive = true }
            };
            _context.ExpenseCategories.AddRange(expCats);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var expenseDescriptions = new[]
        {
            new { Cat = "Rent", Amount = 45000m, Method = PaymentMethod.Bank, Payee = "Commercial Plaza Owner", Notes = "Monthly shop lease" },
            new { Cat = "Electricity", Amount = 12500m, Method = PaymentMethod.Bank, Payee = "WAPDA / Electric Supply", Notes = "Commercial billing" },
            new { Cat = "Internet and phone", Amount = 3500m, Method = PaymentMethod.Bank, Payee = "PTCL Broadband", Notes = "POS & Optical fiber" },
            new { Cat = "Packaging", Amount = 4200m, Method = PaymentMethod.Cash, Payee = "Lahore Bags Mart", Notes = "Biodegradable shopper bags" },
            new { Cat = "Transport", Amount = 1800m, Method = PaymentMethod.Cash, Payee = "Cargo Carrier", Notes = "Goods freight charges" },
            new { Cat = "Maintenance", Amount = 2200m, Method = PaymentMethod.Cash, Payee = "Local Electrician", Notes = "LED light repair & wiring" },
            new { Cat = "Other", Amount = 850m, Method = PaymentMethod.Cash, Payee = "Corner Tea Stall", Notes = "Refreshments for unloading team" },
            new { Cat = "Transport", Amount = 2500m, Method = PaymentMethod.Cash, Payee = "Express Goods", Notes = "Stock delivery carriage" }
        };

        for (int eIdx = 1; eIdx <= 25; eIdx++)
        {
            var spec = expenseDescriptions[(eIdx - 1) % expenseDescriptions.Length];
            var eCat = expCats.FirstOrDefault(c => c.Name == spec.Cat) ?? expCats[0];
            var eDate = DateTime.Today.AddDays(-30 + eIdx);

            _context.Expenses.Add(new Expense
            {
                ExpenseNumber = $"EXP-{eIdx:D6}",
                ExpenseDate = eDate,
                ExpenseCategoryId = eCat.Id,
                Amount = spec.Amount * (spec.Cat == "Rent" || spec.Cat == "Electricity" ? 1m : (0.5m + (eIdx % 4) * 0.25m)),
                Method = spec.Method,
                Payee = spec.Payee,
                Notes = spec.Notes,
                IsCancelled = false
            });
        }
        await _context.SaveChangesAsync(cancellationToken);

        // 8. Collect 10 sample barcodes for display & physical scanner testing
        var sampleList = new List<DemoBarcodeItemDto>();
        foreach (var p in createdProducts.Take(10))
        {
            decimal currentStock = await _stockLedger.GetCurrentStockAsync(p.Id, cancellationToken);
            sampleList.Add(new DemoBarcodeItemDto
            {
                Barcode = p.Barcode,
                ProductName = p.Name,
                Sku = p.Sku,
                SalePrice = p.SalePrice,
                Unit = p.Unit?.ShortName ?? "pcs",
                StockQuantity = currentStock
            });
        }

        _context.ChangeTracker.Clear();
        return new DemoDataResultDto
        {
            Success = true,
            Message = "Demo shop successfully populated! 6 categories, 6 brands, 5 suppliers, ~60 products with valid EAN-13 barcodes, 8 purchases, 40 sales, and 25 expenses generated.",
            BackupFilePath = backupPath,
            CategoriesCreated = categories.Count,
            BrandsCreated = brands.Count,
            SuppliersCreated = suppliers.Count,
            ProductsCreated = createdProducts.Count,
            PurchasesCreated = postedPurchases.Count,
            SalesCreated = completedSaleIds.Count,
            ReturnsCreated = 3,
            ExpensesCreated = 25,
            SampleBarcodes = sampleList
        };
    }

    private async Task<string> CreateBackupAsync(string actionName, string? backupDirectory, CancellationToken cancellationToken)
    {
        if (_backupService != null && backupDirectory == null)
        {
            var res = await _backupService.CreateBackupAsync(ShopPOS.Core.Enums.BackupKind.PreDataTool, cancellationToken: cancellationToken);
            if (!res.Success)
            {
                throw new InvalidOperationException(res.ErrorMessage ?? "PreDataTool database backup failed.");
            }
            return res.FilePath ?? res.FileName ?? "backup.zip";
        }

        string dbPath = _context.Database.GetDbConnection().DataSource;
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            return "in-memory-backup.db";
        }

        string backupDir = !string.IsNullOrWhiteSpace(backupDirectory)
            ? backupDirectory
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".", "Backups");

        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backupFileName = $"backup-{actionName}-{timestamp}.db";
        string backupFilePath = Path.Combine(backupDir, backupFileName);

        using var srcConn = new SqliteConnection($"Data Source={dbPath}");
        await srcConn.OpenAsync(cancellationToken);
        using var dstConn = new SqliteConnection($"Data Source={backupFilePath}");
        await dstConn.OpenAsync(cancellationToken);
        srcConn.BackupDatabase(dstConn);

        return backupFilePath;
    }

    private record DemoProductSpec(
        string Name,
        string Category,
        string Brand,
        string Unit,
        decimal Cost,
        decimal SalePrice,
        decimal TaxPercent,
        decimal ReorderLevel,
        bool AllowDecimal,
        bool HasPackBarcode);

    private static List<DemoProductSpec> GetDemoProductCatalogSpecs()
    {
        return new List<DemoProductSpec>
        {
            // Beverages
            new("Coca-Cola 1.5L", "Beverages", "Coca-Cola", "pcs", 140m, 180m, 10m, 10m, false, true),
            new("Sprite 1.5L", "Beverages", "Coca-Cola", "pcs", 140m, 180m, 10m, 10m, false, true),
            new("Fanta Orange 1.5L", "Beverages", "Coca-Cola", "pcs", 140m, 180m, 10m, 8m, false, false),
            new("Pepsi 1.5L", "Beverages", "PepsiCo", "pcs", 135m, 175m, 10m, 10m, false, true),
            new("7Up Regular 1.5L", "Beverages", "PepsiCo", "pcs", 135m, 175m, 10m, 10m, false, false),
            new("Mountain Dew 1.5L", "Beverages", "PepsiCo", "pcs", 138m, 180m, 10m, 8m, false, false),
            new("Nestle Pure Life 1.5L Water", "Beverages", "Nestle", "pcs", 65m, 90m, 0m, 15m, false, true),
            new("Nestle Fruita Vitals Apple 1L", "Beverages", "Nestle", "pcs", 240m, 310m, 10m, 6m, false, false),
            new("Nescafe Classic Coffee 100g", "Beverages", "Nestle", "pcs", 850m, 1100m, 10m, 4m, false, false),
            new("National Mango Squash 800ml", "Beverages", "National", "pcs", 340m, 420m, 10m, 5m, false, false),

            // Snacks
            new("Lays Classic Salted 65g", "Snacks", "PepsiCo", "pcs", 75m, 100m, 0m, 12m, false, true),
            new("Lays French Cheese 65g", "Snacks", "PepsiCo", "pcs", 75m, 100m, 0m, 12m, false, true),
            new("Lays Masala 65g", "Snacks", "PepsiCo", "pcs", 75m, 100m, 0m, 12m, false, false),
            new("Kurkure Chutney Chaska 50g", "Snacks", "PepsiCo", "pcs", 45m, 60m, 0m, 15m, false, false),
            new("Cheetos Bites 40g", "Snacks", "PepsiCo", "pcs", 40m, 50m, 0m, 15m, false, false),
            new("Super Crisps BBQ 45g", "Snacks", "National", "pcs", 42m, 55m, 0m, 10m, false, false),
            new("Nestle KitKat 4-Finger 41.5g", "Snacks", "Nestle", "pcs", 160m, 210m, 10m, 8m, false, false),
            new("Nestle Milo Snack Bar 21g", "Snacks", "Nestle", "pcs", 55m, 75m, 0m, 10m, false, false),
            new("National Chat Masala 100g", "Snacks", "National", "pcs", 95m, 130m, 0m, 8m, false, false),
            new("National Fried Fish Mix 100g", "Snacks", "National", "pcs", 110m, 150m, 0m, 6m, false, false),

            // Dairy
            new("Nestle MilkPak 1L", "Dairy", "Nestle", "litre", 235m, 275m, 0m, 20m, false, true),
            new("Nestle Everyday Milk Powder 400g", "Dairy", "Nestle", "pcs", 480m, 590m, 0m, 10m, false, false),
            new("Nestle Nido 1+ 375g", "Dairy", "Nestle", "pcs", 620m, 760m, 0m, 6m, false, false),
            new("Nestle Cream 200ml", "Dairy", "Nestle", "pcs", 145m, 185m, 0m, 10m, false, false),
            new("Fresh Cow Milk (Bulk)", "Dairy", "National", "litre", 180m, 210m, 0m, 15m, true, false),
            new("Farm Fresh Desi Yogurt (Kg)", "Dairy", "National", "kg", 190m, 230m, 0m, 10m, true, false),
            new("Blue Band Margarine 250g", "Dairy", "Unilever", "pcs", 185m, 230m, 0m, 8m, false, false),
            new("National Mayonnaise 500g", "Dairy", "National", "pcs", 260m, 340m, 10m, 6m, false, false),
            new("National Tomato Ketchup 800g", "Dairy", "National", "pcs", 310m, 390m, 10m, 8m, false, false),
            new("National Garlic Sauce 300g", "Dairy", "National", "pcs", 175m, 225m, 10m, 6m, false, false),

            // Household
            new("Surf Excel 1kg Detergent", "Household", "Unilever", "kg", 460m, 580m, 10m, 10m, false, true),
            new("Ariel Original 1kg Detergent", "Household", "Unilever", "kg", 450m, 570m, 10m, 10m, false, false),
            new("Vim Dishwash Bar 300g", "Household", "Unilever", "pcs", 85m, 110m, 10m, 15m, false, false),
            new("Max Liquid Dishwash 500ml", "Household", "Unilever", "pcs", 210m, 275m, 10m, 8m, false, false),
            new("Comfort Fabric Conditioner 800ml", "Household", "Unilever", "pcs", 520m, 650m, 10m, 5m, false, false),
            new("National Bleach Regular 1L", "Household", "National", "litre", 130m, 170m, 10m, 10m, false, false),
            new("National Surface Cleaner Rose 1L", "Household", "National", "litre", 260m, 330m, 10m, 6m, false, false),
            new("National Dishwash Liquid Lemon 500ml", "Household", "National", "pcs", 195m, 250m, 10m, 8m, false, false),
            new("National Air Freshener Jasmine 300ml", "Household", "National", "pcs", 290m, 380m, 10m, 6m, false, false),
            new("National Scouring Powder 400g", "Household", "National", "pcs", 75m, 100m, 10m, 10m, false, false),

            // Personal Care
            new("Lifebuoy Total 10 Soap 115g", "Personal care", "Unilever", "pcs", 105m, 140m, 10m, 20m, false, true),
            new("Lux Velvet Touch Soap 115g", "Personal care", "Unilever", "pcs", 115m, 150m, 10m, 15m, false, true),
            new("Dove Beauty Moisture Bar 100g", "Personal care", "Unilever", "pcs", 185m, 240m, 10m, 10m, false, false),
            new("Sunsilk Black Shine Shampoo 360ml", "Personal care", "Unilever", "pcs", 440m, 560m, 10m, 8m, false, false),
            new("Clear Men Anti-Dandruff 360ml", "Personal care", "Unilever", "pcs", 470m, 600m, 10m, 6m, false, false),
            new("Ponds Bright Beauty Face Wash 100g", "Personal care", "Unilever", "pcs", 290m, 380m, 10m, 8m, false, false),
            new("CloseUp Deep Action Toothpaste 140g", "Personal care", "Unilever", "pcs", 190m, 250m, 10m, 10m, false, false),
            new("Vaseline Healthy White Lotion 200ml", "Personal care", "Unilever", "pcs", 380m, 490m, 10m, 6m, false, false),
            new("Rexona Men Invisible Deodorant 150ml", "Personal care", "Unilever", "pcs", 420m, 540m, 10m, 5m, false, false),
            new("Fair & Lovely Glow & Lovely 50g", "Personal care", "Unilever", "pcs", 210m, 275m, 10m, 10m, false, false),

            // Stationery
            new("Dollar Gel Pen 0.5mm (Blue)", "Stationery", "Dollar", "pcs", 35m, 50m, 0m, 30m, false, true),
            new("Dollar Gel Pen 0.5mm (Black)", "Stationery", "Dollar", "pcs", 35m, 50m, 0m, 30m, false, true),
            new("Dollar Permanent Marker 70 (Black)", "Stationery", "Dollar", "pcs", 65m, 90m, 0m, 20m, false, false),
            new("Dollar Permanent Marker 70 (Blue)", "Stationery", "Dollar", "pcs", 65m, 90m, 0m, 20m, false, false),
            new("Dollar Whiteboard Marker 90 (Black)", "Stationery", "Dollar", "pcs", 75m, 100m, 0m, 20m, false, false),
            new("Dollar Whiteboard Marker 90 (Blue)", "Stationery", "Dollar", "pcs", 75m, 100m, 0m, 20m, false, false),
            new("Dollar Correction Pen 7ml", "Stationery", "Dollar", "pcs", 80m, 110m, 0m, 15m, false, false),
            new("Dollar Highlighter Pastel (Yellow)", "Stationery", "Dollar", "pcs", 70m, 95m, 0m, 15m, false, false),
            new("Dollar Glue Stick 15g", "Stationery", "Dollar", "pcs", 60m, 85m, 0m, 15m, false, false),
            new("Dollar Ballpoint 0.7mm Box of 10", "Stationery", "Dollar", "box", 220m, 300m, 0m, 10m, false, false)
        };
    }
}
