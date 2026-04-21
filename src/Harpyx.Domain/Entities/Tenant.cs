namespace Harpyx.Domain.Entities;

public class Tenant : BaseEntity
{
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsVisibleToAllUsers { get; set; } = false;
    public bool IsPersonal { get; set; } = false;

    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
    public ICollection<UserTenant> UserAssignments { get; set; } = new List<UserTenant>();
    public ICollection<UserInvitation> Invitations { get; set; } = new List<UserInvitation>();
}
