using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using ShopPOS.Data.Services;
using Xunit;

namespace ShopPOS.Tests;

public class BackupCryptoServiceTests : IDisposable
{
    private readonly BackupCryptoService _crypto = new();
    private readonly string _tempDir;

    public BackupCryptoServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CryptoTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void EncryptAndDecrypt_RoundTrip_MatchesOriginalData()
    {
        string plainFile = Path.Combine(_tempDir, "original.zip");
        string encFile = Path.Combine(_tempDir, "encrypted.zip.enc");
        string decFile = Path.Combine(_tempDir, "decrypted.zip");

        byte[] payload = Encoding.UTF8.GetBytes("This is test SQLite database and manifest content for backup testing.");
        File.WriteAllBytes(plainFile, payload);

        string password = "TestPassword123!";

        // Encrypt
        _crypto.EncryptFile(plainFile, encFile, password);

        File.Exists(encFile).Should().BeTrue();
        new FileInfo(encFile).Length.Should().BeGreaterThan(payload.Length);
        _crypto.IsEncryptedBackup(encFile).Should().BeTrue();

        // Decrypt
        _crypto.DecryptFile(encFile, decFile, password);

        File.Exists(decFile).Should().BeTrue();
        byte[] decryptedBytes = File.ReadAllBytes(decFile);
        decryptedBytes.Should().Equal(payload);
    }

    [Fact]
    public void Decrypt_WrongPassword_ThrowsExceptionWithoutCrash()
    {
        string plainFile = Path.Combine(_tempDir, "original.zip");
        string encFile = Path.Combine(_tempDir, "encrypted.zip.enc");
        string decFile = Path.Combine(_tempDir, "decrypted.zip");

        File.WriteAllText(plainFile, "Sensitive backup content");
        _crypto.EncryptFile(plainFile, encFile, "CorrectPassword123!");

        Action act = () => _crypto.DecryptFile(encFile, decFile, "WrongPassword999!");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*password*");
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_FailsGcmTagValidation()
    {
        string plainFile = Path.Combine(_tempDir, "original.zip");
        string encFile = Path.Combine(_tempDir, "encrypted.zip.enc");
        string decFile = Path.Combine(_tempDir, "decrypted.zip");

        File.WriteAllText(plainFile, "Payload before tampering");
        _crypto.EncryptFile(plainFile, encFile, "SecretKey123!");

        // Corrupt a byte in the encrypted file (past header)
        byte[] encBytes = File.ReadAllBytes(encFile);
        encBytes[^1] ^= 0xFF; // Flip bits in ciphertext
        File.WriteAllBytes(encFile, encBytes);

        Action act = () => _crypto.DecryptFile(encFile, decFile, "SecretKey123!");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void KeyCheck_ValidatesCorrectPassword_AndRejectsInvalid()
    {
        string password = "OwnerMasterPassword456";
        byte[] salt = new byte[BackupCryptoService.SaltSizeBytes];
        RandomNumberGenerator.Fill(salt);

        byte[] key = _crypto.DeriveKey(password, salt);
        string keyCheck = _crypto.ComputeKeyCheck(key);
        string saltBase64 = Convert.ToBase64String(salt);

        _crypto.ValidateKeyCheck(password, saltBase64, keyCheck).Should().BeTrue();
        _crypto.ValidateKeyCheck("WrongPassword", saltBase64, keyCheck).Should().BeFalse();
        _crypto.ValidateKeyCheck("", saltBase64, keyCheck).Should().BeFalse();
    }

    [Fact]
    public void FormatVersion_IsCheckedOnDecrypt()
    {
        string dummyEnc = Path.Combine(_tempDir, "badversion.enc");
        byte[] badHeader = new byte[60];
        badHeader[0] = 0x99; // Unknown version
        File.WriteAllBytes(dummyEnc, badHeader);

        string decFile = Path.Combine(_tempDir, "out.zip");
        Action act = () => _crypto.DecryptFile(dummyEnc, decFile, "AnyPassword");
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Unsupported*");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}

