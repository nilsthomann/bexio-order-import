using Xunit;
using BexioOrderImport.Wpf.Helpers;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class SettingsAndSecurityTests
{
    [Fact]
    public void Encrypt_WithValidString_ShouldReturnEncryptedBase64()
    {
        // Arrange
        string clearText = "test-api-token-123456";

        // Act
        string encrypted = EncryptionHelper.Encrypt(clearText);

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
        string encrypted = EncryptionHelper.Encrypt(clearText);

        // Act
        string decrypted = EncryptionHelper.Decrypt(encrypted);

        // Assert
        Assert.Equal(clearText, decrypted);
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ShouldReturnEmptyString()
    {
        // Arrange
        string invalidBase64 = "this-is-not-base64";

        // Act
        string decrypted = EncryptionHelper.Decrypt(invalidBase64);

        // Assert
        Assert.Equal(string.Empty, decrypted);
    }

    [Fact]
    public void Encrypt_WithNullOrEmpty_ShouldReturnEmptyString()
    {
        EncryptionHelper.Encrypt(null!).Should().BeEmpty();
        EncryptionHelper.Encrypt("").Should().BeEmpty();
    }

    [Fact]
    public void Decrypt_WithNullOrEmpty_ShouldReturnEmptyString()
    {
        EncryptionHelper.Decrypt(null!).Should().BeEmpty();
        EncryptionHelper.Decrypt("").Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_WhenExceptionThrown_ShouldReturnClearText()
    {
        // Arrange
        EncryptionHelper.ProtectHook = (bytes, entropy, scope) => throw new System.Security.Cryptography.CryptographicException("Mock error");

        try
        {
            // Act
            string clearText = "some-text";
            string result = EncryptionHelper.Encrypt(clearText);

            // Assert
            result.Should().Be(clearText);
        }
        finally
        {
            EncryptionHelper.ProtectHook = null;
        }
    }

    [Fact]
    public void Decrypt_WhenExceptionThrown_ShouldReturnEmptyString()
    {
        // Arrange
        EncryptionHelper.UnprotectHook = (bytes, entropy, scope) => throw new System.Security.Cryptography.CryptographicException("Mock error");

        try
        {
            // Act
            string result = EncryptionHelper.Decrypt("c29tZS10ZXh0"); // Base64 for "some-text"

            // Assert
            result.Should().BeEmpty();
        }
        finally
        {
            EncryptionHelper.UnprotectHook = null;
        }
    }
}
