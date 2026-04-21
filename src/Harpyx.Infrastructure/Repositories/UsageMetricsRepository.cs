using Harpyx.Application.Interfaces;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class UsageMetricsRepository : IUsageMetricsRepository
{
    private const string PersonalTenantName = "Personal";
    private readonly HarpyxDbContext _dbContext;

    public UsageMetricsRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid?> GetPersonalTenantIdByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.UserTenants
            .Where(ut => ut.UserId == userId && ut.Tenant.Name == PersonalTenantName)
            .Select(ut => (Guid?)ut.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountTenantsByUserAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.UserTenants
            .Where(ut => ut.UserId == userId)
            .Select(ut => ut.TenantId)
            .Distinct()
            .CountAsync(cancellationToken);

    public Task<int> CountWorkspacesByUserAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.Workspaces
            .Where(w => _dbContext.UserTenants.Any(ut => ut.UserId == userId && ut.TenantId == w.TenantId))
            .CountAsync(cancellationToken);

    public Task<int> CountProjectsByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
        => _dbContext.Projects
            .Where(p => p.WorkspaceId == workspaceId)
            .CountAsync(cancellationToken);

    public Task<int> CountPermanentProjectsByWorkspaceAsync(Guid workspaceId, Guid? excludeProjectId, CancellationToken cancellationToken)
        => _dbContext.Projects
            .Where(p => p.WorkspaceId == workspaceId &&
                        p.LifetimePreset == null &&
                        (excludeProjectId == null || p.Id != excludeProjectId.Value))
            .CountAsync(cancellationToken);

    public Task<int> CountLlmProvidersByUserAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.LlmConnections
            .Where(p => p.Scope == Harpyx.Domain.Enums.LlmConnectionScope.Personal && p.UserId == userId)
            .CountAsync(cancellationToken);

    public Task<int> CountDocumentsByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
        => _dbContext.Documents
            .Where(d => d.Project!.WorkspaceId == workspaceId)
            .CountAsync(cancellationToken);

    public async Task<long> GetStorageByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var total = await _dbContext.Documents
            .Where(d => _dbContext.UserTenants.Any(ut => ut.UserId == userId && ut.TenantId == d.Project!.Workspace!.TenantId))
            .Select(d => (long?)d.SizeBytes)
            .SumAsync(cancellationToken);

        return total ?? 0L;
    }

    public async Task<long> GetStorageByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var total = await _dbContext.Documents
            .Where(d => d.Project!.Workspace!.TenantId == tenantId)
            .Select(d => (long?)d.SizeBytes)
            .SumAsync(cancellationToken);

        return total ?? 0L;
    }

    public async Task<long> GetStorageByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var total = await _dbContext.Documents
            .Where(d => d.Project!.WorkspaceId == workspaceId)
            .Select(d => (long?)d.SizeBytes)
            .SumAsync(cancellationToken);

        return total ?? 0L;
    }
}
