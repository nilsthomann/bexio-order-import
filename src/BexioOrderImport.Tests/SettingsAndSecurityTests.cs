using Xunit;
using BexioOrderImport.Wpf.Helpers;

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
}
