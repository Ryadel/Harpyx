using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class ProjectPrompt : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public ProjectPromptType PromptType { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
}
