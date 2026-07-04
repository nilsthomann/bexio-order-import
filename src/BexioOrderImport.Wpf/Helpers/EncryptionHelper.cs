using System;
using System.Security.Cryptography;
using System.Text;

namespace BexioOrderImport.Wpf.Helpers;

public static class EncryptionHelper
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BexioOrderImportSecretEntropy");

    public static Func<byte[], byte[], DataProtectionScope, byte[]>? ProtectHook { get; set; }
    public static Func<byte[], byte[], DataProtectionScope, byte[]>? UnprotectHook { get; set; }

    public static string Encrypt(string clearText)
    {
        if (string.IsNullOrEmpty(clearText)) return string.Empty;
        
        try
        {
            byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);
            byte[] encryptedBytes = ProtectHook != null 
                ? ProtectHook(clearBytes, Entropy, DataProtectionScope.CurrentUser) 
                : ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            // ponytail: fail-safe fallback to clear text if DPAPI is not available (e.g. testing)
            return clearText;
        }
    }

    public static string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return string.Empty;

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] clearBytes = UnprotectHook != null 
                ? UnprotectHook(encryptedBytes, Entropy, DataProtectionScope.CurrentUser) 
                : ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch
        {
            // ponytail: if decryption fails, return empty
            return string.Empty;
        }
    }
}
