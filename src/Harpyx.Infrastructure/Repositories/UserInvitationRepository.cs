using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class UserInvitationRepository : IUserInvitationRepository
{
    private readonly HarpyxDbContext _dbContext;

    public UserInvitationRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserInvitation>> GetAllAsync(CancellationToken cancellationToken)
        => await _dbContext.UserInvitations
            .Include(i => i.Tenant)
            .Include(i => i.InvitedByUser)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<UserInvitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.UserInvitations
            .Include(i => i.Tenant)
            .Include(i => i.InvitedByUser)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<UserInvitation?> GetLatestPendingByEmailAsync(string email, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return _dbContext.UserInvitations
            .Where(i => i.Status == UserInvitationStatus.Pending &&
                        i.ExpiresAt >= nowUtc &&
                        i.Email.ToLower() == normalizedEmail)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken)
        => _dbContext.UserInvitations.AddAsync(invitation, cancellationToken).AsTask();

    public void Update(UserInvitation invitation) => _dbContext.UserInvitations.Update(invitation);
}
