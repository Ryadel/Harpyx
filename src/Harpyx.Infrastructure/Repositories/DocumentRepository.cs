using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harpyx.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly HarpyxDbContext _dbContext;

    public DocumentRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Document document, CancellationToken cancellationToken)
        => _dbContext.Documents.AddAsync(document, cancellationToken).AsTask();

    public async Task<IReadOnlyList<Document>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken)
        => await _dbContext.Documents
            .Include(d => d.Project)
            .ThenInclude(p => p!.Workspace)
            .ThenInclude(w => w.RagEmbeddingModel)
            .Include(d => d.Project)
            .ThenInclude(p => p!.Workspace)
            .ThenInclude(w => w.OcrModel)
            .Include(d => d.Project)
            .ThenInclude(p => p!.OcrModel)
            .Where(d => d.ProjectId == projectId)
            .ToListAsync(cancellationToken);

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Documents
            .Include(d => d.Project)
            .ThenInclude(p => p!.Workspace)
            .ThenInclude(w => w.RagEmbeddingModel)
            .Include(d => d.Project)
            .ThenInclude(p => p!.Workspace)
            .ThenInclude(w => w.OcrModel)
            .Include(d => d.Project)
            .ThenInclude(p => p!.RagEmbeddingModel)
            .Include(d => d.Project)
            .ThenInclude(p => p!.OcrModel)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Document>> GetByParentDocumentIdAsync(Guid parentDocumentId, CancellationToken cancellationToken)
        => await _dbContext.Documents
            .Where(d => d.ParentDocumentId == parentDocumentId)
            .ToListAsync(cancellationToken);

    public async Task<(int FileCount, long TotalSizeBytes)> GetExtractionStatsByRootAsync(Guid rootContainerDocumentId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Documents.Where(d => d.RootContainerDocumentId == rootContainerDocumentId);
        var fileCount = await query.CountAsync(cancellationToken);
        var totalSizeBytes = await query.SumAsync(d => (long?)d.SizeBytes, cancellationToken) ?? 0L;
        return (fileCount, totalSizeBytes);
    }

    public void Update(Document document) => _dbContext.Documents.Update(document);

    public void Remove(Document document) => _dbContext.Documents.Remove(document);
}
