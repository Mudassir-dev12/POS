namespace ShopPOS.Core.DTOs.Backup;

/// <summary>
/// Status and health summary of the Google Drive integration.
/// </summary>
public class DriveStatusSummaryDto
{
    public bool IsConfiguredInBuild { get; set; }
    public bool IsConnected { get; set; }
    public string? AccountEmail { get; set; }
    public bool IsDriveEnabled { get; set; }
    public int PendingUploadCount { get; set; }
    public bool IsWaitingForInternet { get; set; }
    public bool ReconnectNeeded { get; set; }
    public bool IsDriveFull { get; set; }
    public DateTime? LastUploadAt { get; set; }
    public string? LastUploadFormatted => LastUploadAt?.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
    public string? LastError { get; set; }
    public string? FolderId { get; set; }
    public string? SubFolderId { get; set; }
    public string? FolderWebUrl => !string.IsNullOrWhiteSpace(SubFolderId)
        ? $"https://drive.google.com/drive/folders/{SubFolderId}"
        : (!string.IsNullOrWhiteSpace(FolderId) ? $"https://drive.google.com/drive/folders/{FolderId}" : null);
}

