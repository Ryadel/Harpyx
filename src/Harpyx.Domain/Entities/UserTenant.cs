using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class UserTenant
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public TenantRole TenantRole { get; set; } = TenantRole.Viewer;
    public bool CanGrant { get; set; }
    public Guid? GrantedByUserId { get; set; }
    public User? GrantedByUser { get; set; }
    public DateTimeOffset? GrantedAt { get; set; }
}
