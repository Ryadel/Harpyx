using Harpyx.Domain.Entities;

namespace Harpyx.Application.Interfaces;

public interface IRagIngestionService
{
    Task IngestDocumentAsync(Document document, CancellationToken cancellationToken);
}
