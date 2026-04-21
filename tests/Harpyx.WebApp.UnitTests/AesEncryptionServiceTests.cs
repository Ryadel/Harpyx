using System.Security.Cryptography;

namespace Harpyx.WebApp.UnitTests;

public class AesEncryptionServiceTests
{
    private static EncryptionOptions CreateValidOptions()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        return new EncryptionOptions { MasterKey = Convert.ToBase64String(key) };
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_ReturnsOriginal()
    {
        var service = new AesEncryptionService(CreateValidOptions());
        var original = "sk-proj-abc123XYZ_test-key";

        var encrypted = service.Encrypt(original);
        var decrypted = service.Decrypt(encrypted);

        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertext_EachTime()
    {
        var service = new AesEncryptionService(CreateValidOptions());
        var plaintext = "sk-test-key-12345";

        var encrypted1 = service.Encrypt(plaintext);
        var encrypted2 = service.Encrypt(plaintext);

        encrypted1.Should().NotBe(encrypted2, "AES-GCM uses a random nonce each time");
    }

    [Fact]
    public void Decrypt_WithWrongKey_Throws()
    {
        var service1 = new AesEncryptionService(CreateValidOptions());
        var service2 = new AesEncryptionService(CreateValidOptions());

        var encrypted = service1.Encrypt("secret-key");

        var act = () => service2.Decrypt(encrypted);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Constructor_WithMissingKey_Throws()
    {
        var act = () => new AesEncryptionService(new EncryptionOptions { MasterKey = "" });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_WithWrongKeyLength_Throws()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var act = () => new AesEncryptionService(new EncryptionOptions { MasterKey = shortKey });
        act.Should().Throw<InvalidOperationException>();
    }
}
