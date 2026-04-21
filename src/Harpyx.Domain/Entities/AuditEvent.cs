namespace Harpyx.Domain.Entities;

public class AuditEvent : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string? UserObjectId { get; set; }
    public string? UserEmail { get; set; }
    public string? Details { get; set; }
}
