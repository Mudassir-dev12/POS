namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Result of a single resumable file upload operation to Google Drive.
/// </summary>
public class DriveUploadResult
{
    public bool Success { get; set; }
    public string? FileId { get; set; }
    public string? Md5Checksum { get; set; }
    public long SizeBytes { get; set; }
    public string? SessionUri { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsQuotaExceeded { get; set; }
    public bool IsAuthExpired { get; set; }
    public bool IsRateLimitedOrServer { get; set; }
}

