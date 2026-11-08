using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ShopPOS.Core.DTOs.Backup;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Tests;

/// <summary>
/// Fully in-memory, zero-network fake for IDriveClient.
/// Enables comprehensive testing of all Google Drive scenarios without internet or real Google API calls.
/// </summary>
public class FakeDriveClient : IDriveClient
{
    public class FakeDriveFile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string? ParentFolderId { get; set; }
        public long SizeBytes { get; set; }
        public string Md5Checksum { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public byte[]? ContentBytes { get; set; }
        public bool IsTrashed { get; set; }
    }

    public List<FakeDriveFile> Files { get; } = new();
    public List<string> CreatedFolderNames { get; } = new();
    public List<string> DeletedFileIds { get; } = new();
    public List<string> UploadedFilePaths { get; } = new();

    public string? AuthenticatedEmail { get; set; } = "testshop@gmail.com";
    public bool IsOnline { get; set; } = true;
    public bool SimulateAuthExpired { get; set; }
    public bool SimulateQuotaExceeded { get; set; }
    public bool SimulateRateLimited { get; set; }
    public bool SimulateMd5Mismatch { get; set; }
    public bool IsTokenRevoked { get; set; }

    public Task<string> AuthorizeAsync(Func<string, Task> onBrowserOpen, CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
        {
            throw new HttpRequestException("No internet connection.");
        }

        onBrowserOpen?.Invoke("http://127.0.0.1:12345/authorize/?code=test_code");
        return Task.FromResult(AuthenticatedEmail ?? "testshop@gmail.com");
    }

    public Task RevokeTokenAsync(CancellationToken cancellationToken = default)
    {
        IsTokenRevoked = true;
        AuthenticatedEmail = null;
        return Task.CompletedTask;
    }

    public Task<string?> GetAccountEmailAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AuthenticatedEmail);
    }

    public Task<string> GetOrCreateFolderAsync(string folderName, string? parentFolderId = null, CancellationToken cancellationToken = default)
    {
        var existing = Files.FirstOrDefault(f => f.Name == folderName && f.ParentFolderId == parentFolderId && !f.IsTrashed);
        if (existing != null)
        {
            return Task.FromResult(existing.Id);
        }

        var folder = new FakeDriveFile
        {
            Id = $"folder_{Guid.NewGuid():N}",
            Name = folderName,
            ParentFolderId = parentFolderId
        };
        Files.Add(folder);
        CreatedFolderNames.Add(folderName);
        return Task.FromResult(folder.Id);
    }

    public Task<IReadOnlyList<DriveBackupItemDto>> ListBackupsInFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        var items = Files
            .Where(f => f.ParentFolderId == folderId && !f.IsTrashed)
            .Select(f => new DriveBackupItemDto
            {
                Id = f.Id,
                Name = f.Name,
                SizeBytes = f.SizeBytes,
                CreatedTime = f.CreatedTime,
                Md5Checksum = f.Md5Checksum
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<DriveBackupItemDto>>(items);
    }

    public async Task<DriveUploadResult> UploadFileResumableAsync(
        string filePath,
        string folderId,
        string? sessionUri = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        UploadedFilePaths.Add(filePath);

        if (SimulateAuthExpired)
        {
            return new DriveUploadResult
            {
                Success = false,
                IsAuthExpired = true,
                ErrorMessage = "Invalid grant / OAuth token expired."
            };
        }

        if (SimulateQuotaExceeded)
        {
            return new DriveUploadResult
            {
                Success = false,
                IsQuotaExceeded = true,
                ErrorMessage = "User storage quota exceeded."
            };
        }

        if (SimulateRateLimited)
        {
            return new DriveUploadResult
            {
                Success = false,
                IsRateLimitedOrServer = true,
                ErrorMessage = "Rate limit exceeded. Retry later."
            };
        }

        if (!File.Exists(filePath))
        {
            return new DriveUploadResult
            {
                Success = false,
                ErrorMessage = "Local file not found."
            };
        }

        byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        string md5;
        using (var md5Hasher = MD5.Create())
        {
            md5 = Convert.ToHexString(md5Hasher.ComputeHash(bytes));
        }

        if (SimulateMd5Mismatch)
        {
            md5 = "CORRUPTED_MD5_HASH";
        }

        progress?.Report(50);

        var file = new FakeDriveFile
        {
            Id = $"file_{Guid.NewGuid():N}",
            Name = Path.GetFileName(filePath),
            ParentFolderId = folderId,
            SizeBytes = bytes.Length,
            Md5Checksum = md5,
            CreatedTime = DateTime.UtcNow,
            ContentBytes = bytes
        };
        Files.Add(file);

        progress?.Report(100);

        return new DriveUploadResult
        {
            Success = true,
            FileId = file.Id,
            Md5Checksum = md5,
            SizeBytes = bytes.Length,
            SessionUri = "https://www.googleapis.com/upload/drive/v3/files?upload_id=mock_session"
        };
    }

    public async Task DownloadFileAsync(
        string fileId,
        string destinationFilePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var file = Files.FirstOrDefault(f => f.Id == fileId && !f.IsTrashed);
        if (file == null)
        {
            throw new FileNotFoundException($"Drive file ID '{fileId}' not found.");
        }

        progress?.Report(50);
        byte[] content = file.ContentBytes ?? new byte[file.SizeBytes];
        await File.WriteAllBytesAsync(destinationFilePath, content, cancellationToken);
        progress?.Report(100);
    }

    public Task DeleteFileAsync(string fileId, bool moveToTrash = false, CancellationToken cancellationToken = default)
    {
        var file = Files.FirstOrDefault(f => f.Id == fileId);
        if (file != null)
        {
            if (moveToTrash)
            {
                file.IsTrashed = true;
            }
            else
            {
                Files.Remove(file);
            }
            DeletedFileIds.Add(fileId);
        }
        return Task.CompletedTask;
    }

    public Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsOnline);
    }
}
