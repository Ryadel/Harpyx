using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class UserTenantRepository : IUserTenantRepository
{
    private readonly HarpyxDbContext _dbContext;

    public UserTenantRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Guid>> GetTenantIdsByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        => await _dbContext.UserTenants
            .Where(x => x.UserId == userId)
            .Select(x => x.TenantId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetUserIdsByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _dbContext.UserTenants
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

    public Task<UserTenant?> GetMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
        => _dbContext.UserTenants
            .Include(x => x.User)
            .Include(x => x.GrantedByUser)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<UserTenant>> GetMembershipsByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _dbContext.UserTenants
            .Include(x => x.User)
            .Include(x => x.GrantedByUser)
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserTenant>> GetMembershipsGrantedByUserAsync(Guid tenantId, Guid grantedByUserId, CancellationToken cancellationToken)
        => await _dbContext.UserTenants
            .Include(x => x.User)
            .Include(x => x.GrantedByUser)
            .Where(x => x.TenantId == tenantId && x.GrantedByUserId == grantedByUserId)
            .ToListAsync(cancellationToken);

    public Task<int> CountMembersByRoleAsync(Guid tenantId, TenantRole role, CancellationToken cancellationToken)
        => _dbContext.UserTenants.CountAsync(x => x.TenantId == tenantId && x.TenantRole == role, cancellationToken);

    public async Task AddOrUpdateMembershipAsync(Guid userId, Guid tenantId, TenantRole tenantRole, bool canGrant, Guid? grantedByUserId, DateTimeOffset? grantedAt, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.UserTenants
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId, cancellationToken);

        if (existing is null)
        {
            await _dbContext.UserTenants.AddAsync(new UserTenant
            {
                UserId = userId,
                TenantId = tenantId,
                TenantRole = tenantRole,
                CanGrant = canGrant,
                GrantedByUserId = grantedByUserId,
                GrantedAt = grantedAt
            }, cancellationToken);
            return;
        }

        existing.TenantRole = tenantRole;
        existing.CanGrant = canGrant;
        existing.GrantedByUserId = grantedByUserId;
        existing.GrantedAt = grantedAt;
    }

    public async Task RemoveMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var membership = await _dbContext.UserTenants
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId, cancellationToken);
        if (membership is null)
            return;

        _dbContext.UserTenants.Remove(membership);
    }

    public async Task ReplaceAsync(Guid userId, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.UserTenants
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        var desired = new HashSet<Guid>(tenantIds);
        var toRemove = existing.Where(x => !desired.Contains(x.TenantId)).ToList();
        var toAdd = desired.Except(existing.Select(x => x.TenantId))
            .Select(id => new UserTenant
            {
                UserId = userId,
                TenantId = id,
                TenantRole = TenantRole.Viewer,
                CanGrant = false,
                GrantedByUserId = null,
                GrantedAt = null
            })
            .ToList();

        if (toRemove.Count > 0)
        {
            _dbContext.UserTenants.RemoveRange(toRemove);
        }

        if (toAdd.Count > 0)
        {
            await _dbContext.UserTenants.AddRangeAsync(toAdd, cancellationToken);
        }
    }

    public async Task AddIfMissingAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.UserTenants
            .AnyAsync(x => x.UserId == userId && x.TenantId == tenantId, cancellationToken);

        if (!exists)
        {
            await _dbContext.UserTenants.AddAsync(new UserTenant
            {
                UserId = userId,
                TenantId = tenantId,
                TenantRole = TenantRole.Viewer,
                CanGrant = false
            }, cancellationToken);
        }
    }
}
