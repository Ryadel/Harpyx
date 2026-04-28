using Harpyx.Domain.Enums;
using System.Collections.Generic;

namespace Harpyx.Domain.Entities;

public class User : BaseEntity
{
    public string? ObjectId { get; set; }
    public string? SubjectId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Standard;
    public bool IsActive { get; set; } = true;
    public string? LastLoginProvider { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<UserTenant> TenantAssignments { get; set; } = new List<UserTenant>();
    public ICollection<LlmConnection> LlmConnections { get; set; } = new List<LlmConnection>();
    public ICollection<UserLlmModelPreference> LlmModelPreferences { get; set; } = new List<UserLlmModelPreference>();
    public ICollection<UserApiKey> ApiKeys { get; set; } = new List<UserApiKey>();
    public ICollection<UserInvitation> SentInvitations { get; set; } = new List<UserInvitation>();
    public ICollection<UserInvitation> AcceptedInvitations { get; set; } = new List<UserInvitation>();
    public ICollection<UserTenant> GrantedTenantMemberships { get; set; } = new List<UserTenant>();
}
