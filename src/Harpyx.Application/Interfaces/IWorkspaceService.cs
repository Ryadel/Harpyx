using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken);
    Task<WorkspaceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken);
    Task<WorkspaceDto> UpdateAsync(WorkspaceDto workspace, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
