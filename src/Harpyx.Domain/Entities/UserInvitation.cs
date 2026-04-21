using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class UserInvitation : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public UserInvitationScope Scope { get; set; } = UserInvitationScope.SelfRegistration;
    public UserInvitationStatus Status { get; set; } = UserInvitationStatus.Pending;
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid InvitedByUserId { get; set; }
    public User InvitedByUser { get; set; } = null!;
    public Guid? AcceptedUserId { get; set; }
    public User? AcceptedUser { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}
