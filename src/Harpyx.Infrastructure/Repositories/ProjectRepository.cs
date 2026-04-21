using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly HarpyxDbContext _dbContext;

    public ProjectRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Project project, CancellationToken cancellationToken)
        => _dbContext.Projects.AddAsync(project, cancellationToken).AsTask();

    public async Task<IReadOnlyList<Project>> GetAllAsync(IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
        => tenantIds.Count == 0
            ? await _dbContext.Projects
                .Include(p => p.Workspace)
                .ThenInclude(w => w.ChatModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.Workspace)
                .ThenInclude(w => w.RagEmbeddingModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.Workspace)
                .ThenInclude(w => w.OcrModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.ChatModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.RagEmbeddingModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.OcrModel)
                .ThenInclude(m => m.Connection)
                .ToListAsync(cancellationToken)
            : await _dbContext.Projects
                .Include(p => p.Workspace)
                .ThenInclude(w => w.ChatModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.Workspace)
                .ThenInclude(w => w.RagEmbeddingModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.Workspace)
                .ThenInclude(w => w.OcrModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.ChatModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.RagEmbeddingModel)
                .ThenInclude(m => m.Connection)
                .Include(p => p.OcrModel)
                .ThenInclude(m => m.Connection)
                .Where(p => tenantIds.Contains(p.Workspace.TenantId))
                .ToListAsync(cancellationToken);

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Projects
            .Include(p => p.Workspace)
            .ThenInclude(w => w.ChatModel)
            .ThenInclude(m => m.Connection)
            .Include(p => p.Workspace)
            .ThenInclude(w => w.RagEmbeddingModel)
            .ThenInclude(m => m.Connection)
            .Include(p => p.Workspace)
            .ThenInclude(w => w.OcrModel)
            .ThenInclude(m => m.Connection)
            .Include(p => p.ChatModel)
            .ThenInclude(m => m.Connection)
            .Include(p => p.RagEmbeddingModel)
            .ThenInclude(m => m.Connection)
            .Include(p => p.OcrModel)
            .ThenInclude(m => m.Connection)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetExpiredProjectIdsAsync(DateTimeOffset nowUtc, int take, CancellationToken cancellationToken)
    {
        var size = take <= 0 ? 50 : take;
        return await _dbContext.Projects
            .Where(p => p.LifetimePreset != null && p.LifetimeExpiresAtUtc != null && p.LifetimeExpiresAtUtc <= nowUtc)
            .OrderBy(p => p.LifetimeExpiresAtUtc)
            .Select(p => p.Id)
            .Take(size)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetByChatModelIdAsync(Guid modelId, CancellationToken cancellationToken)
        => await _dbContext.Projects
            .Where(p => p.ChatLlmOverride == Domain.Enums.LlmFeatureOverride.Enabled && p.ChatModelId == modelId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Project>> GetByRagModelIdAsync(Guid modelId, CancellationToken cancellationToken)
        => await _dbContext.Projects
            .Where(p => p.RagLlmOverride == Domain.Enums.LlmFeatureOverride.Enabled && p.RagEmbeddingModelId == modelId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Project>> GetByOcrModelIdAsync(Guid modelId, CancellationToken cancellationToken)
        => await _dbContext.Projects
            .Where(p => p.OcrLlmOverride == Domain.Enums.LlmFeatureOverride.Enabled && p.OcrModelId == modelId)
            .ToListAsync(cancellationToken);

    public void Update(Project project) => _dbContext.Projects.Update(project);

    public void Remove(Project project) => _dbContext.Projects.Remove(project);
}
