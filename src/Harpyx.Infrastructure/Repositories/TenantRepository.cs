using Harpyx.Application.Filters;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly HarpyxDbContext _dbContext;

    public TenantRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Tenant>> GetAsync(TenantFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.Tenants.AsQueryable();

        if (filter.Ids is { Count: > 0 })
        {
            query = filter.IncludeVisibleToAllUsers
                ? query.Where(t => filter.Ids.Contains(t.Id) || t.IsVisibleToAllUsers)
                : query.Where(t => filter.Ids.Contains(t.Id));
        }
        else if (filter.IncludeVisibleToAllUsers)
        {
            query = query.Where(t => t.IsVisibleToAllUsers);
        }

        if (filter.IsActive is not null)
        {
            query = query.Where(t => t.IsActive == filter.IsActive.Value);
        }

        return await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);
    }

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken)
        => _dbContext.Tenants.AddAsync(tenant, cancellationToken).AsTask();

    public void Update(Tenant tenant) => _dbContext.Tenants.Update(tenant);

    public void Remove(Tenant tenant) => _dbContext.Tenants.Remove(tenant);
}