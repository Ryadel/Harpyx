namespace Harpyx.Application.DTOs;

public record WorkspaceDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    bool IsActive,
    bool IsChatLlmEnabled = false,
    Guid? ChatModelId = null,
    bool IsRagLlmEnabled = false,
    Guid? RagEmbeddingModelId = null,
    bool IsOcrLlmEnabled = false,
    Guid? OcrModelId = null);

public record CreateWorkspaceRequest(
    Guid CreatedByUserId,
    Guid TenantId,
    string Name,
    string? Description,
    bool IsActive,
    bool IsChatLlmEnabled = false,
    Guid? ChatModelId = null,
    bool IsRagLlmEnabled = false,
    Guid? RagEmbeddingModelId = null,
    bool IsOcrLlmEnabled = false,
    Guid? OcrModelId = null);
