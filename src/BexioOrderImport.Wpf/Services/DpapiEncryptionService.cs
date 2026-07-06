using System;
using System.Security.Cryptography;
using System.Text;

namespace BexioOrderImport.Wpf.Services;

public class DpapiEncryptionService : IEncryptionService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BexioOrderImportSecretEntropy");

    public string Encrypt(string clearText)
    {
        if (string.IsNullOrEmpty(clearText)) return string.Empty;
        
        try
        {
            byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);
            byte[] encryptedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            // Fail-safe fallback to clear text if DPAPI is not available
            return clearText;
        }
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return string.Empty;

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] clearBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch
        {
            // If decryption fails, return empty
            return string.Empty;
        }
    }
}
