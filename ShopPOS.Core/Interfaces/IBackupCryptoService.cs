namespace ShopPOS.Core.Interfaces;

/// <summary>
/// Cryptographic service providing AES-256-GCM encryption and PBKDF2-SHA256 key derivation for backups.
/// </summary>
public interface IBackupCryptoService
{
    /// <summary>
    /// Derives a 256-bit symmetric key from a password and salt using PBKDF2-SHA256 (200,000 iterations).
    /// </summary>
    byte[] DeriveKey(string password, byte[] salt, int iterations = 200_000);

    /// <summary>
    /// Computes a validation key-check hash from the derived key (used to verify password without storing it).
    /// </summary>
    string ComputeKeyCheck(byte[] derivedKey);

    /// <summary>
    /// Validates whether the supplied password matches the stored salt and key check hash.
    /// </summary>
    bool ValidateKeyCheck(string password, string saltBase64, string keyCheckBase64);

    /// <summary>
    /// Encrypts a source file into a destination file using AES-256-GCM.
    /// The destination file format is: [Version: 1B][Salt: 16B][Nonce: 12B][Tag: 16B][Ciphertext].
    /// </summary>
    void EncryptFile(string sourceFilePath, string destinationFilePath, string password);

    /// <summary>
    /// Decrypts an AES-256-GCM encrypted backup file to a destination file.
    /// Throws CryptographicException or InvalidOperationException if the password is wrong or ciphertext tampered.
    /// </summary>
    void DecryptFile(string sourceFilePath, string destinationFilePath, string password);

    /// <summary>
    /// Checks whether the specified file has the encrypted backup header marker.
    /// </summary>
    bool IsEncryptedBackup(string filePath);
}

