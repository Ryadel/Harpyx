using Harpyx.Application.DTOs;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Interfaces;

public interface IUserLlmProviderService
{
    Task<LlmProviderListDto> GetAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<LlmProviderDto> SaveAsync(Guid userId, LlmProviderSaveRequest request, CancellationToken cancellationToken);
    Task<LlmProviderDeleteResult> DeleteAsync(Guid userId, LlmProvider provider, CancellationToken cancellationToken);
    Task SetDefaultAsync(Guid userId, LlmProviderType usage, Guid modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HostedLlmModelDto>> GetAvailableHostedModelsAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveHostedModelSelectionAsync(Guid userId, HostedLlmModelSelectionRequest request, CancellationToken cancellationToken);
    Task<bool> HasAnyChatConfiguredAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LlmProviderOptionDto>> GetConfiguredByUsersAsync(
        IReadOnlyList<Guid> userIds,
        LlmProviderType usage,
        CancellationToken cancellationToken);
}
