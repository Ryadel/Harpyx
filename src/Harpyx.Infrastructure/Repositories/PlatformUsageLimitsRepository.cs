using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class PlatformUsageLimitsRepository : IPlatformUsageLimitsRepository
{
    private readonly HarpyxDbContext _dbContext;

    public PlatformUsageLimitsRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PlatformUsageLimits?> GetAsync(CancellationToken cancellationToken)
        => _dbContext.PlatformUsageLimits.FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(PlatformUsageLimits limits, CancellationToken cancellationToken)
        => _dbContext.PlatformUsageLimits.AddAsync(limits, cancellationToken).AsTask();

    public void Update(PlatformUsageLimits limits) => _dbContext.PlatformUsageLimits.Update(limits);
}
