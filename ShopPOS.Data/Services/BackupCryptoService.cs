using System.IO;
using System.Security.Cryptography;
using ShopPOS.Core.Interfaces;

namespace ShopPOS.Data.Services;

/// <summary>
/// Implements AES-256-GCM encryption with PBKDF2-SHA256 key derivation for database backups.
/// File header format: [Version: 1B (0x01)][Salt: 16B][Nonce: 12B][Tag: 16B][Ciphertext: NB].
/// </summary>
public class BackupCryptoService : IBackupCryptoService
{
    public const byte FormatVersion = 0x01;
    public const int SaltSizeBytes = 16;
    public const int NonceSizeBytes = 12;
    public const int TagSizeBytes = 16;
    public const int HeaderSizeBytes = 1 + SaltSizeBytes + NonceSizeBytes + TagSizeBytes; // 45 bytes
    public const int DefaultPbkdf2Iterations = 200_000;

    public byte[] DeriveKey(string password, byte[] salt, int iterations = DefaultPbkdf2Iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32); // 256 bits
    }

    public string ComputeKeyCheck(byte[] derivedKey)
    {
        byte[] hash = SHA256.HashData(derivedKey);
        return Convert.ToBase64String(hash);
    }

    public bool ValidateKeyCheck(string password, string saltBase64, string keyCheckBase64)
    {
        if (string.IsNullOrWhiteSpace(saltBase64) || string.IsNullOrWhiteSpace(keyCheckBase64))
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(saltBase64);
            byte[] key = DeriveKey(password, salt);
            byte[] computedHash = SHA256.HashData(key);
            byte[] expectedHash = Convert.FromBase64String(keyCheckBase64);

            return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    public void EncryptFile(string sourceFilePath, string destinationFilePath, string password)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
        }

        byte[] plaintext = File.ReadAllBytes(sourceFilePath);
        byte[] salt = new byte[SaltSizeBytes];
        byte[] nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        byte[] key = DeriveKey(password, salt);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        using var fs = File.Create(destinationFilePath);
        fs.WriteByte(FormatVersion);
        fs.Write(salt, 0, salt.Length);
        fs.Write(nonce, 0, nonce.Length);
        fs.Write(tag, 0, tag.Length);
        fs.Write(ciphertext, 0, ciphertext.Length);
        fs.Flush();
    }

    public void DecryptFile(string sourceFilePath, string destinationFilePath, string password)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Encrypted backup not found: {sourceFilePath}");
        }

        var fileInfo = new FileInfo(sourceFilePath);
        if (fileInfo.Length < HeaderSizeBytes)
        {
            throw new InvalidDataException("Invalid encrypted backup archive: file is too small.");
        }

        using var fs = File.OpenRead(sourceFilePath);
        int version = fs.ReadByte();
        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported encrypted backup format version: {version}.");
        }

        byte[] salt = new byte[SaltSizeBytes];
        byte[] nonce = new byte[NonceSizeBytes];
        byte[] tag = new byte[TagSizeBytes];

        if (fs.Read(salt, 0, SaltSizeBytes) != SaltSizeBytes ||
            fs.Read(nonce, 0, NonceSizeBytes) != NonceSizeBytes ||
            fs.Read(tag, 0, TagSizeBytes) != TagSizeBytes)
        {
            throw new InvalidDataException("Truncated encrypted backup header.");
        }

        int cipherLength = (int)(fileInfo.Length - HeaderSizeBytes);
        byte[] ciphertext = new byte[cipherLength];
        int readTotal = 0;
        while (readTotal < cipherLength)
        {
            int read = fs.Read(ciphertext, readTotal, cipherLength - readTotal);
            if (read == 0) break;
            readTotal += read;
        }

        byte[] key = DeriveKey(password, salt);
        byte[] plaintext = new byte[cipherLength];

        try
        {
            using var aesGcm = new AesGcm(key, TagSizeBytes);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Incorrect backup password or corrupted backup file.", ex);
        }

        File.WriteAllBytes(destinationFilePath, plaintext);
    }

    public bool IsEncryptedBackup(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        if (filePath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)) return true;

        try
        {
            using var fs = File.OpenRead(filePath);
            if (fs.Length < HeaderSizeBytes) return false;
            int firstByte = fs.ReadByte();
            return firstByte == FormatVersion;
        }
        catch
        {
            return false;
        }
    }
}
