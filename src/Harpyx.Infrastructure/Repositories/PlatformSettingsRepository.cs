using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class PlatformSettingsRepository : IPlatformSettingsRepository
{
    private readonly HarpyxDbContext _dbContext;

    public PlatformSettingsRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PlatformSettings?> GetAsync(CancellationToken cancellationToken)
        => _dbContext.PlatformSettings.FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(PlatformSettings settings, CancellationToken cancellationToken)
        => _dbContext.PlatformSettings.AddAsync(settings, cancellationToken).AsTask();

    public void Update(PlatformSettings settings) => _dbContext.PlatformSettings.Update(settings);
}
