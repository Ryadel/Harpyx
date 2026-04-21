using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record ProjectDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Description,
    LlmFeatureOverride ChatLlmOverride = LlmFeatureOverride.WorkspaceDefault,
    Guid? ChatModelId = null,
    LlmFeatureOverride RagLlmOverride = LlmFeatureOverride.WorkspaceDefault,
    Guid? RagEmbeddingModelId = null,
    LlmFeatureOverride OcrLlmOverride = LlmFeatureOverride.WorkspaceDefault,
    Guid? OcrModelId = null,
    ProjectLifetimePreset? LifetimePreset = ProjectLifetimePreset.Day30,
    DateTimeOffset? LifetimeExpiresAtUtc = null,
    bool AutoExtendLifetimeOnActivity = true,
    bool ConfirmRagReindex = false);

public record CreateProjectRequest(
    Guid WorkspaceId,
    string Name,
    string? Description,
    LlmFeatureOverride ChatLlmOverride = LlmFeatureOverride.WorkspaceDefault,
    Guid? ChatModelId = null,
    LlmFeatureOverride RagLlmOverride = LlmFeatureOverride.WorkspaceDefault,
    Guid? RagEmbeddingModelId = null,
    LlmFeatureOverride OcrLlmOverride = LlmFeatureOverride.WorkspaceDefault,
    Guid? OcrModelId = null,
    ProjectLifetimePreset? LifetimePreset = null,
    bool AutoExtendLifetimeOnActivity = true,
    bool ConfirmRagReindex = false,
    Guid? CreatedByUserId = null);
