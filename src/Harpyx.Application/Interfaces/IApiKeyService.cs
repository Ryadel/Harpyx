using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<ApiKeyCreateResultDto> CreateAsync(Guid userId, ApiKeyCreateRequest request, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken);
    Task<ApiKeyValidationResult?> ValidateAsync(string rawApiKey, CancellationToken cancellationToken);
}
