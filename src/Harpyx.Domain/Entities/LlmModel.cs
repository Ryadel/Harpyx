using Harpyx.Domain.Enums;

namespace Harpyx.Domain.Entities;

public class LlmModel : BaseEntity
{
    public Guid ConnectionId { get; set; }
    public LlmConnection Connection { get; set; } = null!;

    public LlmProviderType Capability { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? EmbeddingDimensions { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsEnabled { get; set; } = true;

    public ICollection<Project> ChatProjects { get; set; } = new List<Project>();
    public ICollection<Project> RagProjects { get; set; } = new List<Project>();
    public ICollection<Project> OcrProjects { get; set; } = new List<Project>();
    public ICollection<Workspace> ChatWorkspaces { get; set; } = new List<Workspace>();
    public ICollection<Workspace> RagWorkspaces { get; set; } = new List<Workspace>();
    public ICollection<Workspace> OcrWorkspaces { get; set; } = new List<Workspace>();
    public ICollection<UserLlmModelPreference> UserPreferences { get; set; } = new List<UserLlmModelPreference>();

    public string GetName()
        => string.IsNullOrWhiteSpace(DisplayName)
            ? ModelId
            : DisplayName.Trim();
}
