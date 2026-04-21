using Harpyx.Application.DTOs;
using Harpyx.Domain.Enums;
using System.Linq;

namespace Harpyx.WebApp.UnitTests;

public class ApiKeyServiceTests
{
    private readonly Mock<IUserApiKeyRepository> _apiKeys = new();
    private readonly Mock<IUsageLimitService> _usageLimits = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ApiKeyServiceTests()
    {
        _usageLimits
            .Setup(p => p.EnsureApiAccessAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CreateAsync_CreatesHashedApiKeyAndReturnsPlainText()
    {
        var userId = Guid.NewGuid();
        UserApiKey? createdEntity = null;

        _apiKeys
            .Setup(r => r.AddAsync(It.IsAny<UserApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<UserApiKey, CancellationToken>((key, _) => createdEntity = key)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.CreateAsync(
            userId,
            new ApiKeyCreateRequest("Automation key", null, ApiPermission.QueryProjects),
            CancellationToken.None);

        result.ApiKey.Name.Should().Be("Automation key");
        result.PlainTextKey.Should().StartWith("hpx_");
        result.PlainTextKey.Should().Contain(".");

        createdEntity.Should().NotBeNull();
        createdEntity!.UserId.Should().Be(userId);
        createdEntity.KeyHash.Should().NotBeNullOrWhiteSpace();
        createdEntity.KeySalt.Should().NotBeNullOrWhiteSpace();
        createdEntity.KeyHash.Should().NotContain(result.PlainTextKey);
        createdEntity.Permissions.Should().Be(ApiPermission.QueryProjects);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_WhenKeyIsValid_ReturnsIdentityData()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "api-user@harpyx.local",
            ObjectId = "oid-123",
            SubjectId = "sub-456",
            IsActive = true
        };

        var store = new List<UserApiKey>();
        _apiKeys
            .Setup(r => r.AddAsync(It.IsAny<UserApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<UserApiKey, CancellationToken>((key, _) =>
            {
                key.User = user;
                store.Add(key);
            })
            .Returns(Task.CompletedTask);

        _apiKeys
            .Setup(r => r.GetByKeyIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string keyId, CancellationToken _) => store.FirstOrDefault(k => k.KeyId == keyId));

        _apiKeys
            .Setup(r => r.Update(It.IsAny<UserApiKey>()))
            .Callback<UserApiKey>(_ => { });

        var service = CreateService();
        var created = await service.CreateAsync(
            userId,
            new ApiKeyCreateRequest("Key", DateTimeOffset.UtcNow.AddDays(7), ApiPermission.QueryProjects | ApiPermission.CreateProjects),
            CancellationToken.None);

        var validation = await service.ValidateAsync(created.PlainTextKey, CancellationToken.None);

        validation.Should().NotBeNull();
        validation!.UserId.Should().Be(userId);
        validation.Email.Should().Be("api-user@harpyx.local");
        validation.ObjectId.Should().Be("oid-123");
        validation.SubjectId.Should().Be("sub-456");
        validation.Permissions.Should().Be(ApiPermission.QueryProjects | ApiPermission.CreateProjects);

        _apiKeys.Verify(r => r.Update(It.IsAny<UserApiKey>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RevokeAsync_WhenRevoked_ValidateReturnsNull()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "api-user@harpyx.local",
            IsActive = true
        };

        UserApiKey? stored = null;
        _apiKeys
            .Setup(r => r.AddAsync(It.IsAny<UserApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<UserApiKey, CancellationToken>((key, _) =>
            {
                key.User = user;
                stored = key;
            })
            .Returns(Task.CompletedTask);

        _apiKeys
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => stored?.Id == id ? stored : null);

        _apiKeys
            .Setup(r => r.GetByKeyIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string keyId, CancellationToken _) => stored?.KeyId == keyId ? stored : null);

        var service = CreateService();
        var created = await service.CreateAsync(userId, new ApiKeyCreateRequest("Temp", null, ApiPermission.QueryProjects), CancellationToken.None);

        var revoked = await service.RevokeAsync(userId, created.ApiKey.Id, CancellationToken.None);
        revoked.Should().BeTrue();

        var validation = await service.ValidateAsync(created.PlainTextKey, CancellationToken.None);
        validation.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenKeyRevoked_RemovesKey()
    {
        var userId = Guid.NewGuid();
        UserApiKey? stored = null;

        _apiKeys
            .Setup(r => r.AddAsync(It.IsAny<UserApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<UserApiKey, CancellationToken>((key, _) => stored = key)
            .Returns(Task.CompletedTask);

        _apiKeys
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => stored?.Id == id ? stored : null);

        var service = CreateService();
        var created = await service.CreateAsync(
            userId,
            new ApiKeyCreateRequest("Disposable", null, ApiPermission.QueryProjects),
            CancellationToken.None);

        // Active key cannot be deleted directly.
        var deletedBeforeRevoke = await service.DeleteAsync(userId, created.ApiKey.Id, CancellationToken.None);
        deletedBeforeRevoke.Should().BeFalse();

        await service.RevokeAsync(userId, created.ApiKey.Id, CancellationToken.None);
        var deleted = await service.DeleteAsync(userId, created.ApiKey.Id, CancellationToken.None);

        deleted.Should().BeTrue();
        _apiKeys.Verify(r => r.Remove(It.IsAny<UserApiKey>()), Times.Once);
    }

    private ApiKeyService CreateService() => new(_apiKeys.Object, _usageLimits.Object, _unitOfWork.Object);
}
