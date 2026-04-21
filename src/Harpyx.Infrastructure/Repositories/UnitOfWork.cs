using Harpyx.Application.Interfaces;
using Harpyx.Infrastructure.Data;

namespace Harpyx.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly HarpyxDbContext _dbContext;

    public UnitOfWork(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
