namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Result of a backup creation operation.
/// </summary>
public class BackupResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public BackupRecordDto? Record { get; set; }
    public string? WarningMessage { get; set; }
}

