using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class ProjectPromptRepository : IProjectPromptRepository
{
    private readonly HarpyxDbContext _dbContext;

    public ProjectPromptRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectPrompt?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.ProjectPrompts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProjectPrompt>> GetByProjectAndTypeAsync(
        Guid projectId,
        ProjectPromptType promptType,
        CancellationToken cancellationToken)
        => await _dbContext.ProjectPrompts
            .Where(p => p.ProjectId == projectId && p.PromptType == promptType)
            .OrderByDescending(p => p.LastUsedAt)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ProjectPrompt?> GetExactMatchAsync(
        Guid projectId,
        ProjectPromptType promptType,
        string contentHash,
        string content,
        CancellationToken cancellationToken)
        => _dbContext.ProjectPrompts
            .FirstOrDefaultAsync(
                p => p.ProjectId == projectId &&
                     p.PromptType == promptType &&
                     p.ContentHash == contentHash &&
                     p.Content == content,
                cancellationToken);

    public Task<ProjectPrompt?> GetLastUsedAsync(
        Guid projectId,
        ProjectPromptType promptType,
        CancellationToken cancellationToken)
        => _dbContext.ProjectPrompts
            .Where(p => p.ProjectId == projectId && p.PromptType == promptType)
            .OrderByDescending(p => p.LastUsedAt)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(ProjectPrompt prompt, CancellationToken cancellationToken)
        => _dbContext.ProjectPrompts.AddAsync(prompt, cancellationToken).AsTask();

    public void Update(ProjectPrompt prompt) => _dbContext.ProjectPrompts.Update(prompt);

    public void RemoveRange(IReadOnlyList<ProjectPrompt> prompts) => _dbContext.ProjectPrompts.RemoveRange(prompts);
}
