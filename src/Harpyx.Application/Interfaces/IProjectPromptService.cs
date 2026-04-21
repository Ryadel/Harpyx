using Harpyx.Application.DTOs;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Interfaces;

public interface IProjectPromptService
{
    Task<ProjectPromptCollectionDto> GetProjectPromptsAsync(Guid projectId, ProjectPromptType promptType, CancellationToken cancellationToken);
    Task<ProjectPromptDto?> GetByIdAsync(Guid promptId, CancellationToken cancellationToken);
    Task<ProjectPromptDto?> GetLastUsedAsync(Guid projectId, ProjectPromptType promptType, CancellationToken cancellationToken);
    Task<ProjectPromptDto?> SavePromptUsageAsync(Guid projectId, ProjectPromptType promptType, string? content, CancellationToken cancellationToken);
    Task<bool> ToggleFavoriteAsync(Guid promptId, CancellationToken cancellationToken);
}
