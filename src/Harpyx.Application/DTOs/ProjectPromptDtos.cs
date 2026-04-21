using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record ProjectPromptDto(
    Guid Id,
    Guid ProjectId,
    ProjectPromptType PromptType,
    string Content,
    bool IsFavorite,
    DateTimeOffset LastUsedAt);

public record ProjectPromptCollectionDto(
    IReadOnlyList<ProjectPromptDto> Favorites,
    IReadOnlyList<ProjectPromptDto> History)
{
    public static ProjectPromptCollectionDto Empty { get; } =
        new(Array.Empty<ProjectPromptDto>(), Array.Empty<ProjectPromptDto>());
}
