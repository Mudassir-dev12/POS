using System.Collections.Generic;

namespace ShopPOS.Core.DTOs.DataTools;

public class TableCountDto
{
    public string TableName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DataCountsDto
{
    public int SalesCount { get; set; }
    public int SaleItemsCount { get; set; }
    public int SalePaymentsCount { get; set; }
    public int SaleReturnsCount { get; set; }
    public int PurchasesCount { get; set; }
    public int PurchaseItemsCount { get; set; }
    public int SupplierPaymentsCount { get; set; }
    public int PurchaseReturnsCount { get; set; }
    public int StockAdjustmentsCount { get; set; }
    public int StockMovementsCount { get; set; }
    public int ExpensesCount { get; set; }
    public int AuditLogsCount { get; set; }

    public int ProductsCount { get; set; }
    public int CategoriesCount { get; set; }
    public int BrandsCount { get; set; }
    public int UnitsCount { get; set; }
    public int SuppliersCount { get; set; }

    public int TotalTransactionsCount => SalesCount + SaleReturnsCount + PurchasesCount + SupplierPaymentsCount + PurchaseReturnsCount + StockAdjustmentsCount + ExpensesCount;

    public List<TableCountDto> GetTransactionCounts()
    {
        return new List<TableCountDto>
        {
            new() { TableName = "Sales", Count = SalesCount },
            new() { TableName = "Sale Items", Count = SaleItemsCount },
            new() { TableName = "Sale Payments", Count = SalePaymentsCount },
            new() { TableName = "Sale Returns", Count = SaleReturnsCount },
            new() { TableName = "Purchases", Count = PurchasesCount },
            new() { TableName = "Purchase Items", Count = PurchaseItemsCount },
            new() { TableName = "Supplier Payments", Count = SupplierPaymentsCount },
            new() { TableName = "Purchase Returns", Count = PurchaseReturnsCount },
            new() { TableName = "Stock Adjustments", Count = StockAdjustmentsCount },
            new() { TableName = "Stock Movements", Count = StockMovementsCount },
            new() { TableName = "Expenses", Count = ExpensesCount },
            new() { TableName = "Audit Logs", Count = AuditLogsCount }
        };
    }

    public List<TableCountDto> GetMasterDataCounts()
    {
        return new List<TableCountDto>
        {
            new() { TableName = "Products", Count = ProductsCount },
            new() { TableName = "Categories", Count = CategoriesCount },
            new() { TableName = "Brands", Count = BrandsCount },
            new() { TableName = "Units", Count = UnitsCount },
            new() { TableName = "Suppliers", Count = SuppliersCount }
        };
    }
}

public class DataToolResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? BackupFilePath { get; set; }
    public List<TableCountDto> DeletedRows { get; set; } = new();
    public List<TableCountDto> KeptRows { get; set; } = new();
    public string? Error { get; set; }
}

public class DemoBarcodeItemDto
{
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal StockQuantity { get; set; }
}

public class DemoDataResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? BackupFilePath { get; set; }
    public int CategoriesCreated { get; set; }
    public int BrandsCreated { get; set; }
    public int SuppliersCreated { get; set; }
    public int ProductsCreated { get; set; }
    public int PurchasesCreated { get; set; }
    public int SalesCreated { get; set; }
    public int ReturnsCreated { get; set; }
    public int ExpensesCreated { get; set; }
    public List<DemoBarcodeItemDto> SampleBarcodes { get; set; } = new();
    public string? Error { get; set; }
}

