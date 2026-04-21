using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly HarpyxDbContext _dbContext;

    public UserRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
        => await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

    public Task<User?> GetByObjectIdAsync(string objectId, CancellationToken cancellationToken)
        => _dbContext.Users.FirstOrDefaultAsync(u => u.ObjectId == objectId, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        => _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken)
        => await _dbContext.Users.ToListAsync(cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken)
        => _dbContext.Users.AddAsync(user, cancellationToken).AsTask();

    public async Task ClearOwnershipReferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _dbContext.Tenants
            .Where(t => t.CreatedByUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedByUserId, (Guid?)null), cancellationToken);

        await _dbContext.Workspaces
            .Where(w => w.CreatedByUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.CreatedByUserId, (Guid?)null), cancellationToken);

        await _dbContext.Projects
            .Where(p => p.CreatedByUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedByUserId, (Guid?)null), cancellationToken);

        await _dbContext.Documents
            .Where(d => d.UploadedByUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UploadedByUserId, (Guid?)null), cancellationToken);

        await _dbContext.UserTenants
            .Where(ut => ut.GrantedByUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(ut => ut.GrantedByUserId, (Guid?)null), cancellationToken);
    }

    public void Update(User user) => _dbContext.Users.Update(user);

    public void Remove(User user) => _dbContext.Users.Remove(user);

    public Task<User?> GetBySubjectIdAsync(string subjectId, CancellationToken cancellationToken)
        => _dbContext.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId, cancellationToken);
}
