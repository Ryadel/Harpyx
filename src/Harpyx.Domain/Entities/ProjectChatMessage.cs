namespace Harpyx.Domain.Entities;

public class ProjectChatMessage : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset MessageTimestamp { get; set; } = DateTimeOffset.UtcNow;
}
