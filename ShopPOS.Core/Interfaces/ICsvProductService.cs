using ShopPOS.Core.DTOs;
using ShopPOS.Core.Models;

namespace ShopPOS.Core.Interfaces;

public class CsvRowError
{
    public int RowNumber { get; set; }
    public string Error { get; set; } = string.Empty;
}

public class CsvPreviewResult
{
    public List<string> Headers { get; set; } = new();
    public List<Dictionary<string, string>> PreviewRows { get; set; } = new();
    public int TotalRowsFound { get; set; }
    public string FileTypeInfo { get; set; } = string.Empty;
    public string? DetectedNameColumn { get; set; }
}

public class CsvValidationReport
{
    public int TotalRows { get; set; }
    public int ValidRowCount { get; set; }
    public int ErrorRowCount { get; set; }
    public List<CsvRowError> Errors { get; set; } = new();
}

public class CsvImportOptions
{
    public bool UpdateExisting { get; set; } = true;
    public int? UserId { get; set; }
    public string? UserRole { get; set; }
}

public class CsvImportResult
{
    public int ProcessedCount { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<CsvRowError> Errors { get; set; } = new();
}

public interface ICsvProductService
{
    Task<byte[]> ExportProductsToCsvAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
    Task<byte[]> ExportProductsToExcelAsync(ProductSearchQuery query, CancellationToken cancellationToken = default);
    byte[] GetSampleCsvTemplate();
    byte[] GetSampleExcelTemplate();
    Task<Result<CsvPreviewResult>> PreviewCsvAsync(Stream stream, CancellationToken cancellationToken = default);
    Task<Result<CsvValidationReport>> ValidateCsvAsync(Stream stream, CancellationToken cancellationToken = default);
    Task<Result<CsvImportResult>> ImportCsvAsync(Stream stream, CsvImportOptions options, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
}
