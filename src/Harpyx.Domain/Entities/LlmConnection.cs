using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class LlmConnection : BaseEntity
{
    public LlmConnectionScope Scope { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public LlmProvider Provider { get; set; } = LlmProvider.None;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public string? BaseUrl { get; set; }
    public string? EncryptedApiKey { get; set; }
    public string? ApiKeyLast4 { get; set; }
    public bool IsEnabled { get; set; } = true;

    public ICollection<LlmModel> Models { get; set; } = new List<LlmModel>();

    public string GetName()
        => string.IsNullOrWhiteSpace(Name)
            ? Provider.ToString()
            : Name.Trim();
}
