using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record LlmProviderDto(
    Guid Id,
    LlmProvider Provider,
    LlmConnectionScope Scope,
    string? Name,
    string? Description,
    string? Notes,
    string? BaseUrl,
    bool IsConfigured,
    string? ApiKeyLast4,
    bool IsEnabled,
    bool SupportsChat,
    bool SupportsRagEmbedding,
    bool SupportsOcr,
    Guid? ChatModelId,
    string? ChatModel,
    Guid? RagEmbeddingModelId,
    string? RagEmbeddingModel,
    Guid? OcrModelId,
    string? OcrModel,
    bool IsDefaultChat,
    bool IsDefaultRagEmbedding,
    bool IsDefaultOcr,
    int UsedByProjectCount
)
{
    public string GetName()
        => string.IsNullOrWhiteSpace(Name)
            ? Provider.ToString()
            : $"{Name.Trim()} - {Provider}";

    public bool IsPersonal => Scope == LlmConnectionScope.Personal;
    public bool IsHosted => Scope == LlmConnectionScope.Hosted;
}

public record LlmProviderListDto(
    IReadOnlyList<LlmProviderDto> Providers,
    bool HasAnyChatConfigured,
    bool HasAnyRagConfigured,
    bool HasAnyOcrConfigured
);

public record LlmProviderSaveRequest(
    LlmProvider Provider,
    string? ApiKey,
    bool EnableChat,
    bool EnableRagEmbedding,
    bool EnableOcr,
    string? ChatModel,
    string? RagEmbeddingModel,
    string? OcrModel,
    bool SetAsDefaultChat,
    bool SetAsDefaultRagEmbedding,
    bool SetAsDefaultOcr,
    string? Name = null,
    string? Description = null,
    string? Notes = null,
    string? BaseUrl = null
)
{
    public string GetName()
        => string.IsNullOrWhiteSpace(Name)
            ? Provider.ToString()
            : $"{Name.Trim()} - {Provider}";
}

public record LlmProviderDeleteResult(
    bool Deleted,
    int AffectedProjectCount);

public record LlmProviderOptionDto(
    Guid Id,
    LlmConnectionScope Scope,
    Guid? UserId,
    string? UserEmail,
    LlmProvider Provider,
    string? Name,
    string? Model,
    bool IsDefault,
    bool IsEnabled,
    bool IsPublished,
    int? EmbeddingDimensions
)
{
    public string GetName()
    {
        var displayName = string.IsNullOrWhiteSpace(Name) ? Model : Name;
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName!;

        return Provider.ToString();
    }

    public string GroupName => Scope == LlmConnectionScope.Hosted
        ? "Hosted Models"
        : "Personal Models";
}

public record HostedLlmModelDto(
    Guid Id,
    LlmProviderType Capability,
    string DisplayName,
    string ModelId,
    string ConnectionName,
    int? EmbeddingDimensions,
    bool IsSelected,
    bool IsDefault);

public record HostedLlmModelSelectionRequest(
    Guid? ChatModelId,
    Guid? RagEmbeddingModelId,
    Guid? OcrModelId,
    bool SetAsDefaultChat,
    bool SetAsDefaultRagEmbedding,
    bool SetAsDefaultOcr);

public record LlmNotConfiguredResponse(
    string Code,
    string ProfileUrl
)
{
    public static LlmNotConfiguredResponse Instance { get; } = new("LLM_NOT_CONFIGURED", "/Profile");
}
