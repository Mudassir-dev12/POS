namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Data transfer object representing a backup archive file discovered on Google Drive.
/// </summary>
public class DriveBackupItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeFormatted => FormatBytes(SizeBytes);
    public DateTime? CreatedTime { get; set; }
    public string CreatedTimeFormatted => CreatedTime?.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt") ?? "-";
    public string? Md5Checksum { get; set; }
    public bool ExistsLocally { get; set; }
    public int? LocalBackupRecordId { get; set; }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
        return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }
}

