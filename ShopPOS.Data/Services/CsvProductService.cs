using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using global::CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using ShopPOS.Core.DTOs;
using ShopPOS.Core.Entities;
using ShopPOS.Core.Enums;
using ShopPOS.Core.Interfaces;
using ShopPOS.Core.Models;

namespace ShopPOS.Data.Services;

public class CsvProductService : ICsvProductService
{
    private readonly AppDbContext _context;
    private readonly IProductRepository _productRepository;
    private readonly IAuditLogService _auditLogService;

    public CsvProductService(
        AppDbContext context,
        IProductRepository productRepository,
        IAuditLogService auditLogService)
    {
        _context = context;
        _productRepository = productRepository;
        _auditLogService = auditLogService;
    }

    public async Task<byte[]> ExportProductsToCsvAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllForExportAsync(query, cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();
        var stockMap = await _context.StockMovements
            .AsNoTracking()
            .Where(sm => !sm.IsDeleted && productIds.Contains(sm.ProductId))
            .GroupBy(sm => sm.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(sm => sm.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);

        using var memoryStream = new MemoryStream();
        using var streamWriter = new StreamWriter(memoryStream, new UTF8Encoding(true));
        using var csv = new CsvWriter(streamWriter, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        // Headers
        csv.WriteField("SKU");
        csv.WriteField("Barcode");
        csv.WriteField("Name");
        csv.WriteField("Category");
        csv.WriteField("Brand");
        csv.WriteField("Unit");
        csv.WriteField("CostPrice");
        csv.WriteField("SalePrice");
        csv.WriteField("MinSalePrice");
        csv.WriteField("TaxPercent");
        csv.WriteField("ReorderLevel");
        csv.WriteField("CurrentStock");
        csv.WriteField("TrackStock");
        csv.WriteField("IsWeighable");
        csv.WriteField("AllowDecimalQuantity");
        csv.WriteField("Description");
        csv.WriteField("IsActive");
        await csv.NextRecordAsync();

        foreach (var p in products)
        {
            decimal stock = stockMap.TryGetValue(p.Id, out var s) ? s : 0m;

            csv.WriteField(p.Sku);
            csv.WriteField(p.Barcode ?? string.Empty);
            csv.WriteField(p.Name);
            csv.WriteField(p.Category?.Name ?? string.Empty);
            csv.WriteField(p.Brand?.Name ?? string.Empty);
            csv.WriteField(p.Unit?.ShortName ?? p.Unit?.Name ?? string.Empty);
            csv.WriteField(p.CostPrice.ToString("F2", CultureInfo.InvariantCulture));
            csv.WriteField(p.SalePrice.ToString("F2", CultureInfo.InvariantCulture));
            csv.WriteField(p.MinSalePrice.HasValue ? p.MinSalePrice.Value.ToString("F2", CultureInfo.InvariantCulture) : string.Empty);
            csv.WriteField(p.TaxPercent.ToString("F2", CultureInfo.InvariantCulture));
            csv.WriteField(p.ReorderLevel.ToString("F3", CultureInfo.InvariantCulture));
            csv.WriteField(stock.ToString("F3", CultureInfo.InvariantCulture));
            csv.WriteField(p.TrackStock);
            csv.WriteField(p.IsWeighable);
            csv.WriteField(p.AllowDecimalQuantity);
            csv.WriteField(p.Description ?? string.Empty);
            csv.WriteField(p.IsActive);
            await csv.NextRecordAsync();
        }

        await streamWriter.FlushAsync(cancellationToken);
        return memoryStream.ToArray();
    }

    public async Task<byte[]> ExportProductsToExcelAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllForExportAsync(query, cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();
        var stockMap = await _context.StockMovements
            .AsNoTracking()
            .Where(sm => !sm.IsDeleted && productIds.Contains(sm.ProductId))
            .GroupBy(sm => sm.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(sm => sm.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Products");

        string[] headers = [
            "SKU", "Barcode", "Product Name", "Category", "Brand", "Unit",
            "Cost Price", "Sale Price", "Min Sale Price", "Tax %",
            "Reorder Level", "Current Stock", "Track Stock", "Is Weighable", "Allow Decimal",
            "Description", "Status"
        ];

        for (int col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            cell.Style.Border.BottomBorderColor = XLColor.FromArgb(203, 213, 225);
        }

        int row = 2;
        foreach (var p in products)
        {
            decimal stock = stockMap.TryGetValue(p.Id, out var s) ? s : 0m;
            ws.Cell(row, 1).SetValue(p.Sku);
            ws.Cell(row, 2).SetValue(p.Barcode ?? string.Empty);
            ws.Cell(row, 3).SetValue(p.Name);
            ws.Cell(row, 4).SetValue(p.Category?.Name ?? string.Empty);
            ws.Cell(row, 5).SetValue(p.Brand?.Name ?? string.Empty);
            ws.Cell(row, 6).SetValue(p.Unit?.ShortName ?? p.Unit?.Name ?? string.Empty);
            ws.Cell(row, 7).SetValue(p.CostPrice);
            ws.Cell(row, 8).SetValue(p.SalePrice);
            if (p.MinSalePrice.HasValue) ws.Cell(row, 9).SetValue(p.MinSalePrice.Value);
            ws.Cell(row, 10).SetValue(p.TaxPercent);
            ws.Cell(row, 11).SetValue(p.ReorderLevel);
            ws.Cell(row, 12).SetValue(stock);
            ws.Cell(row, 13).SetValue(p.TrackStock ? "Yes" : "No");
            ws.Cell(row, 14).SetValue(p.IsWeighable ? "Yes" : "No");
            ws.Cell(row, 15).SetValue(p.AllowDecimalQuantity ? "Yes" : "No");
            ws.Cell(row, 16).SetValue(p.Description ?? string.Empty);
            ws.Cell(row, 17).SetValue(p.IsActive ? "Active" : "Inactive");
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GetSampleCsvTemplate()
    {
        var sb = new StringBuilder();
        // UTF-8 BOM
        sb.AppendLine("SKU,Barcode,Product Name,Category,Brand,Unit,Cost Price,Sale Price,Min Sale Price,Tax %,Reorder Level,Current Stock,Track Stock,Description,Status");
        sb.AppendLine("P000001,5449000000996,Coca-Cola 500ml,Beverages,Coca-Cola,pcs,1.10,1.75,1.50,5.0,15,100,Yes,Chilled soda bottle,Active");
        sb.AppendLine("P000002,7891000000012,Sourdough Bread,Bakery & Fresh,Bakery Pride,pcs,2.00,3.50,3.00,0.0,5,30,Yes,Freshly baked daily,Active");
        sb.AppendLine(",5449000000998,Apple Royal Gala,Fresh Produce,,kg,1.80,2.90,,0.0,10,50,Yes,Crisp fresh apples,Active");

        return new UTF8Encoding(true).GetBytes(sb.ToString());
    }

    public byte[] GetSampleExcelTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Products Template");

        string[] headers = [
            "SKU", "Barcode", "Product Name", "Category", "Brand", "Unit",
            "Cost Price", "Sale Price", "Min Sale Price", "Tax %",
            "Reorder Level", "Current Stock", "Track Stock", "Description", "Status"
        ];

        for (int col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(224, 231, 255);
        }

        object[][] samples = [
            ["P000001", "5449000000996", "Coca-Cola 500ml", "Beverages", "Coca-Cola", "pcs", 1.10m, 1.75m, 1.50m, 5.0m, 15m, 100m, "Yes", "Chilled soda bottle", "Active"],
            ["P000002", "7891000000012", "Sourdough Bread", "Bakery & Fresh", "Bakery Pride", "pcs", 2.00m, 3.50m, 3.00m, 0.0m, 5m, 30m, "Yes", "Freshly baked daily", "Active"],
            ["", "5449000000998", "Apple Royal Gala", "Fresh Produce", "", "kg", 1.80m, 2.90m, 2.50m, 0.0m, 10m, 50m, "Yes", "Crisp fresh apples", "Active"]
        ];

        for (int r = 0; r < samples.Length; r++)
        {
            for (int c = 0; c < samples[r].Length; c++)
            {
                ws.Cell(r + 2, c + 1).SetValue(samples[r][c]?.ToString() ?? string.Empty);
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<Result<CsvPreviewResult>> PreviewCsvAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            var (headers, rows) = await ExtractDataFromStreamAsync(stream, cancellationToken);
            var previewRows = new List<Dictionary<string, string>>();

            foreach (var (_, rowData) in rows.Take(20))
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in headers)
                {
                    dict[h] = rowData.TryGetValue(NormalizeKey(h), out var v) ? v : string.Empty;
                }
                previewRows.Add(dict);
            }

            string fileTypeInfo = IsExcelStream(stream)
                ? "Excel Workbook (.xlsx)"
                : $"CSV Delimited (Detected delimiter: '{DetectCsvDelimiter(stream)}')";

            string? detectedNameCol = null;
            foreach (var h in headers)
            {
                string norm = NormalizeKey(h);
                if (norm == "name" || norm == "productname" || norm == "itemname" || norm.Contains("name") || norm.Contains("prod") || norm.Contains("item"))
                {
                    detectedNameCol = h;
                    break;
                }
            }

            return Result.Ok(new CsvPreviewResult
            {
                Headers = headers,
                PreviewRows = previewRows,
                TotalRowsFound = rows.Count,
                FileTypeInfo = fileTypeInfo,
                DetectedNameColumn = detectedNameCol
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<CsvPreviewResult>($"Failed to read file: {ex.Message}");
        }
    }

    public async Task<Result<CsvValidationReport>> ValidateCsvAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            var (_, rows) = await ExtractDataFromStreamAsync(stream, cancellationToken);
            var errors = new List<CsvRowError>();

            foreach (var (rowNumber, rowData) in rows)
            {
                string name = ResolveProductName(rowData);
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new CsvRowError { RowNumber = rowNumber, Error = "Product Name is required." });
                    continue;
                }

                string costStr = GetVal(rowData, "costprice", "cost", "purchaseprice", "unitcost", "buyingprice", "buyprice");
                if (!string.IsNullOrWhiteSpace(costStr) && (!decimal.TryParse(costStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var c) || c < 0))
                {
                    errors.Add(new CsvRowError { RowNumber = rowNumber, Error = $"Invalid Cost Price '{costStr}'. Must be >= 0." });
                    continue;
                }

                string saleStr = GetVal(rowData, "saleprice", "price", "sellingprice", "retailprice", "unitprice", "retail", "sale");
                if (!string.IsNullOrWhiteSpace(saleStr) && (!decimal.TryParse(saleStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var s) || s < 0))
                {
                    errors.Add(new CsvRowError { RowNumber = rowNumber, Error = $"Invalid Sale Price '{saleStr}'. Must be >= 0." });
                    continue;
                }

                string minSaleStr = GetVal(rowData, "minsaleprice", "minprice", "floorprice");
                if (!string.IsNullOrWhiteSpace(minSaleStr) && decimal.TryParse(minSaleStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
                {
                    if (m < 0)
                    {
                        errors.Add(new CsvRowError { RowNumber = rowNumber, Error = "Minimum Sale Price cannot be negative." });
                        continue;
                    }
                    if (decimal.TryParse(saleStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sp) && m > sp)
                    {
                        errors.Add(new CsvRowError { RowNumber = rowNumber, Error = $"MinSalePrice ({m}) cannot exceed SalePrice ({sp})." });
                        continue;
                    }
                }
            }

            return Result.Ok(new CsvValidationReport
            {
                TotalRows = rows.Count,
                ErrorRowCount = errors.Count,
                ValidRowCount = rows.Count - errors.Count,
                Errors = errors
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<CsvValidationReport>($"Failed to validate file: {ex.Message}");
        }
    }

    public async Task<Result<CsvImportResult>> ImportCsvAsync(
        Stream stream,
        CsvImportOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // single-user: no permission check needed

        try
        {
            var (_, rows) = await ExtractDataFromStreamAsync(stream, cancellationToken);
            int totalRows = rows.Count;
            if (totalRows == 0)
            {
                return Result.Ok(new CsvImportResult());
            }

            // Pre-load reference maps for categories, brands, units
            var categoryMap = await _context.Categories
                .ToDictionaryAsync(c => c.Name.Trim().ToLower(), c => c, cancellationToken);

            var brandMap = await _context.Brands
                .ToDictionaryAsync(b => b.Name.Trim().ToLower(), b => b, cancellationToken);

            var unitMap = await _context.Units
                .ToDictionaryAsync(u => u.ShortName.Trim().ToLower(), u => u, cancellationToken);

            var unitFullNameMap = await _context.Units
                .ToDictionaryAsync(u => u.Name.Trim().ToLower(), u => u, cancellationToken);

            var defaultUnit = await _context.Units.FirstOrDefaultAsync(u => u.ShortName == "pcs", cancellationToken)
                ?? await _context.Units.FirstOrDefaultAsync(cancellationToken);

            var defaultCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "General", cancellationToken)
                ?? await _context.Categories.FirstOrDefaultAsync(cancellationToken);

            var result = new CsvImportResult();
            const int batchSize = 500;

            for (int i = 0; i < totalRows; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = rows.Skip(i).Take(batchSize).ToList();

                using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

                foreach (var (rowNumber, rowData) in batch)
                {
                    string name = ResolveProductName(rowData);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        result.Errors.Add(new CsvRowError { RowNumber = rowNumber, Error = "Missing required product name." });
                        result.ErrorCount++;
                        result.ProcessedCount++;
                        continue;
                    }

                    // Price and numeric parsing
                    decimal cost = ParseDecimal(GetVal(rowData, "costprice", "cost", "purchaseprice", "unitcost", "buyingprice", "buyprice"), 0m);
                    decimal sale = ParseDecimal(GetVal(rowData, "saleprice", "price", "sellingprice", "retailprice", "unitprice", "retail", "sale"), 0m);
                    decimal? minSale = ParseNullableDecimal(GetVal(rowData, "minsaleprice", "minprice", "floorprice"));
                    decimal tax = ParseDecimal(GetVal(rowData, "taxpercent", "tax", "taxrate", "vat", "gst"), 0m);
                    decimal reorder = ParseDecimal(GetVal(rowData, "reorderlevel", "reorder", "minstock", "alertlevel", "lowstock"), 0m);
                    decimal? stockQty = ParseNullableDecimal(GetVal(rowData, "currentstock", "stock", "quantity", "qty", "openingstock", "initialstock", "onhand"));

                    bool trackStock = ParseBool(GetVal(rowData, "trackstock", "trackinventory"), true);
                    bool isWeighable = ParseBool(GetVal(rowData, "isweighable", "weighable", "soldbyweight"), false);
                    bool allowDecimal = ParseBool(GetVal(rowData, "allowdecimalquantity", "allowdecimal", "decimalqty"), false);
                    bool isActive = ParseBool(GetVal(rowData, "isactive", "active", "status", "enabled"), true);
                    string? desc = GetVal(rowData, "description", "desc", "details", "notes");

                    string? barcode = GetVal(rowData, "barcode", "primarybarcode", "upc", "ean", "barcodes", "codebar");
                    if (string.IsNullOrWhiteSpace(barcode)) barcode = null;

                    string? sku = GetVal(rowData, "sku", "itemcode", "productcode", "code", "itemno");
                    if (string.IsNullOrWhiteSpace(sku)) sku = null;

                    // 1. Resolve Category (case-insensitive auto create)
                    string catName = GetVal(rowData, "category", "categoryname", "group", "department");
                    Category category;
                    if (!string.IsNullOrWhiteSpace(catName))
                    {
                        string catKey = catName.Trim().ToLower();
                        if (!categoryMap.TryGetValue(catKey, out category!))
                        {
                            category = new Category { Name = catName.Trim(), IsActive = true, CreatedByUserId = options.UserId };
                            await _context.Categories.AddAsync(category, cancellationToken);
                            await _context.SaveChangesAsync(cancellationToken);
                            categoryMap[catKey] = category;
                        }
                    }
                    else
                    {
                        category = defaultCategory!;
                    }

                    // 2. Resolve Brand (case-insensitive auto create if provided)
                    string brandName = GetVal(rowData, "brand", "brandname", "manufacturer", "make");
                    Brand? brand = null;
                    if (!string.IsNullOrWhiteSpace(brandName))
                    {
                        string brandKey = brandName.Trim().ToLower();
                        if (!brandMap.TryGetValue(brandKey, out brand))
                        {
                            brand = new Brand { Name = brandName.Trim(), IsActive = true, CreatedByUserId = options.UserId };
                            await _context.Brands.AddAsync(brand, cancellationToken);
                            await _context.SaveChangesAsync(cancellationToken);
                            brandMap[brandKey] = brand;
                        }
                    }

                    // 3. Resolve Unit (case-insensitive auto create if provided)
                    string unitName = GetVal(rowData, "unit", "unitname", "unitshortname", "uom");
                    Unit unit;
                    if (!string.IsNullOrWhiteSpace(unitName))
                    {
                        string uKey = unitName.Trim().ToLower();
                        if (!unitMap.TryGetValue(uKey, out unit!) && !unitFullNameMap.TryGetValue(uKey, out unit!))
                        {
                            unit = new Unit { Name = unitName.Trim(), ShortName = unitName.Trim().ToLower(), IsActive = true, CreatedByUserId = options.UserId };
                            await _context.Units.AddAsync(unit, cancellationToken);
                            await _context.SaveChangesAsync(cancellationToken);
                            unitMap[uKey] = unit;
                        }
                    }
                    else
                    {
                        unit = defaultUnit!;
                    }

                    // 4. Match existing product by SKU, Barcode, or exact Name
                    Product? existing = null;
                    if (!string.IsNullOrWhiteSpace(sku))
                    {
                        existing = await _context.Products.FirstOrDefaultAsync(p => p.Sku.ToLower() == sku.Trim().ToLower(), cancellationToken);
                    }

                    if (existing == null && !string.IsNullOrWhiteSpace(barcode))
                    {
                        existing = await _context.Products.FirstOrDefaultAsync(p => p.Barcode == barcode.Trim(), cancellationToken);
                        if (existing == null)
                        {
                            var matchingPb = await _context.ProductBarcodes
                                .Include(pb => pb.Product)
                                .FirstOrDefaultAsync(pb => pb.Barcode == barcode.Trim(), cancellationToken);
                            if (matchingPb != null)
                            {
                                existing = matchingPb.Product;
                            }
                        }
                    }

                    if (existing == null && !string.IsNullOrWhiteSpace(name))
                    {
                        existing = await _context.Products.FirstOrDefaultAsync(p => p.Name.ToLower() == name.Trim().ToLower(), cancellationToken);
                    }

                    if (existing != null)
                    {
                        if (options.UpdateExisting)
                        {
                            // Price change audit if price modified
                            if (existing.CostPrice != cost || existing.SalePrice != sale)
                            {
                                await _productRepository.RecordPriceChangeAsync(
                                    existing.Id,
                                    existing.CostPrice,
                                    cost,
                                    existing.SalePrice,
                                    sale,
                                    options.UserId,
                                    cancellationToken);
                            }

                            existing.Name = name.Trim();
                            if (!string.IsNullOrWhiteSpace(barcode))
                            {
                                existing.Barcode = barcode.Trim();
                                var hasPb = await _context.ProductBarcodes
                                    .AnyAsync(pb => pb.ProductId == existing.Id && pb.Barcode == barcode.Trim(), cancellationToken);
                                if (!hasPb)
                                {
                                    await _context.ProductBarcodes.AddAsync(new ProductBarcode
                                    {
                                        ProductId = existing.Id,
                                        Barcode = barcode.Trim(),
                                        IsPrimary = true
                                    }, cancellationToken);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(sku)) existing.Sku = sku.Trim();
                            existing.CategoryId = category.Id;
                            existing.BrandId = brand?.Id;
                            existing.UnitId = unit.Id;
                            existing.CostPrice = cost;
                            existing.SalePrice = sale;
                            existing.MinSalePrice = minSale;
                            existing.TaxPercent = tax;
                            existing.ReorderLevel = reorder;
                            existing.TrackStock = trackStock;
                            existing.IsWeighable = isWeighable;
                            existing.AllowDecimalQuantity = allowDecimal;
                            existing.IsActive = isActive;
                            if (!string.IsNullOrWhiteSpace(desc)) existing.Description = desc.Trim();

                            // Adjust stock if specified in the imported file
                            if (stockQty.HasValue)
                            {
                                decimal currentStock = await _context.StockMovements
                                    .Where(sm => !sm.IsDeleted && sm.ProductId == existing.Id)
                                    .SumAsync(sm => (decimal?)sm.Quantity, cancellationToken) ?? 0m;
                                decimal delta = stockQty.Value - currentStock;
                                if (delta != 0)
                                {
                                    await _productRepository.RecordStockAdjustmentAsync(
                                        existing.Id,
                                        delta,
                                        existing.CostPrice,
                                        "Stock adjustment from file import",
                                        options.UserId,
                                        cancellationToken);
                                }
                            }

                            result.UpdatedCount++;
                        }
                        else
                        {
                            result.SkippedCount++;
                        }
                    }
                    else
                    {
                        // Insert new product
                        string finalSku = string.IsNullOrWhiteSpace(sku)
                            ? await _productRepository.GenerateNextSkuAsync(cancellationToken)
                            : sku.Trim();

                        var newProduct = new Product
                        {
                            Name = name.Trim(),
                            Sku = finalSku,
                            Barcode = barcode?.Trim(),
                            CategoryId = category.Id,
                            BrandId = brand?.Id,
                            UnitId = unit.Id,
                            CostPrice = cost,
                            SalePrice = sale,
                            MinSalePrice = minSale,
                            TaxPercent = tax,
                            ReorderLevel = reorder,
                            TrackStock = trackStock,
                            IsWeighable = isWeighable,
                            AllowDecimalQuantity = allowDecimal,
                            IsActive = isActive,
                            Description = desc?.Trim(),
                            CreatedByUserId = options.UserId
                        };

                        await _context.Products.AddAsync(newProduct, cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);

                        if (!string.IsNullOrWhiteSpace(newProduct.Barcode))
                        {
                            await _context.ProductBarcodes.AddAsync(new ProductBarcode
                            {
                                ProductId = newProduct.Id,
                                Barcode = newProduct.Barcode,
                                IsPrimary = true
                            }, cancellationToken);
                        }

                        // Create opening stock if specified
                        if (stockQty.HasValue && stockQty.Value > 0)
                        {
                            await _context.StockMovements.AddAsync(new StockMovement
                            {
                                ProductId = newProduct.Id,
                                MovementType = StockMovementType.Opening,
                                Quantity = stockQty.Value,
                                UnitCost = newProduct.CostPrice,
                                Note = "Initial opening stock from file import",
                                CreatedByUserId = options.UserId
                            }, cancellationToken);
                        }

                        result.InsertedCount++;
                    }

                    result.ProcessedCount++;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                int progressPercent = (int)Math.Min(100, Math.Round((double)(i + batch.Count) / totalRows * 100));
                progress?.Report(progressPercent);
            }

            await _auditLogService.LogAsync(
                options.UserId,
                "ImportProducts",
                "Product",
                null,
                $"Imported catalog: Processed {result.ProcessedCount}, Inserted {result.InsertedCount}, Updated {result.UpdatedCount}, Skipped {result.SkippedCount}, Errors {result.ErrorCount}");

            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<CsvImportResult>("Import operation was cancelled.");
        }
        catch (Exception ex)
        {
            return Result.Failure<CsvImportResult>($"Error during import: {ex.Message}");
        }
    }

    private static async Task<(List<string> Headers, List<(int RowNumber, Dictionary<string, string> Data)> Rows)> ExtractDataFromStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek) stream.Position = 0;

        (List<string> Headers, List<(int RowNumber, Dictionary<string, string> Data)> Rows) result;
        if (IsExcelStream(stream))
        {
            result = ExtractFromExcel(stream);
        }
        else
        {
            result = await ExtractFromCsvAsync(stream, cancellationToken);
        }

        if (stream.CanSeek) stream.Position = 0;
        return result;
    }

    private static bool IsExcelStream(Stream stream)
    {
        if (!stream.CanSeek) return false;
        long pos = stream.Position;
        try
        {
            stream.Position = 0;
            if (stream.Length < 4) return false;
            byte[] buffer = new byte[4];
            int read = stream.Read(buffer, 0, 4);
            return read >= 4 && buffer[0] == 0x50 && buffer[1] == 0x4B && (buffer[2] == 0x03 || buffer[2] == 0x05 || buffer[2] == 0x07);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (stream.CanSeek) stream.Position = pos;
        }
    }

    private static string DetectCsvDelimiter(Stream stream)
    {
        if (!stream.CanSeek) return ",";
        long originalPos = stream.Position;
        try
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 2048, leaveOpen: true);
            string? firstLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(firstLine)) return ",";

            int commas = firstLine.Count(c => c == ',');
            int semicolons = firstLine.Count(c => c == ';');
            int tabs = firstLine.Count(c => c == '\t');
            int pipes = firstLine.Count(c => c == '|');

            if (semicolons > commas && semicolons >= tabs && semicolons >= pipes) return ";";
            if (tabs > commas && tabs >= semicolons && tabs >= pipes) return "\t";
            if (pipes > commas && pipes >= semicolons && pipes >= tabs) return "|";
            return ",";
        }
        catch
        {
            return ",";
        }
        finally
        {
            if (stream.CanSeek) stream.Position = originalPos;
        }
    }

    private static (List<string> Headers, List<(int RowNumber, Dictionary<string, string> Data)> Rows) ExtractFromExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault() ?? throw new InvalidOperationException("Excel file contains no worksheets.");

        var range = worksheet.RangeUsed();
        if (range == null)
        {
            return (new List<string>(), new List<(int, Dictionary<string, string>)>());
        }

        int firstCol = range.FirstColumn().ColumnNumber();
        int lastCol = range.LastColumn().ColumnNumber();
        int firstRow = range.FirstRow().RowNumber();
        int lastRow = range.LastRow().RowNumber();

        // Find header row: look for the first row with at least 2 non-empty cells
        int headerRowIndex = firstRow;
        for (int r = firstRow; r <= Math.Min(firstRow + 5, lastRow); r++)
        {
            int nonEmpty = 0;
            for (int c = firstCol; c <= lastCol; c++)
            {
                var val = GetCellString(worksheet.Cell(r, c));
                if (!string.IsNullOrWhiteSpace(val)) nonEmpty++;
            }
            if (nonEmpty >= 2)
            {
                headerRowIndex = r;
                break;
            }
        }

        var headers = new List<string>();
        for (int c = firstCol; c <= lastCol; c++)
        {
            string h = GetCellString(worksheet.Cell(headerRowIndex, c));
            headers.Add(string.IsNullOrWhiteSpace(h) ? $"Column_{c}" : h);
        }

        var rows = new List<(int RowNumber, Dictionary<string, string> Data)>();
        for (int r = headerRowIndex + 1; r <= lastRow; r++)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool allEmpty = true;

            for (int c = firstCol; c <= lastCol; c++)
            {
                int headerIdx = c - firstCol;
                if (headerIdx >= headers.Count) break;

                string val = GetCellString(worksheet.Cell(r, c));
                if (!string.IsNullOrWhiteSpace(val)) allEmpty = false;

                string normKey = NormalizeKey(headers[headerIdx]);
                if (!string.IsNullOrEmpty(normKey))
                {
                    dict[normKey] = val;
                }
            }

            if (!allEmpty)
            {
                rows.Add((r, dict));
            }
        }

