namespace ShopPOS.Core.Enums;

/// <summary>
/// Status of cloud synchronization to a private GitHub repository for a backup archive.
/// </summary>
public enum GitHubUploadStatus
{
    NotConfigured = 0,
    Pending = 1,
    Uploading = 2,
    Uploaded = 3,
    Failed = 4,
    Skipped = 5
}

