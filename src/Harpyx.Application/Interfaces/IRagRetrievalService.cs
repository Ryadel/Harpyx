using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IRagRetrievalService
{
    Task<RagContextResult> BuildContextAsync(
        Guid projectId,
        IReadOnlyList<Guid> documentIds,
        string query,
        CancellationToken cancellationToken);
}
