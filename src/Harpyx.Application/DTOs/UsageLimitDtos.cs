namespace Harpyx.Application.DTOs;

public record UsageLimitsDto(
    int? TenantsPerUser,
    int? WorkspacesPerUser,
    int? DocumentsPerWorkspace,
    int? StoragePerUserGb,
    int? StoragePerTenantGb,
    int? StoragePerWorkspaceGb,
    int? ProjectsPerWorkspace,
    int? PermanentProjectsPerWorkspace,
    int? MaxTemporaryProjectLifetimeHours,
    int? LlmProvidersPerUser,
    bool EnableOcr,
    bool EnableRagIndexing,
    bool EnableApi);

public record UsageLimitsSaveRequest(
    int? TenantsPerUser,
    int? WorkspacesPerUser,
    int? DocumentsPerWorkspace,
    int? StoragePerUserGb,
    int? StoragePerTenantGb,
    int? StoragePerWorkspaceGb,
    int? ProjectsPerWorkspace,
    int? PermanentProjectsPerWorkspace,
    int? MaxTemporaryProjectLifetimeHours,
    int? LlmProvidersPerUser,
    bool EnableOcr,
    bool EnableRagIndexing,
    bool EnableApi);
