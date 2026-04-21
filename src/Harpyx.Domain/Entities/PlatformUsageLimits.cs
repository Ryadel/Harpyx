namespace Harpyx.Domain.Entities;

public class PlatformUsageLimits : BaseEntity
{
    public int? TenantsPerUser { get; set; } = 1;
    public int? WorkspacesPerUser { get; set; } = 3;
    public int? DocumentsPerWorkspace { get; set; } = 200;

    public int? StoragePerUserGb { get; set; } = 2;
    public int? StoragePerTenantGb { get; set; } = 5;
    public int? StoragePerWorkspaceGb { get; set; } = 2;

    public int? ProjectsPerWorkspace { get; set; } = 10;
    public int? PermanentProjectsPerWorkspace { get; set; } = 3;
    public int? MaxTemporaryProjectLifetimeHours { get; set; } = 24 * 30;
    public int? LlmProvidersPerUser { get; set; } = 3;

    public bool EnableOcr { get; set; } = true;
    public bool EnableRagIndexing { get; set; } = true;
    public bool EnableApi { get; set; } = true;
}
