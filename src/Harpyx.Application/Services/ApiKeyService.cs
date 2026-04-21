using System.Security.Cryptography;
using System.Text;
using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class ApiKeyService : IApiKeyService
{
    private const string KeyPrefix = "hpx_";
    private const int DefaultHashIterations = 120000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    private readonly IUserApiKeyRepository _apiKeys;
    private readonly IUsageLimitService _usageLimits;
    private readonly IUnitOfWork _unitOfWork;

    public ApiKeyService(
        IUserApiKeyRepository apiKeys,
        IUsageLimitService usageLimits,
        IUnitOfWork unitOfWork)
    {
        _apiKeys = apiKeys;
        _usageLimits = usageLimits;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var apiKeys = await _apiKeys.GetAllByUserAsync(userId, cancellationToken);
        return apiKeys.Select(ToDto).ToList();
    }

    public async Task<ApiKeyCreateResultDto> CreateAsync(Guid userId, ApiKeyCreateRequest request, CancellationToken cancellationToken)
    {
        await _usageLimits.EnsureApiAccessAllowedAsync(userId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("API key name is required.");
        if (name.Length > 200)
            throw new ArgumentException("API key name is too long.");
        if (request.ExpiresAtUtc is DateTimeOffset expiresAt && expiresAt <= now)
            throw new ArgumentException("API key expiration must be in the future.");
        if (request.Permissions == ApiPermission.None)
            throw new ArgumentException("Select at least one API permission.");

        var keyId = Guid.NewGuid().ToString("N");
        var secret = GenerateSecret();
        var plainTextKey = BuildPlainTextKey(keyId, secret);

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hashBytes = HashApiKey(plainTextKey, saltBytes, DefaultHashIterations);
        var apiKey = new UserApiKey
        {
            UserId = userId,
            Name = name,
            KeyId = keyId,
            KeySalt = Convert.ToBase64String(saltBytes),
            KeyHash = Convert.ToBase64String(hashBytes),
            KeyHashIterations = DefaultHashIterations,
            KeyPreview = KeyPrefix + keyId[..8],
            KeyLast4 = secret.Length >= 4 ? secret[^4..] : secret,
            Permissions = request.Permissions,
            IsActive = true,
            ExpiresAtUtc = request.ExpiresAtUtc
        };

        await _apiKeys.AddAsync(apiKey, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiKeyCreateResultDto(ToDto(apiKey), plainTextKey);
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeys.GetByIdAsync(apiKeyId, cancellationToken);
        if (apiKey is null || apiKey.UserId != userId)
            return false;

        if (!apiKey.IsActive && apiKey.RevokedAtUtc is not null)
            return true;

        var now = DateTimeOffset.UtcNow;
        apiKey.IsActive = false;
        apiKey.RevokedAtUtc = now;
        apiKey.UpdatedAt = now;
        _apiKeys.Update(apiKey);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeys.GetByIdAsync(apiKeyId, cancellationToken);
        if (apiKey is null || apiKey.UserId != userId)
            return false;

        if (apiKey.IsActive)
            return false;

        _apiKeys.Remove(apiKey);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ApiKeyValidationResult?> ValidateAsync(string rawApiKey, CancellationToken cancellationToken)
    {
        if (!TryParseRawKey(rawApiKey, out var keyId, out var secret))
            return null;

        var apiKey = await _apiKeys.GetByKeyIdAsync(keyId, cancellationToken);
        if (apiKey is null || apiKey.User is null)
            return null;
        if (!apiKey.IsActive || apiKey.RevokedAtUtc is not null)
            return null;

        var now = DateTimeOffset.UtcNow;
        if (apiKey.ExpiresAtUtc is DateTimeOffset expiresAtUtc && expiresAtUtc <= now)
            return null;
        if (!apiKey.User.IsActive)
            return null;

        if (!VerifyHash(BuildPlainTextKey(keyId, secret), apiKey.KeySalt, apiKey.KeyHash, apiKey.KeyHashIterations))
            return null;

        // Avoid writing for every single request when a key is used heavily.
        if (apiKey.LastUsedAtUtc is null || now - apiKey.LastUsedAtUtc.Value >= TimeSpan.FromMinutes(1))
        {
            apiKey.LastUsedAtUtc = now;
            apiKey.UpdatedAt = now;
            _apiKeys.Update(apiKey);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ApiKeyValidationResult(
            apiKey.Id,
            apiKey.UserId,
            apiKey.User.Email,
            apiKey.User.ObjectId,
            apiKey.User.SubjectId,
            apiKey.Permissions);
    }

    private static ApiKeyDto ToDto(UserApiKey apiKey)
        => new(
            apiKey.Id,
            apiKey.Name,
            apiKey.KeyPreview,
            apiKey.KeyLast4,
            apiKey.Permissions,
            apiKey.IsActive,
            apiKey.CreatedAt,
            apiKey.ExpiresAtUtc,
            apiKey.RevokedAtUtc,
            apiKey.LastUsedAtUtc);

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildPlainTextKey(string keyId, string secret)
        => $"{KeyPrefix}{keyId}.{secret}";

    private static bool TryParseRawKey(string? rawApiKey, out string keyId, out string secret)
    {
        keyId = string.Empty;
        secret = string.Empty;
        if (string.IsNullOrWhiteSpace(rawApiKey))
            return false;

        var value = rawApiKey.Trim();
        if (value.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
            value = value[KeyPrefix.Length..];

        var separatorIndex = value.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            return false;

        keyId = value[..separatorIndex].Trim();
        secret = value[(separatorIndex + 1)..].Trim();
        if (keyId.Length != 32 || secret.Length < 10)
            return false;

        return true;
    }

    private static byte[] HashApiKey(string plainTextApiKey, byte[] saltBytes, int iterations)
    {
        var keyBytes = Encoding.UTF8.GetBytes(plainTextApiKey);
        return Rfc2898DeriveBytes.Pbkdf2(keyBytes, saltBytes, iterations, HashAlgorithmName.SHA256, HashSizeBytes);
    }

    private static bool VerifyHash(string plainTextApiKey, string saltBase64, string hashBase64, int iterations)
    {
        byte[] saltBytes;
        byte[] expectedHash;

        try
        {
            saltBytes = Convert.FromBase64String(saltBase64);
            expectedHash = Convert.FromBase64String(hashBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        if (saltBytes.Length == 0 || expectedHash.Length == 0)
            return false;

        var actualHash = HashApiKey(plainTextApiKey, saltBytes, iterations);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
