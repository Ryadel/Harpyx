using Harpyx.Application.Filters;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Interfaces;

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> GetAllAsync(IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetExpiredProjectIdsAsync(DateTimeOffset nowUtc, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> GetByChatModelIdAsync(Guid modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> GetByRagModelIdAsync(Guid modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> GetByOcrModelIdAsync(Guid modelId, CancellationToken cancellationToken);
    void Update(Project project);
    void Remove(Project project);
}

public interface IProjectPromptRepository
{
    Task<ProjectPrompt?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectPrompt>> GetByProjectAndTypeAsync(Guid projectId, ProjectPromptType promptType, CancellationToken cancellationToken);
    Task<ProjectPrompt?> GetExactMatchAsync(Guid projectId, ProjectPromptType promptType, string contentHash, string content, CancellationToken cancellationToken);
    Task<ProjectPrompt?> GetLastUsedAsync(Guid projectId, ProjectPromptType promptType, CancellationToken cancellationToken);
    Task AddAsync(ProjectPrompt prompt, CancellationToken cancellationToken);
    void Update(ProjectPrompt prompt);
    void RemoveRange(IReadOnlyList<ProjectPrompt> prompts);
}

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<Workspace>> GetAllAsync(IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken);
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> GetByChatModelIdAsync(Guid modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> GetByRagModelIdAsync(Guid modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> GetByOcrModelIdAsync(Guid modelId, CancellationToken cancellationToken);
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken);
    void Update(Workspace workspace);
    void Remove(Workspace workspace);
}

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken);
    Task<IReadOnlyList<Document>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Document>> GetByParentDocumentIdAsync(Guid parentDocumentId, CancellationToken cancellationToken);
    Task<(int FileCount, long TotalSizeBytes)> GetExtractionStatsByRootAsync(Guid rootContainerDocumentId, CancellationToken cancellationToken);
    void Update(Document document);
    void Remove(Document document);
}

public interface IDocumentChunkRepository
{
    Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken);
    Task RemoveByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken);
    Task RemoveByProjectIdAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdsAsync(IReadOnlyList<Guid> documentIds, CancellationToken cancellationToken);
}

public interface IJobRepository
{
    Task AddAsync(Job job, CancellationToken cancellationToken);
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Update(Job job);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken);
    Task<User?> GetByObjectIdAsync(string objectId, CancellationToken cancellationToken);
    Task<User?> GetBySubjectIdAsync(string subjectId, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task ClearOwnershipReferencesAsync(Guid userId, CancellationToken cancellationToken);
    void Update(User user);
    void Remove(User user);
}

public interface IUserTenantRepository
{
    Task<IReadOnlyList<Guid>> GetTenantIdsByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetUserIdsByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<UserTenant?> GetMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserTenant>> GetMembershipsByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserTenant>> GetMembershipsGrantedByUserAsync(Guid tenantId, Guid grantedByUserId, CancellationToken cancellationToken);
    Task<int> CountMembersByRoleAsync(Guid tenantId, TenantRole role, CancellationToken cancellationToken);
    Task AddOrUpdateMembershipAsync(Guid userId, Guid tenantId, TenantRole tenantRole, bool canGrant, Guid? grantedByUserId, DateTimeOffset? grantedAt, CancellationToken cancellationToken);
    Task RemoveMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    Task ReplaceAsync(Guid userId, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken);
    Task AddIfMissingAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}

public interface ITenantRepository
{
    Task<IReadOnlyList<Tenant>> GetAsync(TenantFilter filter, CancellationToken cancellationToken);
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);
    void Update(Tenant tenant);
    void Remove(Tenant tenant);
}

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

public interface ILlmCatalogRepository
{
    Task<IReadOnlyList<LlmConnection>> GetPersonalConnectionsByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LlmConnection>> GetHostedConnectionsAsync(CancellationToken cancellationToken);
    Task<LlmConnection?> GetConnectionByIdAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<LlmConnection?> GetPersonalConnectionByProviderAsync(Guid userId, LlmProvider provider, CancellationToken cancellationToken);
    Task AddConnectionAsync(LlmConnection connection, CancellationToken cancellationToken);
    void UpdateConnection(LlmConnection connection);
    void RemoveConnection(LlmConnection connection);

    Task<LlmModel?> GetModelByIdAsync(Guid modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LlmModel>> GetModelsByConnectionAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LlmModel>> GetSelectableModelsAsync(IReadOnlyList<Guid> userIds, LlmProviderType usage, CancellationToken cancellationToken);
    Task<IReadOnlyList<LlmModel>> GetPublishedHostedModelsAsync(LlmProviderType? usage, CancellationToken cancellationToken);
    Task AddModelAsync(LlmModel model, CancellationToken cancellationToken);
    void UpdateModel(LlmModel model);
    void RemoveModel(LlmModel model);

    Task<IReadOnlyList<UserLlmModelPreference>> GetPreferencesByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserLlmModelPreference?> GetPreferenceAsync(Guid userId, LlmProviderType usage, CancellationToken cancellationToken);
    Task AddPreferenceAsync(UserLlmModelPreference preference, CancellationToken cancellationToken);
    void UpdatePreference(UserLlmModelPreference preference);
    void RemovePreference(UserLlmModelPreference preference);
}

public interface IUserApiKeyRepository
{
    Task<IReadOnlyList<UserApiKey>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserApiKey?> GetByKeyIdAsync(string keyId, CancellationToken cancellationToken);
    Task AddAsync(UserApiKey apiKey, CancellationToken cancellationToken);
    void Update(UserApiKey apiKey);
    void Remove(UserApiKey apiKey);
}

public interface IPlatformSettingsRepository
{
    Task<PlatformSettings?> GetAsync(CancellationToken cancellationToken);
    Task AddAsync(PlatformSettings settings, CancellationToken cancellationToken);
    void Update(PlatformSettings settings);
}

public interface IPlatformUsageLimitsRepository
{
    Task<PlatformUsageLimits?> GetAsync(CancellationToken cancellationToken);
    Task AddAsync(PlatformUsageLimits limits, CancellationToken cancellationToken);
    void Update(PlatformUsageLimits limits);
}

public interface IUserInvitationRepository
{
    Task<IReadOnlyList<UserInvitation>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserInvitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserInvitation?> GetLatestPendingByEmailAsync(string email, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken);
    void Update(UserInvitation invitation);
}

public interface IUsageMetricsRepository
{
    Task<Guid?> GetPersonalTenantIdByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<int> CountTenantsByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<int> CountWorkspacesByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<int> CountProjectsByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<int> CountPermanentProjectsByWorkspaceAsync(Guid workspaceId, Guid? excludeProjectId, CancellationToken cancellationToken);
    Task<int> CountLlmProvidersByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<int> CountDocumentsByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<long> GetStorageByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<long> GetStorageByTenantAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<long> GetStorageByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken);
}

public interface IProjectChatMessageRepository
{
    Task<IReadOnlyList<ProjectChatMessage>> GetByProjectAsync(Guid projectId, int limit, CancellationToken cancellationToken);
    Task AddAsync(ProjectChatMessage message, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyList<ProjectChatMessage> messages, CancellationToken cancellationToken);
    void RemoveRange(IReadOnlyList<ProjectChatMessage> messages);
    Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
