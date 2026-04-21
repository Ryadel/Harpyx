using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly HarpyxDbContext _dbContext;

    public WorkspaceRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Workspace>> GetAllAsync(IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var query = _dbContext.Workspaces.AsQueryable();
        if (tenantIds.Count > 0)
        {
            query = query.Where(w => tenantIds.Contains(w.TenantId));
        }

        return await query
            .Include(w => w.ChatModel)
            .ThenInclude(m => m.Connection)
            .Include(w => w.RagEmbeddingModel)
            .ThenInclude(m => m.Connection)
            .Include(w => w.OcrModel)
            .ThenInclude(m => m.Connection)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Workspaces
            .Include(w => w.ChatModel)
            .ThenInclude(m => m.Connection)
            .Include(w => w.RagEmbeddingModel)
            .ThenInclude(m => m.Connection)
            .Include(w => w.OcrModel)
            .ThenInclude(m => m.Connection)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Workspace>> GetByChatModelIdAsync(Guid modelId, CancellationToken cancellationToken)
        => await _dbContext.Workspaces
            .Where(w => w.ChatModelId == modelId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Workspace>> GetByRagModelIdAsync(Guid modelId, CancellationToken cancellationToken)
        => await _dbContext.Workspaces
            .Where(w => w.RagEmbeddingModelId == modelId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Workspace>> GetByOcrModelIdAsync(Guid modelId, CancellationToken cancellationToken)
        => await _dbContext.Workspaces
            .Where(w => w.OcrModelId == modelId)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Workspace workspace, CancellationToken cancellationToken)
        => _dbContext.Workspaces.AddAsync(workspace, cancellationToken).AsTask();

    public void Update(Workspace workspace) => _dbContext.Workspaces.Update(workspace);

    public void Remove(Workspace workspace) => _dbContext.Workspaces.Remove(workspace);
}
