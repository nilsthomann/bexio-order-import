using BexioOrderImport.Wpf.Services;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class SettingsAndSecurityTests
{
    private readonly DpapiEncryptionService _encryptionService = new();

    [Fact]
    public void Encrypt_WithValidString_ShouldReturnEncryptedBase64()
    {
        // Arrange
        string clearText = "test-api-token-123456";

        // Act
        string encrypted = _encryptionService.Encrypt(clearText);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);
        Assert.NotEqual(clearText, encrypted);
    }

    [Fact]
    public void Decrypt_WithEncryptedString_ShouldRestoreOriginalString()
    {
        // Arrange
        string clearText = "my-secret-bexio-key";
        string encrypted = _encryptionService.Encrypt(clearText);

        // Act
        string decrypted = _encryptionService.Decrypt(encrypted);

        // Assert
        Assert.Equal(clearText, decrypted);
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ShouldReturnEmptyString()
    {
        // Arrange
        string invalidBase64 = "this-is-not-base64";

        // Act
        string decrypted = _encryptionService.Decrypt(invalidBase64);

        // Assert
        Assert.Equal(string.Empty, decrypted);
    }

    [Fact]
    public void Encrypt_WithNullOrEmpty_ShouldReturnEmptyString()
    {
        _encryptionService.Encrypt(null!).Should().BeEmpty();
        _encryptionService.Encrypt("").Should().BeEmpty();
    }

    [Fact]
    public void Decrypt_WithNullOrEmpty_ShouldReturnEmptyString()
    {
        _encryptionService.Decrypt(null!).Should().BeEmpty();
        _encryptionService.Decrypt("").Should().BeEmpty();
    }
}
