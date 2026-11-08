using ShopPOS.Core.DTOs.Backup;

namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Low-level abstraction wrapping Google Drive v3 API and Google Auth.
/// Enables 100% testability with fakes without internet or real Google calls.
/// </summary>
public interface IDriveClient
{
    /// <summary>
    /// Executes the interactive OAuth authorization code flow with PKCE on loopback.
    /// Returns the authenticated user email, or throws on user cancellation/error.
    /// </summary>
    Task<string> AuthorizeAsync(Func<string, Task> onBrowserOpen, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes authorization with Google and clears cached credentials.
    /// </summary>
    Task RevokeTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the currently authenticated Google user email, or null if unauthenticated.
    /// </summary>
    Task<string?> GetAccountEmailAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds or creates a folder with the specified name under the parent (or root).
    /// </summary>
    Task<string> GetOrCreateFolderAsync(string folderName, string? parentFolderId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all backup files in the specified Drive folder.
    /// </summary>
    Task<IReadOnlyList<DriveBackupItemDto>> ListBackupsInFolderAsync(string folderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a resumable upload of a backup archive with 5MB chunks.
    /// </summary>
    Task<DriveUploadResult> UploadFileResumableAsync(
        string filePath,
        string folderId,
        string? sessionUri = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a backup file from Google Drive to a local destination file.
    /// </summary>
    Task DownloadFileAsync(
        string fileId,
        string destinationFilePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file on Google Drive (or moves to trash if specified).
    /// </summary>
    Task DeleteFileAsync(string fileId, bool moveToTrash = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Probes Google API endpoint with a 5-second timeout to check real HTTPS internet connectivity.
    /// </summary>
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}

