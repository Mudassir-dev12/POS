using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Logging;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Implementation of IDriveClient wrapping Google Drive v3 and Google OAuth APIs.
/// </summary>
public class GoogleDriveClient : IDriveClient
{
    private readonly IGoogleDriveConfigService _configService;
    private readonly IDriveTokenStore _tokenStore;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleDriveClient> _logger;
    private DriveService? _driveService;
    private readonly SemaphoreSlim _clientLock = new(1, 1);

    public GoogleDriveClient(
        IGoogleDriveConfigService configService,
        IDriveTokenStore tokenStore,
        HttpClient? httpClient = null,
        ILogger<GoogleDriveClient>? logger = null)
    {
        _configService = configService;
        _tokenStore = tokenStore;
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleDriveClient>.Instance;
    }

    public async Task<string> AuthorizeAsync(Func<string, Task> onBrowserOpen, CancellationToken cancellationToken = default)
    {
        var config = _configService.GetConfig();
        if (!config.IsConfigured)
        {
            throw new InvalidOperationException("Google Drive OAuth client ID and secret are not configured.");
        }

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = config.ClientId,
                ClientSecret = config.ClientSecret
            },
            Scopes = new[] { DriveService.Scope.DriveFile },
            DataStore = new DpapiDriveDataStore(_tokenStore)
        });

        var receiver = new LoopbackCodeReceiver(onBrowserOpen);

        // 3-minute timeout for user to complete browser sign-in
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(3));

        try
        {
            var credential = await new AuthorizationCodeInstalledApp(flow, receiver).AuthorizeAsync(
                "user",
                cts.Token);

            if (credential == null)
            {
                throw new InvalidOperationException("Failed to acquire Google Drive credentials.");
            }

            // Invalidate cached drive service
            await _clientLock.WaitAsync(cancellationToken);
            try
            {
                _driveService?.Dispose();
                _driveService = null;
            }
            finally
            {
                _clientLock.Release();
            }

            string? email = await GetAccountEmailAsync(cancellationToken);
            return email ?? "Connected";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && cts.IsCancellationRequested)
        {
            throw new TimeoutException("Google Drive authorization timed out after 3 minutes.");
        }
    }

    public async Task RevokeTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string? tokenJson = await _tokenStore.LoadTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(tokenJson))
            {
                string? tokenToRevoke = null;
                try
                {
                    using var doc = JsonDocument.Parse(tokenJson);
                    if (doc.RootElement.TryGetProperty("RefreshToken", out var rf) ||
                        doc.RootElement.TryGetProperty("refresh_token", out rf))
                    {
                        tokenToRevoke = rf.GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("AccessToken", out var ac) ||
                             doc.RootElement.TryGetProperty("access_token", out ac))
                    {
                        tokenToRevoke = ac.GetString();
                    }
                }
                catch
                {
                    // Ignore parse error
                }

                if (!string.IsNullOrWhiteSpace(tokenToRevoke))
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/revoke");
                    req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "token", tokenToRevoke }
                    });

                    using var res = await _httpClient.SendAsync(req, cancellationToken);
                    _logger.LogInformation("Google token revocation status: {StatusCode}", res.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not revoke Google OAuth token online; deleting local credentials anyway.");
        }
        finally
        {
            await _tokenStore.DeleteTokenAsync(cancellationToken);

            await _clientLock.WaitAsync(cancellationToken);
            try
            {
                _driveService?.Dispose();
                _driveService = null;
            }
            finally
            {
                _clientLock.Release();
            }
        }
    }

    public async Task<string?> GetAccountEmailAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var driveService = await GetDriveServiceAsync(cancellationToken);
            if (driveService == null) return null;

            var aboutReq = driveService.About.Get();
            aboutReq.Fields = "user(emailAddress)";
            var about = await aboutReq.ExecuteAsync(cancellationToken);

            return about?.User?.EmailAddress;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve Google user email from Drive API.");
            return null;
        }
    }

    public async Task<string> GetOrCreateFolderAsync(string folderName, string? parentFolderId = null, CancellationToken cancellationToken = default)
    {
        var driveService = await GetDriveServiceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Not authenticated with Google Drive.");

        string query = $"mimeType = 'application/vnd.google-apps.folder' and name = '{folderName.Replace("'", "\\'")}' and trashed = false";
        if (!string.IsNullOrWhiteSpace(parentFolderId))
        {
            query += $" and '{parentFolderId}' in parents";
        }

        var listReq = driveService.Files.List();
        listReq.Q = query;
        listReq.Fields = "files(id, name)";
        listReq.Spaces = "drive";

        var result = await listReq.ExecuteAsync(cancellationToken);
        if (result.Files != null && result.Files.Count > 0)
        {
            return result.Files[0].Id;
        }

        // Folder not found; create it
        var folderMeta = new Google.Apis.Drive.v3.Data.File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = !string.IsNullOrWhiteSpace(parentFolderId) ? new List<string> { parentFolderId } : null
        };

        var createReq = driveService.Files.Create(folderMeta);
        createReq.Fields = "id";
        var created = await createReq.ExecuteAsync(cancellationToken);

        _logger.LogInformation("Created Google Drive folder '{Name}' with ID '{Id}'", folderName, created.Id);
        return created.Id;
    }

    public async Task<IReadOnlyList<DriveBackupItemDto>> ListBackupsInFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var driveService = await GetDriveServiceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Not authenticated with Google Drive.");

        var listReq = driveService.Files.List();
        listReq.Q = $"'{folderId}' in parents and trashed = false";
        listReq.Fields = "files(id, name, size, createdTime, md5Checksum)";
        listReq.PageSize = 1000;

        var result = await listReq.ExecuteAsync(cancellationToken);
        var list = new List<DriveBackupItemDto>();

        if (result.Files != null)
        {
            foreach (var f in result.Files)
            {
                list.Add(new DriveBackupItemDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    SizeBytes = f.Size ?? 0,
                    CreatedTime = f.CreatedTimeDateTimeOffset?.UtcDateTime,
                    Md5Checksum = f.Md5Checksum
                });
            }
        }

        return list;
    }

    public async Task<DriveUploadResult> UploadFileResumableAsync(
        string filePath,
        string folderId,
        string? sessionUri = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new DriveUploadResult();
        try
        {
            var driveService = await GetDriveServiceAsync(cancellationToken)
                ?? throw new InvalidOperationException("Not authenticated with Google Drive.");

            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Local backup file '{filePath}' not found.";
                return result;
            }

            var fileInfo = new FileInfo(filePath);
            result.SizeBytes = fileInfo.Length;

            var fileMeta = new Google.Apis.Drive.v3.Data.File
            {
                Name = Path.GetFileName(filePath),
                Parents = new List<string> { folderId }
            };

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var uploadReq = driveService.Files.Create(fileMeta, stream, "application/octet-stream");
            uploadReq.ChunkSize = 5 * 1024 * 1024; // 5 MB chunks (multiples of 256 KB)
            uploadReq.Fields = "id, name, size, md5Checksum";

            uploadReq.ProgressChanged += uploadProgress =>
            {
                if (fileInfo.Length > 0 && uploadProgress.BytesSent > 0)
                {
                    int pct = (int)Math.Min(100, Math.Max(0, (uploadProgress.BytesSent * 100) / fileInfo.Length));
                    progress?.Report(pct);
                }

                if (uploadProgress.Status == UploadStatus.Failed && uploadProgress.Exception != null)
                {
                    _logger.LogWarning(uploadProgress.Exception, "Resumable upload progress reported failure.");
                }
            };

            uploadReq.UploadSessionData += sessionData =>
            {
                if (sessionData.UploadUri != null)
                {
                    result.SessionUri = sessionData.UploadUri.ToString();
                }
            };

            Google.Apis.Drive.v3.Data.File? uploadedFile = null;
            uploadReq.ResponseReceived += file =>
            {
                uploadedFile = file;
            };

            IUploadProgress finalProgress;
            if (!string.IsNullOrWhiteSpace(sessionUri) && Uri.TryCreate(sessionUri, UriKind.Absolute, out var parsedSessionUri))
            {
                try
                {
                    finalProgress = await uploadReq.ResumeAsync(parsedSessionUri, cancellationToken);
                }
                catch
                {
                    // If session expired or invalid, restart upload from beginning
                    stream.Position = 0;
                    finalProgress = await uploadReq.UploadAsync(cancellationToken);
                }
            }
            else
            {
                finalProgress = await uploadReq.UploadAsync(cancellationToken);
            }

            if (finalProgress.Status == UploadStatus.Completed && uploadedFile != null)
            {
                result.Success = true;
                result.FileId = uploadedFile.Id;
                result.Md5Checksum = uploadedFile.Md5Checksum;
                result.SizeBytes = uploadedFile.Size ?? fileInfo.Length;
                progress?.Report(100);
                return result;
            }

            // Upload failed or threw
            if (finalProgress.Exception != null)
            {
                ClassifyException(finalProgress.Exception, result);
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = $"Upload ended with status {finalProgress.Status}.";
            }
        }
        catch (Exception ex)
        {
            ClassifyException(ex, result);
        }

        return result;
    }

    public async Task DownloadFileAsync(
        string fileId,
        string destinationFilePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var driveService = await GetDriveServiceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Not authenticated with Google Drive.");

        var getReq = driveService.Files.Get(fileId);
        getReq.Fields = "id, size, md5Checksum";
        var meta = await getReq.ExecuteAsync(cancellationToken);

        long totalSize = meta.Size ?? 0;

        string tempPath = destinationFilePath + ".tmp";
        await using (var outputStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var downloadReq = driveService.Files.Get(fileId);
            downloadReq.MediaDownloader.ProgressChanged += dlProgress =>
            {
                if (totalSize > 0 && dlProgress.BytesDownloaded > 0)
                {
                    int pct = (int)Math.Min(100, Math.Max(0, (dlProgress.BytesDownloaded * 100) / totalSize));
                    progress?.Report(pct);
                }
            };

            var status = await downloadReq.DownloadAsync(outputStream, cancellationToken);
            if (status.Status != DownloadStatus.Completed)
            {
                if (status.Exception != null)
                    throw status.Exception;
                throw new InvalidOperationException($"File download ended with status {status.Status}.");
            }
        }

        if (File.Exists(destinationFilePath))
        {
            File.Delete(destinationFilePath);
        }
        File.Move(tempPath, destinationFilePath);
        progress?.Report(100);
    }

    public async Task DeleteFileAsync(string fileId, bool moveToTrash = false, CancellationToken cancellationToken = default)
    {
        var driveService = await GetDriveServiceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Not authenticated with Google Drive.");

        if (moveToTrash)
        {
            var updateMeta = new Google.Apis.Drive.v3.Data.File { Trashed = true };
            await driveService.Files.Update(updateMeta, fileId).ExecuteAsync(cancellationToken);
        }
        else
        {
            await driveService.Files.Delete(fileId).ExecuteAsync(cancellationToken);
        }
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/generate_204");
            using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            return res.StatusCode == HttpStatusCode.NoContent || res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<DriveService?> GetDriveServiceAsync(CancellationToken cancellationToken)
    {
        if (_driveService != null) return _driveService;

        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            if (_driveService != null) return _driveService;

            var config = _configService.GetConfig();
            if (!config.IsConfigured || !_tokenStore.HasToken())
            {
                return null;
            }

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = config.ClientId,
                    ClientSecret = config.ClientSecret
                },
                Scopes = new[] { DriveService.Scope.DriveFile },
                DataStore = new DpapiDriveDataStore(_tokenStore)
            });

            var credential = await flow.LoadTokenAsync("user", cancellationToken);
            if (credential == null)
            {
                return null;
            }

            var userCredential = new UserCredential(flow, "user", credential);

            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = userCredential,
                ApplicationName = "ShopPOS"
            });

            return _driveService;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private void ClassifyException(Exception ex, DriveUploadResult result)
    {
        result.Success = false;
        result.ErrorMessage = ex.Message;

        if (ex is TokenResponseException || ex.Message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
        {
            result.IsAuthExpired = true;
            return;
        }

        if (ex is GoogleApiException gEx)
        {
            int code = (int)gEx.HttpStatusCode;
            if (code == 401)
            {
                result.IsAuthExpired = true;
            }
            else if (code == 403)
            {
                string msg = gEx.Message.ToLowerInvariant();
                if (msg.Contains("storagequotaexceeded") || msg.Contains("quotaexceeded") || msg.Contains("quota"))
                {
                    result.IsQuotaExceeded = true;
                }
                else
                {
                    result.IsRateLimitedOrServer = true;
                }
            }
            else if (code >= 500 || code == 429)
            {
                result.IsRateLimitedOrServer = true;
            }
        }
        else if (ex is HttpRequestException)
        {
            result.IsRateLimitedOrServer = true;
        }
    }
}
