using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class UserLlmProviderService : IUserLlmProviderService
{
    private static readonly HashSet<LlmProvider> RagEmbeddingProviders = new()
    {
        LlmProvider.OpenAI,
        LlmProvider.Google,
        LlmProvider.OpenAICompatible,
        LlmProvider.AzureOpenAI
    };

    private readonly ILlmCatalogRepository _catalog;
    private readonly IProjectRepository _projects;
    private readonly IWorkspaceRepository _workspaces;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IDocumentRepository _documents;
    private readonly IEncryptionService _encryption;
    private readonly ILlmConnectionSmokeTestService _smokeTest;
    private readonly IUsageLimitService _usageLimits;
    private readonly IJobQueue _jobQueue;
    private readonly IUnitOfWork _unitOfWork;

    public UserLlmProviderService(
        ILlmCatalogRepository catalog,
        IProjectRepository projects,
        IWorkspaceRepository workspaces,
        IDocumentChunkRepository chunks,
        IDocumentRepository documents,
        IEncryptionService encryption,
        ILlmConnectionSmokeTestService smokeTest,
        IUsageLimitService usageLimits,
        IJobQueue jobQueue,
        IUnitOfWork unitOfWork)
    {
        _catalog = catalog;
        _projects = projects;
        _workspaces = workspaces;
        _chunks = chunks;
        _documents = documents;
        _encryption = encryption;
        _smokeTest = smokeTest;
        _usageLimits = usageLimits;
        _jobQueue = jobQueue;
        _unitOfWork = unitOfWork;
    }

    public async Task<LlmProviderListDto> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connections = await _catalog.GetPersonalConnectionsByUserAsync(userId, cancellationToken);
        var preferences = await _catalog.GetPreferencesByUserAsync(userId, cancellationToken);
        var preferenceByUsage = preferences.ToDictionary(p => p.Usage, p => p.LlmModelId);
        var dtos = new List<LlmProviderDto>();

        foreach (var connection in connections)
        {
            var models = connection.Models.ToList();
            var chatModel = models.FirstOrDefault(m => m.Capability == LlmProviderType.Chat);
            var ragModel = models.FirstOrDefault(m => m.Capability == LlmProviderType.RagEmbedding);
            var ocrModel = models.FirstOrDefault(m => m.Capability == LlmProviderType.Ocr);
            var usageCount = ragModel is null
                ? 0
                : (await _projects.GetByRagModelIdAsync(ragModel.Id, cancellationToken)).Count;

            dtos.Add(ToDto(connection, chatModel, ragModel, ocrModel, preferenceByUsage, usageCount));
        }

        var hasAnyChat = dtos.Any(p => p.SupportsChat && p.IsConfigured);
        var hasAnyRag = dtos.Any(p => p.SupportsRagEmbedding && p.IsConfigured);
        var hasAnyOcr = dtos.Any(p => p.SupportsOcr && p.IsConfigured);
        return new LlmProviderListDto(dtos, hasAnyChat, hasAnyRag, hasAnyOcr);
    }

    public async Task<LlmProviderDto> SaveAsync(Guid userId, LlmProviderSaveRequest request, CancellationToken cancellationToken)
    {
        if (request.Provider is LlmProvider.None or LlmProvider.OpenAICompatible)
            throw new ArgumentException("Please select a supported personal cloud provider.");

        if (RequiresBaseUrl(request.Provider) && string.IsNullOrWhiteSpace(request.BaseUrl))
            throw new ArgumentException($"{request.GetName()} requires an endpoint/base URL.");

        if (!request.EnableChat && !request.EnableRagEmbedding && !request.EnableOcr)
            throw new ArgumentException("At least one capability must be enabled (Chat, RAG Embedding, and/or OCR).");

        if (request.EnableRagEmbedding && !RagEmbeddingProviders.Contains(request.Provider))
            throw new ArgumentException($"{request.GetName()} is not supported for RAG embeddings.");

        if (request.EnableOcr && !SupportsOcr(request.Provider))
            throw new ArgumentException($"{request.GetName()} is not supported for OCR.");

        var connection = await _catalog.GetPersonalConnectionByProviderAsync(userId, request.Provider, cancellationToken);
        var isNew = connection is null;
        if (isNew)
            await _usageLimits.EnsureLlmProviderCreationAllowedAsync(userId, cancellationToken);

        var effectiveApiKey = string.IsNullOrWhiteSpace(request.ApiKey)
            ? null
            : request.ApiKey.Trim();

        if (isNew && string.IsNullOrWhiteSpace(effectiveApiKey))
            throw new ArgumentException("An API key is required when creating a provider.");

        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            if (connection is null || string.IsNullOrWhiteSpace(connection.EncryptedApiKey))
                throw new ArgumentException("An API key is required when no existing key is available.");

            effectiveApiKey = _encryption.Decrypt(connection.EncryptedApiKey);
        }

        await _smokeTest.ValidateAsync(
            request.Provider,
            effectiveApiKey!,
            request.BaseUrl,
            request.EnableChat,
            request.ChatModel,
            request.EnableRagEmbedding,
            request.RagEmbeddingModel,
            request.EnableOcr,
            request.OcrModel,
            request.GetName(),
            cancellationToken);

        connection ??= new LlmConnection
        {
            UserId = userId,
            Scope = LlmConnectionScope.Personal,
            Provider = request.Provider
        };

        connection.Name = NormalizeName(request.Name);
        connection.Description = NormalizeText(request.Description);
        connection.Notes = NormalizeText(request.Notes);
        connection.BaseUrl = RequiresBaseUrl(connection.Provider)
            ? NormalizeBaseUrl(request.BaseUrl)
            : null;
        connection.IsEnabled = true;
        connection.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.ApiKey) || string.IsNullOrWhiteSpace(connection.EncryptedApiKey))
        {
            connection.EncryptedApiKey = _encryption.Encrypt(effectiveApiKey!);
            connection.ApiKeyLast4 = Last4(effectiveApiKey!);
        }

        if (isNew)
            await _catalog.AddConnectionAsync(connection, cancellationToken);
        else
            _catalog.UpdateConnection(connection);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var chatModel = await UpsertCapabilityModelAsync(
            connection,
            LlmProviderType.Chat,
            request.EnableChat,
            NormalizeModel(request.ChatModel) ?? DefaultChatModel(request.Provider),
            cancellationToken);

        var ragModel = await UpsertCapabilityModelAsync(
            connection,
            LlmProviderType.RagEmbedding,
            request.EnableRagEmbedding,
            NormalizeModel(request.RagEmbeddingModel) ?? DefaultEmbeddingModel(request.Provider),
            cancellationToken);

        var ocrModel = await UpsertCapabilityModelAsync(
            connection,
            LlmProviderType.Ocr,
            request.EnableOcr,
            NormalizeModel(request.OcrModel) ?? DefaultOcrModel(request.Provider),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.SetAsDefaultChat && chatModel is not null)
            await SetDefaultAsync(userId, LlmProviderType.Chat, chatModel.Id, cancellationToken);
        if (request.SetAsDefaultRagEmbedding && ragModel is not null)
            await SetDefaultAsync(userId, LlmProviderType.RagEmbedding, ragModel.Id, cancellationToken);
        if (request.SetAsDefaultOcr && ocrModel is not null)
            await SetDefaultAsync(userId, LlmProviderType.Ocr, ocrModel.Id, cancellationToken);

        var preferences = await _catalog.GetPreferencesByUserAsync(userId, cancellationToken);
        var preferenceByUsage = preferences.ToDictionary(p => p.Usage, p => p.LlmModelId);
        var usageCount = ragModel is null ? 0 : (await _projects.GetByRagModelIdAsync(ragModel.Id, cancellationToken)).Count;
        return ToDto(connection, chatModel, ragModel, ocrModel, preferenceByUsage, usageCount);
    }

    public async Task<LlmProviderDeleteResult> DeleteAsync(Guid userId, LlmProvider provider, CancellationToken cancellationToken)
    {
        var connection = await _catalog.GetPersonalConnectionByProviderAsync(userId, provider, cancellationToken);
        if (connection is null)
            return new LlmProviderDeleteResult(false, 0);

        var affectedProjectIds = new HashSet<Guid>();
        var documentsToRequeue = new HashSet<Guid>();
        var models = connection.Models.ToList();

        foreach (var model in models)
        {
            var chatProjects = await _projects.GetByChatModelIdAsync(model.Id, cancellationToken);
            foreach (var project in chatProjects)
            {
                project.ChatLlmOverride = LlmFeatureOverride.Disabled;
                project.ChatModelId = null;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                _projects.Update(project);
                affectedProjectIds.Add(project.Id);
            }

            var chatWorkspaces = await _workspaces.GetByChatModelIdAsync(model.Id, cancellationToken);
            foreach (var workspace in chatWorkspaces)
            {
                workspace.IsChatLlmEnabled = false;
                workspace.ChatModelId = null;
                workspace.UpdatedAt = DateTimeOffset.UtcNow;
                _workspaces.Update(workspace);
            }

            var ragProjects = await _projects.GetByRagModelIdAsync(model.Id, cancellationToken);
            foreach (var project in ragProjects)
            {
                project.RagLlmOverride = LlmFeatureOverride.Disabled;
                project.RagEmbeddingModelId = null;
                project.RagIndexVersion++;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                _projects.Update(project);
                affectedProjectIds.Add(project.Id);
                await _chunks.RemoveByProjectIdAsync(project.Id, cancellationToken);

                var documents = await _documents.GetByProjectAsync(project.Id, cancellationToken);
                foreach (var document in documents)
                {
                    if (document.State is DocumentState.Quarantined or DocumentState.Rejected)
                        continue;

                    document.State = DocumentState.Queued;
                    document.UpdatedAt = DateTimeOffset.UtcNow;
                    _documents.Update(document);
                    documentsToRequeue.Add(document.Id);
                }
            }

            var ragWorkspaces = await _workspaces.GetByRagModelIdAsync(model.Id, cancellationToken);
            foreach (var workspace in ragWorkspaces)
            {
                workspace.IsRagLlmEnabled = false;
                workspace.RagEmbeddingModelId = null;
                workspace.UpdatedAt = DateTimeOffset.UtcNow;
                _workspaces.Update(workspace);
            }

            var ocrProjects = await _projects.GetByOcrModelIdAsync(model.Id, cancellationToken);
            foreach (var project in ocrProjects)
            {
                project.OcrLlmOverride = LlmFeatureOverride.Disabled;
                project.OcrModelId = null;
                project.UpdatedAt = DateTimeOffset.UtcNow;
                _projects.Update(project);
                affectedProjectIds.Add(project.Id);
            }

            var ocrWorkspaces = await _workspaces.GetByOcrModelIdAsync(model.Id, cancellationToken);
            foreach (var workspace in ocrWorkspaces)
            {
                workspace.IsOcrLlmEnabled = false;
                workspace.OcrModelId = null;
                workspace.UpdatedAt = DateTimeOffset.UtcNow;
                _workspaces.Update(workspace);
            }
        }

        var preferences = await _catalog.GetPreferencesByUserAsync(userId, cancellationToken);
        foreach (var preference in preferences.Where(p => models.Any(m => m.Id == p.LlmModelId)).ToList())
        {
            _catalog.RemovePreference(preference);
        }

        _catalog.RemoveConnection(connection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var documentId in documentsToRequeue)
        {
            await _jobQueue.EnqueueParseJobAsync(documentId, cancellationToken);
        }

        return new LlmProviderDeleteResult(true, affectedProjectIds.Count);
    }

    public async Task SetDefaultAsync(Guid userId, LlmProviderType usage, Guid modelId, CancellationToken cancellationToken)
    {
        var model = await _catalog.GetModelByIdAsync(modelId, cancellationToken)
            ?? throw new InvalidOperationException("Selected model not found.");

        if (!IsSelectableForUser(model, userId) || model.Capability != usage)
            throw new InvalidOperationException($"Selected model is not available for {usage}.");

        var preference = await _catalog.GetPreferenceAsync(userId, usage, cancellationToken);
        if (preference is null)
        {
            await _catalog.AddPreferenceAsync(
                new UserLlmModelPreference
                {
                    UserId = userId,
                    Usage = usage,
                    LlmModelId = model.Id
                },
                cancellationToken);
        }
        else
        {
            preference.LlmModelId = model.Id;
            preference.UpdatedAt = DateTimeOffset.UtcNow;
            _catalog.UpdatePreference(preference);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HostedLlmModelDto>> GetAvailableHostedModelsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var models = await _catalog.GetPublishedHostedModelsAsync(null, cancellationToken);
        var preferences = await _catalog.GetPreferencesByUserAsync(userId, cancellationToken);
        var preferenceIds = preferences.Select(p => p.LlmModelId).ToHashSet();
        var preferenceByUsage = preferences.ToDictionary(p => p.Usage, p => p.LlmModelId);

        return models
            .Select(m => new HostedLlmModelDto(
                m.Id,
                m.Capability,
                m.GetName(),
                m.ModelId,
                m.Connection.GetName(),
                m.EmbeddingDimensions,
                preferenceIds.Contains(m.Id),
                preferenceByUsage.TryGetValue(m.Capability, out var defaultModelId) && defaultModelId == m.Id))
            .ToList();
    }

    public async Task SaveHostedModelSelectionAsync(Guid userId, HostedLlmModelSelectionRequest request, CancellationToken cancellationToken)
    {
        if (request.SetAsDefaultChat && request.ChatModelId is Guid chatModelId)
            await SetDefaultAsync(userId, LlmProviderType.Chat, chatModelId, cancellationToken);

        if (request.SetAsDefaultRagEmbedding && request.RagEmbeddingModelId is Guid ragModelId)
            await SetDefaultAsync(userId, LlmProviderType.RagEmbedding, ragModelId, cancellationToken);

        if (request.SetAsDefaultOcr && request.OcrModelId is Guid ocrModelId)
            await SetDefaultAsync(userId, LlmProviderType.Ocr, ocrModelId, cancellationToken);
    }

    public async Task<bool> HasAnyChatConfiguredAsync(Guid userId, CancellationToken cancellationToken)
    {
        var models = await _catalog.GetSelectableModelsAsync(new[] { userId }, LlmProviderType.Chat, cancellationToken);
        return models.Any();
    }

    public async Task<IReadOnlyList<LlmProviderOptionDto>> GetConfiguredByUsersAsync(
        IReadOnlyList<Guid> userIds,
        LlmProviderType usage,
        CancellationToken cancellationToken)
    {
        var models = await _catalog.GetSelectableModelsAsync(userIds, usage, cancellationToken);
        if (userIds.Count == 0)
            return Array.Empty<LlmProviderOptionDto>();

        var preferenceByUser = new Dictionary<Guid, Guid>();
        foreach (var userId in userIds)
        {
            var preference = await _catalog.GetPreferenceAsync(userId, usage, cancellationToken);
            if (preference is not null)
                preferenceByUser[userId] = preference.LlmModelId;
        }

        return models
            .Select(m =>
            {
                var connection = m.Connection;
                var userId = connection.UserId;
                return new LlmProviderOptionDto(
                    m.Id,
                    connection.Scope,
                    userId,
                    connection.User?.Email,
                    connection.Provider,
                    m.GetName(),
                    m.ModelId,
                    userId is not null && preferenceByUser.TryGetValue(userId.Value, out var defaultModelId) && defaultModelId == m.Id,
                    m.IsEnabled && connection.IsEnabled,
                    m.IsPublished,
                    m.EmbeddingDimensions);
            })
            .ToList();
    }

    private async Task<LlmModel?> UpsertCapabilityModelAsync(
        LlmConnection connection,
        LlmProviderType capability,
        bool enabled,
        string modelId,
        CancellationToken cancellationToken)
    {
        var existing = (await _catalog.GetModelsByConnectionAsync(connection.Id, cancellationToken))
            .FirstOrDefault(m => m.Capability == capability);

        if (!enabled)
        {
            if (existing is not null)
                _catalog.RemoveModel(existing);
            return null;
        }

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException($"{capability} model is required.");

        if (existing is null)
        {
            existing = new LlmModel
            {
                ConnectionId = connection.Id,
                Capability = capability
            };
            await _catalog.AddModelAsync(existing, cancellationToken);
        }

        existing.ModelId = modelId;
        existing.DisplayName = BuildPersonalModelDisplayName(connection, capability, modelId);
        existing.EmbeddingDimensions = capability == LlmProviderType.RagEmbedding
            ? DefaultEmbeddingDimensions(connection.Provider)
            : null;
        existing.IsEnabled = true;
        existing.IsPublished = true;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        return existing;
    }

    private static bool IsSelectableForUser(LlmModel model, Guid userId)
    {
        if (!model.IsEnabled || !model.Connection.IsEnabled)
            return false;

        return model.Connection.Scope switch
        {
            LlmConnectionScope.Hosted => model.IsPublished,
            LlmConnectionScope.Personal => model.Connection.UserId == userId,
            _ => false
        };
    }

    private static string BuildPersonalModelDisplayName(LlmConnection connection, LlmProviderType capability, string modelId)
    {
        var prefix = string.IsNullOrWhiteSpace(connection.Name)
            ? connection.Provider.ToString()
            : connection.Name.Trim();

        return $"{prefix} - {modelId}";
    }

    private static string? NormalizeModel(string? model)
        => string.IsNullOrWhiteSpace(model) ? null : model.Trim();

    private static string? NormalizeBaseUrl(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/');

    private static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalized = name.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeText(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Last4(string value)
        => value.Length >= 4 ? value[^4..] : value;

    private static string DefaultChatModel(LlmProvider provider)
        => provider switch
        {
            LlmProvider.Claude => "claude-sonnet-4-5-20250929",
            LlmProvider.Google => "gemini-1.5-pro",
            LlmProvider.AzureOpenAI => "gpt-4o",
            LlmProvider.AmazonBedrock => "openai.gpt-oss-20b",
            _ => "gpt-4o"
        };

    private static string DefaultEmbeddingModel(LlmProvider provider)
        => provider switch
        {
            LlmProvider.Google => "gemini-embedding-001",
            LlmProvider.AzureOpenAI => "text-embedding-3-small",
            _ => "text-embedding-3-small"
        };

    private static string DefaultOcrModel(LlmProvider provider)
        => provider switch
        {
            LlmProvider.Claude => "claude-sonnet-4-5-20250929",
            LlmProvider.Google => "gemini-1.5-pro",
            LlmProvider.AzureOpenAI => "gpt-4o",
            _ => "gpt-4o"
        };

    private static int? DefaultEmbeddingDimensions(LlmProvider provider)
        => provider switch
        {
            LlmProvider.Google => 768,
            _ => 1536
        };

    private static bool RequiresBaseUrl(LlmProvider provider)
        // Personal OpenAI-compatible/local endpoints are intentionally managed by admins
        // as hosted models, so user BYO providers only need cloud endpoints here.
        => provider is LlmProvider.AzureOpenAI or LlmProvider.AmazonBedrock;

    private static bool SupportsOcr(LlmProvider provider)
        => provider is LlmProvider.OpenAI or LlmProvider.Claude or LlmProvider.Google;

    private static LlmProviderDto ToDto(
        LlmConnection connection,
        LlmModel? chatModel,
        LlmModel? ragModel,
        LlmModel? ocrModel,
        IReadOnlyDictionary<LlmProviderType, Guid> preferenceByUsage,
        int usedByProjectCount)
    {
        return new LlmProviderDto(
            connection.Id,
            connection.Provider,
            connection.Scope,
            connection.Name,
            connection.Description,
            connection.Notes,
            connection.BaseUrl,
            connection.Provider != LlmProvider.None && !string.IsNullOrEmpty(connection.EncryptedApiKey),
            connection.ApiKeyLast4,
            connection.IsEnabled,
            chatModel is not null,
            ragModel is not null,
            ocrModel is not null,
            chatModel?.Id,
            chatModel?.ModelId,
            ragModel?.Id,
            ragModel?.ModelId,
            ocrModel?.Id,
            ocrModel?.ModelId,
            chatModel is not null && preferenceByUsage.TryGetValue(LlmProviderType.Chat, out var chatDefaultId) && chatDefaultId == chatModel.Id,
            ragModel is not null && preferenceByUsage.TryGetValue(LlmProviderType.RagEmbedding, out var ragDefaultId) && ragDefaultId == ragModel.Id,
            ocrModel is not null && preferenceByUsage.TryGetValue(LlmProviderType.Ocr, out var ocrDefaultId) && ocrDefaultId == ocrModel.Id,
            usedByProjectCount);
    }
}
