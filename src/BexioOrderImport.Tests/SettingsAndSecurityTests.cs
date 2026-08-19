using BexioOrderImport.Wpf.Services;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class SettingsAndSecurityTests
{
    private readonly DpapiEncryptionService _encryptionService = new();

    [Test]
    public void Encrypt_WithValidString_ShouldReturnEncryptedBase64()
    {
        // Arrange
        string clearText = "test-api-token-123456";

        // Act
        string encrypted = _encryptionService.Encrypt(clearText);

        // Assert
        encrypted.Should().NotBeNull();
        encrypted.Should().NotBeEmpty();
        encrypted.Should().NotBe(clearText);
    }

    [Test]
    public void Decrypt_WithEncryptedString_ShouldRestoreOriginalString()
    {
        // Arrange
        string clearText = "my-secret-bexio-key";
        string encrypted = _encryptionService.Encrypt(clearText);

        // Act
        string decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(clearText);
    }

    [Test]
    public void Decrypt_WithInvalidBase64_ShouldReturnEmptyString()
    {
        // Arrange
        string invalidBase64 = "this-is-not-base64";

        // Act
        string decrypted = _encryptionService.Decrypt(invalidBase64);

        // Assert
        decrypted.Should().Be(string.Empty);
    }

    [Test]
    public void Encrypt_WithNullOrEmpty_ShouldReturnEmptyString()
    {
        _encryptionService.Encrypt(null!).Should().BeEmpty();
        _encryptionService.Encrypt("").Should().BeEmpty();
    }

    [Test]
    public void Decrypt_WithNullOrEmpty_ShouldReturnEmptyString()
    {
        _encryptionService.Decrypt(null!).Should().BeEmpty();
        _encryptionService.Decrypt("").Should().BeEmpty();
    }

    [Test]
    public void Decrypt_WithCorruptedCiphertextBase64_ShouldCatchExceptionAndReturnEmptyString()
    {
        // Arrange: Valid base64 encoding of non-DPAPI bytes, which causes ProtectedData.Unprotect to throw CryptographicException
        string corruptedBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

        // Act
        string decrypted = _encryptionService.Decrypt(corruptedBase64);

        // Assert
        decrypted.Should().Be(string.Empty);
    }
}
