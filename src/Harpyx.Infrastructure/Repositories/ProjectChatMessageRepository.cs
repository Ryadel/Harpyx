using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class ProjectChatMessageRepository : IProjectChatMessageRepository
{
    private readonly HarpyxDbContext _dbContext;

    public ProjectChatMessageRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectChatMessage>> GetByProjectAsync(
        Guid projectId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ProjectChatMessages
            .Where(m => m.ProjectId == projectId)
            .OrderByDescending(m => m.MessageTimestamp)
            .ThenByDescending(m => m.CreatedAt);

        var messages = limit > 0
            ? await query.Take(limit).ToListAsync(cancellationToken)
            : await query.ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public Task AddAsync(ProjectChatMessage message, CancellationToken cancellationToken)
        => _dbContext.ProjectChatMessages.AddAsync(message, cancellationToken).AsTask();

    public async Task AddRangeAsync(IReadOnlyList<ProjectChatMessage> messages, CancellationToken cancellationToken)
        => await _dbContext.ProjectChatMessages.AddRangeAsync(messages, cancellationToken);

    public void RemoveRange(IReadOnlyList<ProjectChatMessage> messages)
        => _dbContext.ProjectChatMessages.RemoveRange(messages);

    public Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken)
        => _dbContext.ProjectChatMessages.CountAsync(m => m.ProjectId == projectId, cancellationToken);
}
