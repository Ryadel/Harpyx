using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IProjectService
{
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectDto>> GetAllAsync(IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken);
    Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProjectDto> UpdateAsync(ProjectDto project, CancellationToken cancellationToken);
    Task TouchLifetimeAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
