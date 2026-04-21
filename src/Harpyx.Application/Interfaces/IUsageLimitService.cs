using Harpyx.Application.DTOs;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Interfaces;

public interface IUsageLimitService
{
    Task<UsageLimitsDto> GetAsync(CancellationToken cancellationToken);
    Task<UsageLimitsDto> SaveAsync(UsageLimitsSaveRequest request, CancellationToken cancellationToken);

    Task EnsureTenantCreationAllowedAsync(Guid userId, CancellationToken cancellationToken);
    Task EnsureWorkspaceCreationAllowedAsync(Guid userId, CancellationToken cancellationToken);
    Task EnsureProjectCreationAllowedAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task EnsureProjectLifetimeAllowedAsync(Guid workspaceId, Guid? projectId, ProjectLifetimePreset? lifetimePreset, CancellationToken cancellationToken);
    Task EnsureLlmProviderCreationAllowedAsync(Guid userId, CancellationToken cancellationToken);
    Task EnsureDocumentUploadAllowedAsync(Guid userId, Guid tenantId, Guid workspaceId, long uploadSizeBytes, CancellationToken cancellationToken);
    Task EnsureRagIndexingAllowedAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<bool> IsOcrAllowedAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<bool> IsApiEnabledForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task EnsureApiAccessAllowedAsync(Guid userId, CancellationToken cancellationToken);
}