        return (headers, rows);
    }

    private static string GetCellString(IXLCell cell)
    {
        try
        {
            if (cell.IsEmpty()) return string.Empty;
            return cell.GetFormattedString()?.Trim() ?? cell.Value.ToString().Trim();
        }
        catch
        {
            try
            {
                return cell.Value.ToString().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private static async Task<(List<string> Headers, List<(int RowNumber, Dictionary<string, string> Data)> Rows)> ExtractFromCsvAsync(Stream stream, CancellationToken cancellationToken)
    {
        string delimiter = DetectCsvDelimiter(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 2048, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord?.Select(h => h.Trim()).ToList() ?? new List<string>();

        var rows = new List<(int RowNumber, Dictionary<string, string> Data)>();
        int rowNumber = 1;

        while (await csv.ReadAsync())
        {
            rowNumber++;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool allEmpty = true;

            foreach (var h in headers)
            {
                string val = csv.GetField(h)?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(val)) allEmpty = false;

                string normKey = NormalizeKey(h);
                if (!string.IsNullOrEmpty(normKey))
                {
                    dict[normKey] = val;
                }
            }

            if (!allEmpty)
            {
                rows.Add((rowNumber, dict));
            }
        }

        return (headers, rows);
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        return new string(key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string ResolveProductName(Dictionary<string, string> dict)
    {
        // 1. Direct alias check
        string val = GetVal(dict,
            "productname", "name", "itemname", "item", "product", "title",
            "producttitle", "items", "products", "itemdescription", "article",
            "artiklename", "designation", "label", "nom", "produit");

        if (!string.IsNullOrWhiteSpace(val)) return val;

        // 2. Substring matching: any header containing "name", "item", "prod", or "title"
        var candidateKey = dict.Keys.FirstOrDefault(k =>
            k.Contains("name") || k.Contains("item") || k.Contains("prod") || k.Contains("title"));

        if (candidateKey != null && !string.IsNullOrWhiteSpace(dict[candidateKey]))
        {
            return dict[candidateKey].Trim();
        }

        return string.Empty;
    }

    private static string GetVal(Dictionary<string, string> dict, params string[] normalizedAliases)
    {
        foreach (var alias in normalizedAliases)
        {
            if (dict.TryGetValue(alias, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val.Trim();
            }
        }

        // Fuzzy match: if any dictionary key contains one of the aliases as substring
        foreach (var alias in normalizedAliases)
        {
            if (alias.Length >= 4)
            {
                var match = dict.FirstOrDefault(kv => kv.Key.Contains(alias) && !string.IsNullOrWhiteSpace(kv.Value));
                if (!string.IsNullOrEmpty(match.Key))
                {
                    return match.Value.Trim();
                }
            }
        }

        return string.Empty;
    }

    private static decimal ParseDecimal(string? val, decimal defaultVal)
    {
        if (string.IsNullOrWhiteSpace(val)) return defaultVal;
        // Clean currency symbols or commas
        val = val.Replace("$", string.Empty).Replace("€", string.Empty).Replace("£", string.Empty).Trim();
        if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
        if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.CurrentCulture, out var d2)) return d2;
        return defaultVal;
    }

    private static decimal? ParseNullableDecimal(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        val = val.Replace("$", string.Empty).Replace("€", string.Empty).Replace("£", string.Empty).Trim();
        if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
        if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.CurrentCulture, out var d2)) return d2;
        return null;
    }

    private static bool ParseBool(string? val, bool defaultVal)
    {
        if (string.IsNullOrWhiteSpace(val)) return defaultVal;
        val = val.Trim();
        if (bool.TryParse(val, out var b)) return b;
        if (val == "1" || val.Equals("yes", StringComparison.OrdinalIgnoreCase) || val.Equals("active", StringComparison.OrdinalIgnoreCase) || val.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (val == "0" || val.Equals("no", StringComparison.OrdinalIgnoreCase) || val.Equals("inactive", StringComparison.OrdinalIgnoreCase) || val.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return defaultVal;
    }
}
