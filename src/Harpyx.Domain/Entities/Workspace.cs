namespace Harpyx.Domain.Entities;

public class Workspace : BaseEntity
{
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsChatLlmEnabled { get; set; }
    public Guid? ChatModelId { get; set; }
    public LlmModel? ChatModel { get; set; }
    public bool IsRagLlmEnabled { get; set; }
    public Guid? RagEmbeddingModelId { get; set; }
    public LlmModel? RagEmbeddingModel { get; set; }
    public bool IsOcrLlmEnabled { get; set; }
    public Guid? OcrModelId { get; set; }
    public LlmModel? OcrModel { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
