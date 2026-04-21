using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class DocumentChunkRepository : IDocumentChunkRepository
{
    private readonly HarpyxDbContext _dbContext;

    public DocumentChunkRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken)
        => _dbContext.DocumentChunks.AddRangeAsync(chunks, cancellationToken);

    public Task RemoveByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken)
        => _dbContext.DocumentChunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken);

    public Task RemoveByProjectIdAsync(Guid projectId, CancellationToken cancellationToken)
        => _dbContext.DocumentChunks
            .Where(c => c.Document != null && c.Document.ProjectId == projectId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdsAsync(IReadOnlyList<Guid> documentIds, CancellationToken cancellationToken)
        => await _dbContext.DocumentChunks
            .Include(c => c.Document)
            .ThenInclude(d => d!.Project)
            .ThenInclude(p => p!.RagEmbeddingModel)
            .Include(c => c.Document)
            .ThenInclude(d => d!.Project)
            .ThenInclude(p => p!.OcrModel)
            .Include(c => c.Document)
            .ThenInclude(d => d!.Project)
            .ThenInclude(p => p!.Workspace)
            .ThenInclude(w => w.RagEmbeddingModel)
            .Include(c => c.Document)
            .ThenInclude(d => d!.Project)
            .ThenInclude(p => p!.Workspace)
            .ThenInclude(w => w.OcrModel)
            .Where(c => documentIds.Contains(c.DocumentId))
            .ToListAsync(cancellationToken);
}
