using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaces;
    private readonly ILlmCatalogRepository _catalog;
    private readonly IUsageLimitService _usageLimits;
    private readonly IUnitOfWork _unitOfWork;

    public WorkspaceService(
        IWorkspaceRepository workspaces,
        ILlmCatalogRepository catalog,
        IUsageLimitService usageLimits,
        IUnitOfWork unitOfWork)
    {
        _workspaces = workspaces;
        _catalog = catalog;
        _usageLimits = usageLimits;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var workspaces = await _workspaces.GetAllAsync(tenantIds, cancellationToken);
        return workspaces.Select(ToDto).ToList();
    }

    public async Task<WorkspaceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await _workspaces.GetByIdAsync(id, cancellationToken);
        return workspace is null ? null : ToDto(workspace);
    }

    public async Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        await _usageLimits.EnsureWorkspaceCreationAllowedAsync(request.CreatedByUserId, cancellationToken);
        var normalized = await NormalizeAndValidateAsync(
            request.TenantId,
            request.IsChatLlmEnabled,
            request.ChatModelId,
            request.IsRagLlmEnabled,
            request.RagEmbeddingModelId,
            request.IsOcrLlmEnabled,
            request.OcrModelId,
            cancellationToken);

        var workspace = new Workspace
        {
            CreatedByUserId = request.CreatedByUserId,
            TenantId = request.TenantId,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            IsChatLlmEnabled = normalized.IsChatLlmEnabled,
            ChatModelId = normalized.ChatModelId,
            IsRagLlmEnabled = normalized.IsRagLlmEnabled,
            RagEmbeddingModelId = normalized.RagEmbeddingModelId,
            IsOcrLlmEnabled = normalized.IsOcrLlmEnabled,
            OcrModelId = normalized.OcrModelId
        };

        await _workspaces.AddAsync(workspace, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(workspace);
    }

    public async Task<WorkspaceDto> UpdateAsync(WorkspaceDto workspace, CancellationToken cancellationToken)
    {
        var entity = await _workspaces.GetByIdAsync(workspace.Id, cancellationToken) ??
            throw new InvalidOperationException("Workspace not found.");
        var normalized = await NormalizeAndValidateAsync(
            workspace.TenantId,
            workspace.IsChatLlmEnabled,
            workspace.ChatModelId,
            workspace.IsRagLlmEnabled,
            workspace.RagEmbeddingModelId,
            workspace.IsOcrLlmEnabled,
            workspace.OcrModelId,
            cancellationToken);

        entity.TenantId = workspace.TenantId;
        entity.Name = workspace.Name;
        entity.Description = workspace.Description;
        entity.IsActive = workspace.IsActive;
        entity.IsChatLlmEnabled = normalized.IsChatLlmEnabled;
        entity.ChatModelId = normalized.ChatModelId;
        entity.IsRagLlmEnabled = normalized.IsRagLlmEnabled;
        entity.RagEmbeddingModelId = normalized.RagEmbeddingModelId;
        entity.IsOcrLlmEnabled = normalized.IsOcrLlmEnabled;
        entity.OcrModelId = normalized.OcrModelId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        _workspaces.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _workspaces.GetByIdAsync(id, cancellationToken) ??
            throw new InvalidOperationException("Workspace not found.");

        _workspaces.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<(
        bool IsChatLlmEnabled,
        Guid? ChatModelId,
        bool IsRagLlmEnabled,
        Guid? RagEmbeddingModelId,
        bool IsOcrLlmEnabled,
        Guid? OcrModelId)> NormalizeAndValidateAsync(
        Guid tenantId,
        bool isChatLlmEnabled,
        Guid? ChatModelId,
        bool isRagLlmEnabled,
        Guid? RagEmbeddingModelId,
        bool isOcrLlmEnabled,
        Guid? OcrModelId,
        CancellationToken cancellationToken)
    {
        if (!isChatLlmEnabled)
            ChatModelId = null;

        if (!isRagLlmEnabled)
            RagEmbeddingModelId = null;

        if (!isOcrLlmEnabled)
            OcrModelId = null;

        if (isChatLlmEnabled)
        {
            if (ChatModelId is null || ChatModelId == Guid.Empty)
                throw new InvalidOperationException("Chat LLM is enabled but no Chat model is selected.");

            await ValidateModelAsync(ChatModelId.Value, LlmProviderType.Chat, cancellationToken);
        }

        if (isRagLlmEnabled)
        {
            if (RagEmbeddingModelId is null || RagEmbeddingModelId == Guid.Empty)
                throw new InvalidOperationException("RAG LLM is enabled but no RAG embedding model is selected.");

            await _usageLimits.EnsureRagIndexingAllowedAsync(tenantId, cancellationToken);
            await ValidateModelAsync(RagEmbeddingModelId.Value, LlmProviderType.RagEmbedding, cancellationToken);
        }

        if (isOcrLlmEnabled)
        {
            if (OcrModelId is null || OcrModelId == Guid.Empty)
                throw new InvalidOperationException("OCR LLM is enabled but no OCR model is selected.");

            await ValidateModelAsync(OcrModelId.Value, LlmProviderType.Ocr, cancellationToken);
        }

        return (isChatLlmEnabled, ChatModelId, isRagLlmEnabled, RagEmbeddingModelId, isOcrLlmEnabled, OcrModelId);
    }

    private async Task ValidateModelAsync(Guid modelId, LlmProviderType usage, CancellationToken cancellationToken)
    {
        var model = await _catalog.GetModelByIdAsync(modelId, cancellationToken)
            ?? throw new InvalidOperationException("Selected model not found.");

        if (model.Capability != usage)
            throw new InvalidOperationException($"Selected model is not configured for {usage}.");

        if (!IsConfiguredModel(model))
            throw new InvalidOperationException("Selected model is not configured.");
    }

    private static WorkspaceDto ToDto(Workspace workspace)
    {
        var chatEnabled = workspace.IsChatLlmEnabled &&
            workspace.ChatModelId is not null &&
            (workspace.ChatModel is null ||
             IsConfiguredModel(workspace.ChatModel));

        var ragEnabled = workspace.IsRagLlmEnabled &&
            workspace.RagEmbeddingModelId is not null &&
            (workspace.RagEmbeddingModel is null ||
             IsConfiguredModel(workspace.RagEmbeddingModel));

        var ocrEnabled = workspace.IsOcrLlmEnabled &&
            workspace.OcrModelId is not null &&
            (workspace.OcrModel is null ||
             IsConfiguredModel(workspace.OcrModel));

        return new WorkspaceDto(
            workspace.Id,
            workspace.TenantId,
            workspace.Name,
            workspace.Description,
            workspace.IsActive,
            chatEnabled,
            chatEnabled ? workspace.ChatModelId : null,
            ragEnabled,
            ragEnabled ? workspace.RagEmbeddingModelId : null,
            ocrEnabled,
            ocrEnabled ? workspace.OcrModelId : null);
    }

    private static bool IsConfiguredModel(LlmModel model)
        => model.IsEnabled &&
           model.Connection.IsEnabled &&
           model.Connection.Provider != LlmProvider.None &&
           (model.Connection.Scope == LlmConnectionScope.Hosted
                ? model.IsPublished
                : !string.IsNullOrWhiteSpace(model.Connection.EncryptedApiKey));
}
