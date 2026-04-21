using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class UserLlmModelPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public LlmProviderType Usage { get; set; }

    public Guid LlmModelId { get; set; }
    public LlmModel LlmModel { get; set; } = null!;
}
