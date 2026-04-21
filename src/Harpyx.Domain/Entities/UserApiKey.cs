using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class UserApiKey : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string KeySalt { get; set; } = string.Empty;
    public int KeyHashIterations { get; set; }
    public string KeyPreview { get; set; } = string.Empty;
    public string KeyLast4 { get; set; } = string.Empty;
    public ApiPermission Permissions { get; set; } = ApiPermission.None;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
}
