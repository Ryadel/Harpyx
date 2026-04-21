using Harpyx.Application.DTOs;
using Harpyx.Application.Defaults;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IWorkspaceRepository _workspaces;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentChunkRepository _chunks;
    private readonly ILlmCatalogRepository _catalog;
    private readonly IUsageLimitService _usageLimits;
    private readonly IJobQueue _jobQueue;
    private readonly IStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectService(
        IProjectRepository projects,
        IWorkspaceRepository workspaces,
        IDocumentRepository documents,
        IDocumentChunkRepository chunks,
        ILlmCatalogRepository catalog,
        IUsageLimitService usageLimits,
        IJobQueue jobQueue,
        IStorageService storage,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _workspaces = workspaces;
        _documents = documents;
        _chunks = chunks;
        _catalog = catalog;
        _usageLimits = usageLimits;
        _jobQueue = jobQueue;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaces.GetByIdAsync(request.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException("Workspace not found.");

        await _usageLimits.EnsureProjectCreationAllowedAsync(request.WorkspaceId, cancellationToken);
        await _usageLimits.EnsureProjectLifetimeAllowedAsync(request.WorkspaceId, null, request.LifetimePreset, cancellationToken);
        var normalized = NormalizeSettings(
            request.ChatLlmOverride,
            request.ChatModelId,
            request.RagLlmOverride,
            request.RagEmbeddingModelId,
            request.OcrLlmOverride,
            request.OcrModelId);
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? lifetimeExpiresAtUtc = request.LifetimePreset is null
            ? null
            : ProjectLifetimeDefaults.CalculateExpirationUtc(request.LifetimePreset.Value, now);

        if (normalized.ChatLlmOverride == LlmFeatureOverride.Enabled)
            await ValidateModelAsync(normalized.ChatModelId, LlmProviderType.Chat, cancellationToken);

        if (normalized.RagLlmOverride == LlmFeatureOverride.Enabled)
            await ValidateModelAsync(normalized.RagEmbeddingModelId, LlmProviderType.RagEmbedding, cancellationToken);

        if (normalized.OcrLlmOverride == LlmFeatureOverride.Enabled)
            await ValidateModelAsync(normalized.OcrModelId, LlmProviderType.Ocr, cancellationToken);

        var effectiveRagModelId = ResolveEffectiveRagModelId(
            normalized.RagLlmOverride,
            normalized.RagEmbeddingModelId,
            workspace);
        if (effectiveRagModelId is not null)
            await _usageLimits.EnsureRagIndexingAllowedAsync(workspace.TenantId, cancellationToken);

        var project = new Project
        {
            CreatedByUserId = request.CreatedByUserId,
            WorkspaceId = request.WorkspaceId,
            Name = request.Name,
            Description = request.Description,
            ChatLlmOverride = normalized.ChatLlmOverride,
            ChatModelId = normalized.ChatModelId,
            RagLlmOverride = normalized.RagLlmOverride,
            RagEmbeddingModelId = normalized.RagEmbeddingModelId,
            OcrLlmOverride = normalized.OcrLlmOverride,
            OcrModelId = normalized.OcrModelId,
            LifetimePreset = request.LifetimePreset,
            LifetimeExpiresAtUtc = lifetimeExpiresAtUtc,
            AutoExtendLifetimeOnActivity = request.LifetimePreset is not null && request.AutoExtendLifetimeOnActivity
        };

        await _projects.AddAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync(IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var projects = await _projects.GetAllAsync(tenantIds, cancellationToken);
        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(id, cancellationToken);
        return project is null ? null : ToDto(project);
    }

    public async Task<ProjectDto> UpdateAsync(ProjectDto project, CancellationToken cancellationToken)
    {
        var entity = await _projects.GetByIdAsync(project.Id, cancellationToken) ?? throw new InvalidOperationException("Project not found.");
        var workspaceChanged = entity.WorkspaceId != project.WorkspaceId;
        Workspace targetWorkspace;
        if (workspaceChanged)
        {
            targetWorkspace = await _workspaces.GetByIdAsync(project.WorkspaceId, cancellationToken)
                ?? throw new InvalidOperationException("Workspace not found.");
            await _usageLimits.EnsureProjectCreationAllowedAsync(project.WorkspaceId, cancellationToken);
        }
        else
        {
            targetWorkspace = entity.Workspace
                ?? await _workspaces.GetByIdAsync(entity.WorkspaceId, cancellationToken)
                ?? throw new InvalidOperationException("Workspace not found.");
        }
        await _usageLimits.EnsureProjectLifetimeAllowedAsync(project.WorkspaceId, entity.Id, project.LifetimePreset, cancellationToken);

        var normalized = NormalizeSettings(
            project.ChatLlmOverride,
            project.ChatModelId,
            project.RagLlmOverride,
            project.RagEmbeddingModelId,
            project.OcrLlmOverride,
            project.OcrModelId);

        if (normalized.ChatLlmOverride == LlmFeatureOverride.Enabled)
            await ValidateModelAsync(normalized.ChatModelId, LlmProviderType.Chat, cancellationToken);

        if (normalized.RagLlmOverride == LlmFeatureOverride.Enabled)
            await ValidateModelAsync(normalized.RagEmbeddingModelId, LlmProviderType.RagEmbedding, cancellationToken);

        if (normalized.OcrLlmOverride == LlmFeatureOverride.Enabled)
            await ValidateModelAsync(normalized.OcrModelId, LlmProviderType.Ocr, cancellationToken);

        var oldEffectiveRagModelId = ResolveEffectiveRagModelId(entity);
        var newEffectiveRagModelId = ResolveEffectiveRagModelId(
            normalized.RagLlmOverride,
            normalized.RagEmbeddingModelId,
            targetWorkspace);
        if (newEffectiveRagModelId is not null)
            await _usageLimits.EnsureRagIndexingAllowedAsync(targetWorkspace.TenantId, cancellationToken);

        var ragProviderChanged = oldEffectiveRagModelId != newEffectiveRagModelId;

        if (ragProviderChanged && !project.ConfirmRagReindex)
            throw new InvalidOperationException("RAG embedding model changed. Confirmation required before reindexing.");
        var now = DateTimeOffset.UtcNow;
        var lifetimePresetChanged = entity.LifetimePreset != project.LifetimePreset;
        var autoExtendLifetimeOnActivity = project.LifetimePreset is not null && project.AutoExtendLifetimeOnActivity;

        entity.WorkspaceId = project.WorkspaceId;
        entity.Workspace = targetWorkspace;
        entity.Name = project.Name;
        entity.Description = project.Description;
        entity.ChatLlmOverride = normalized.ChatLlmOverride;
        entity.ChatModelId = normalized.ChatModelId;
        entity.RagLlmOverride = normalized.RagLlmOverride;
        entity.RagEmbeddingModelId = normalized.RagEmbeddingModelId;
        entity.OcrLlmOverride = normalized.OcrLlmOverride;
        entity.OcrModelId = normalized.OcrModelId;
        entity.LifetimePreset = project.LifetimePreset;
        entity.AutoExtendLifetimeOnActivity = autoExtendLifetimeOnActivity;
        if (project.LifetimePreset is null)
        {
            entity.LifetimeExpiresAtUtc = null;
        }
        else if (lifetimePresetChanged || entity.LifetimeExpiresAtUtc is null)
        {
            entity.LifetimeExpiresAtUtc = ProjectLifetimeDefaults.CalculateExpirationUtc(project.LifetimePreset.Value, now);
        }
        else if (autoExtendLifetimeOnActivity &&
                 entity.LifetimeExpiresAtUtc.Value > now)
        {
            entity.LifetimeExpiresAtUtc = ProjectLifetimeDefaults.CalculateExpirationUtc(project.LifetimePreset.Value, now);
        }
        if (ragProviderChanged)
        {
            entity.RagIndexVersion++;
        }

        entity.UpdatedAt = now;
        _projects.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (ragProviderChanged)
        {
            await _chunks.RemoveByProjectIdAsync(entity.Id, cancellationToken);
            var documents = await _documents.GetByProjectAsync(entity.Id, cancellationToken);
            foreach (var document in documents)
            {
                if (document.State is DocumentState.Quarantined or DocumentState.Rejected)
                    continue;

                document.State = DocumentState.Queued;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                _documents.Update(document);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var document in documents)
            {
                if (document.State is DocumentState.Quarantined or DocumentState.Rejected)
                    continue;

                await _jobQueue.EnqueueParseJobAsync(document.Id, cancellationToken);
            }
        }

        return ToDto(entity);
    }

    public async Task TouchLifetimeAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _projects.GetByIdAsync(id, cancellationToken);
        if (entity is null ||
            entity.LifetimePreset is null ||
            !entity.AutoExtendLifetimeOnActivity ||
            entity.LifetimeExpiresAtUtc is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (entity.LifetimeExpiresAtUtc.Value <= now)
            return;

        var nextExpiry = ProjectLifetimeDefaults.CalculateExpirationUtc(entity.LifetimePreset.Value, now);
        if (nextExpiry <= entity.LifetimeExpiresAtUtc.Value)
            return;

        entity.LifetimeExpiresAtUtc = nextExpiry;
        entity.UpdatedAt = now;
        _projects.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _projects.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Project not found.");
        var documents = await _documents.GetByProjectAsync(entity.Id, cancellationToken);
        await _chunks.RemoveByProjectIdAsync(entity.Id, cancellationToken);
        foreach (var document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.StorageKey))
                continue;

            try
            {
                await _storage.DeleteAsync(document.StorageKey, cancellationToken);
            }
            catch
            {
                // Best effort storage cleanup.
            }
        }

        _projects.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateModelAsync(Guid? modelId, LlmProviderType usage, CancellationToken cancellationToken)
    {
        if (modelId is null || modelId == Guid.Empty)
            throw new InvalidOperationException($"No {usage} model selected.");

        var model = await _catalog.GetModelByIdAsync(modelId.Value, cancellationToken);
        if (model is null)
            throw new InvalidOperationException($"Selected {usage} model not found.");

        if (model.Capability != usage)
            throw new InvalidOperationException($"Selected model is not configured for {usage}.");

        if (!IsConfiguredModel(model))
            throw new InvalidOperationException($"Selected {usage} model is not configured.");
    }

    private static (
        LlmFeatureOverride ChatLlmOverride,
        Guid? ChatModelId,
        LlmFeatureOverride RagLlmOverride,
        Guid? RagEmbeddingModelId,
        LlmFeatureOverride OcrLlmOverride,
        Guid? OcrModelId) NormalizeSettings(
        LlmFeatureOverride chatLlmOverride,
        Guid? ChatModelId,
        LlmFeatureOverride ragLlmOverride,
        Guid? RagEmbeddingModelId,
        LlmFeatureOverride ocrLlmOverride,
        Guid? OcrModelId)
    {
        if (chatLlmOverride != LlmFeatureOverride.Enabled)
            ChatModelId = null;
        if (ragLlmOverride != LlmFeatureOverride.Enabled)
            RagEmbeddingModelId = null;
        if (ocrLlmOverride != LlmFeatureOverride.Enabled)
            OcrModelId = null;

        if (chatLlmOverride == LlmFeatureOverride.Enabled && (ChatModelId is null || ChatModelId == Guid.Empty))
            throw new InvalidOperationException("Chat is enabled but no Chat model was selected.");

        if (ragLlmOverride == LlmFeatureOverride.Enabled && (RagEmbeddingModelId is null || RagEmbeddingModelId == Guid.Empty))
            throw new InvalidOperationException("RAG is enabled but no RAG model was selected.");

        if (ocrLlmOverride == LlmFeatureOverride.Enabled && (OcrModelId is null || OcrModelId == Guid.Empty))
            throw new InvalidOperationException("OCR LLM is enabled but no OCR model was selected.");

        return (chatLlmOverride, ChatModelId, ragLlmOverride, RagEmbeddingModelId, ocrLlmOverride, OcrModelId);
    }

    private static Guid? ResolveEffectiveRagModelId(Project project)
        => ResolveEffectiveRagModelId(project.RagLlmOverride, project.RagEmbeddingModelId, project.Workspace);

    private static Guid? ResolveEffectiveRagModelId(
        LlmFeatureOverride ragLlmOverride,
        Guid? RagEmbeddingModelId,
        Workspace workspace)
    {
        var workspaceRagModelId = workspace.IsRagLlmEnabled &&
                                     workspace.RagEmbeddingModelId is not null &&
                                     (workspace.RagEmbeddingModel is null ||
                                      IsConfiguredModel(workspace.RagEmbeddingModel))
            ? workspace.RagEmbeddingModelId
            : null;

        return ragLlmOverride switch
        {
            LlmFeatureOverride.Enabled => RagEmbeddingModelId,
            LlmFeatureOverride.Disabled => null,
            _ => workspaceRagModelId
        };
    }

    private static ProjectDto ToDto(Project project)
    {
        var workspaceChatEnabled = project.Workspace is not null &&
                                   project.Workspace.IsChatLlmEnabled &&
                                   project.Workspace.ChatModelId is not null &&
                                   (project.Workspace.ChatModel is null ||
                                    IsConfiguredModel(project.Workspace.ChatModel));

        var workspaceRagEnabled = project.Workspace is not null &&
                                  project.Workspace.IsRagLlmEnabled &&
                                  project.Workspace.RagEmbeddingModelId is not null &&
                                  (project.Workspace.RagEmbeddingModel is null ||
                                   IsConfiguredModel(project.Workspace.RagEmbeddingModel));

        var workspaceOcrEnabled = project.Workspace is not null &&
                                  project.Workspace.IsOcrLlmEnabled &&
                                  project.Workspace.OcrModelId is not null &&
                                  (project.Workspace.OcrModel is null ||
                                   IsConfiguredModel(project.Workspace.OcrModel));

        var projectChatModelConfigured = project.ChatModelId is not null &&
                                            (project.ChatModel is null ||
                                             IsConfiguredModel(project.ChatModel));

        var projectRagModelConfigured = project.RagEmbeddingModelId is not null &&
                                           (project.RagEmbeddingModel is null ||
                                            IsConfiguredModel(project.RagEmbeddingModel));

        var projectOcrModelConfigured = project.OcrModelId is not null &&
                                           (project.OcrModel is null ||
                                           IsConfiguredModel(project.OcrModel));

        var chatOverride = project.ChatLlmOverride;
        if (chatOverride == LlmFeatureOverride.Enabled && !projectChatModelConfigured)
            chatOverride = LlmFeatureOverride.Disabled;
        else if (chatOverride == LlmFeatureOverride.WorkspaceDefault && project.Workspace is not null && !workspaceChatEnabled)
            chatOverride = LlmFeatureOverride.Disabled;

        var ragOverride = project.RagLlmOverride;
        if (ragOverride == LlmFeatureOverride.Enabled && !projectRagModelConfigured)
            ragOverride = LlmFeatureOverride.Disabled;
        else if (ragOverride == LlmFeatureOverride.WorkspaceDefault && project.Workspace is not null && !workspaceRagEnabled)
            ragOverride = LlmFeatureOverride.Disabled;

        var ocrOverride = project.OcrLlmOverride;
        if (ocrOverride == LlmFeatureOverride.Enabled && !projectOcrModelConfigured)
            ocrOverride = LlmFeatureOverride.Disabled;
        else if (ocrOverride == LlmFeatureOverride.WorkspaceDefault && project.Workspace is not null && !workspaceOcrEnabled)
            ocrOverride = LlmFeatureOverride.Disabled;

        return new ProjectDto(
            project.Id,
            project.WorkspaceId,
            project.Name,
            project.Description,
            chatOverride,
            chatOverride == LlmFeatureOverride.Enabled ? project.ChatModelId : null,
            ragOverride,
            ragOverride == LlmFeatureOverride.Enabled ? project.RagEmbeddingModelId : null,
            ocrOverride,
            ocrOverride == LlmFeatureOverride.Enabled ? project.OcrModelId : null,
            project.LifetimePreset,
            project.LifetimeExpiresAtUtc,
            project.AutoExtendLifetimeOnActivity);
    }

    private static bool IsConfiguredModel(LlmModel model)
        => model.IsEnabled &&
           model.Connection.IsEnabled &&
           model.Connection.Provider != LlmProvider.None &&
           (model.Connection.Scope == LlmConnectionScope.Hosted
                ? model.IsPublished
                : !string.IsNullOrWhiteSpace(model.Connection.EncryptedApiKey));
}
