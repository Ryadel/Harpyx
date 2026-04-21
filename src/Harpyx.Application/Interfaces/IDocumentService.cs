using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IDocumentService
{
    Task<DocumentDto> UploadAsync(UploadDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDto> AddUrlAsync(AddUrlDocumentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentDto>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<DocumentDto?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken);
    Task RenameAsync(Guid documentId, string fileName, CancellationToken cancellationToken);
    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken);
    Task<bool> RetryAsync(Guid documentId, CancellationToken cancellationToken);
}
