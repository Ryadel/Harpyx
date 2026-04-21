using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class JobRepository : IJobRepository
{
    private readonly HarpyxDbContext _dbContext;

    public JobRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Job job, CancellationToken cancellationToken)
        => _dbContext.Jobs.AddAsync(job, cancellationToken).AsTask();

    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public void Update(Job job) => _dbContext.Jobs.Update(job);
}
