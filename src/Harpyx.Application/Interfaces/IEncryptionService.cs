namespace Harpyx.Application.Interfaces;

public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string encryptedBase64);
}
