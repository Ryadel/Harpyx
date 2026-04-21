using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class UserApiKeyRepository : IUserApiKeyRepository
{
    private readonly HarpyxDbContext _dbContext;

    public UserApiKeyRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserApiKey>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken)
        => await _dbContext.UserApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.IsActive)
            .ThenByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<UserApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.UserApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

    public Task<UserApiKey?> GetByKeyIdAsync(string keyId, CancellationToken cancellationToken)
        => _dbContext.UserApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyId == keyId, cancellationToken);

    public Task AddAsync(UserApiKey apiKey, CancellationToken cancellationToken)
        => _dbContext.UserApiKeys.AddAsync(apiKey, cancellationToken).AsTask();

    public void Update(UserApiKey apiKey) => _dbContext.UserApiKeys.Update(apiKey);

    public void Remove(UserApiKey apiKey) => _dbContext.UserApiKeys.Remove(apiKey);
}
