using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class Project : BaseEntity
{
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public LlmFeatureOverride ChatLlmOverride { get; set; } = LlmFeatureOverride.WorkspaceDefault;
    public Guid? ChatModelId { get; set; }
    public LlmModel? ChatModel { get; set; }
    public LlmFeatureOverride RagLlmOverride { get; set; } = LlmFeatureOverride.WorkspaceDefault;
    public Guid? RagEmbeddingModelId { get; set; }
    public LlmModel? RagEmbeddingModel { get; set; }
    public LlmFeatureOverride OcrLlmOverride { get; set; } = LlmFeatureOverride.WorkspaceDefault;
    public Guid? OcrModelId { get; set; }
    public LlmModel? OcrModel { get; set; }
    public ProjectLifetimePreset? LifetimePreset { get; set; }
    public DateTimeOffset? LifetimeExpiresAtUtc { get; set; }
    public bool AutoExtendLifetimeOnActivity { get; set; } = true;
    public int RagIndexVersion { get; set; } = 1;
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<ProjectPrompt> Prompts { get; set; } = new List<ProjectPrompt>();
    public ICollection<ProjectChatMessage> ChatMessages { get; set; } = new List<ProjectChatMessage>();
}
