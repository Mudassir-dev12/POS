using System.Collections.Generic;

namespace ShopPOS.Core.DTOs;

/// <summary>
/// Summary result of a bulk delete, cancel, or deactivate operation.
/// </summary>
public class BulkOperationResult
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public List<BulkItemResult> Items { get; set; } = new();
}

/// <summary>
/// Individual item result within a bulk operation.
/// </summary>
public class BulkItemResult
{
    public int Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool CanDeactivateInstead { get; set; }
}

