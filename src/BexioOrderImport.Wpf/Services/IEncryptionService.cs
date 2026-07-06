namespace BexioOrderImport.Wpf.Services;

public interface IEncryptionService
{
    string Encrypt(string clearText);
    string Decrypt(string encryptedText);
}
