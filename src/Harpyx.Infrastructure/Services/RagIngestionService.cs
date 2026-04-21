using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public class RagIngestionService : IRagIngestionService
{
    private readonly IStorageService _storage;
    private readonly IDocumentChunkRepository _chunks;
    private readonly ITextChunkingService _chunking;
    private readonly IDocumentTextExtractionService _extraction;
    private readonly IEmbeddingService _embedding;
    private readonly IKeywordExtractionService _keywords;
    private readonly IOpenSearchChunkIndexService _openSearch;
    private readonly IDocumentContainerExpansionService _containerExpansion;
    private readonly IEncryptionService _encryption;
    private readonly IUsageLimitService _usageLimits;
    private readonly IIdempotencyService _idempotency;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly IUrlFetcher _urlFetcher;
    private readonly IFileMalwareScanner _malwareScanner;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RagOptions _options;
    private readonly OpenSearchOptions _openSearchOptions;
    private readonly ILogger<RagIngestionService> _logger;

    public RagIngestionService(
        IStorageService storage,
        IDocumentChunkRepository chunks,
        ITextChunkingService chunking,
        IDocumentTextExtractionService extraction,
        IEmbeddingService embedding,
        IKeywordExtractionService keywords,
        IOpenSearchChunkIndexService openSearch,
        IDocumentContainerExpansionService containerExpansion,
        IEncryptionService encryption,
        IUsageLimitService usageLimits,
        IIdempotencyService idempotency,
        IPlatformSettingsService platformSettings,
        IUrlFetcher urlFetcher,
        IFileMalwareScanner malwareScanner,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        RagOptions options,
        OpenSearchOptions openSearchOptions,
        ILogger<RagIngestionService> logger)
    {
        _storage = storage;
        _chunks = chunks;
        _chunking = chunking;
        _extraction = extraction;
        _embedding = embedding;
        _keywords = keywords;
        _openSearch = openSearch;
        _containerExpansion = containerExpansion;
        _encryption = encryption;
        _usageLimits = usageLimits;
        _idempotency = idempotency;
        _platformSettings = platformSettings;
        _urlFetcher = urlFetcher;
        _malwareScanner = malwareScanner;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
        _options = options;
        _openSearchOptions = openSearchOptions;
        _logger = logger;
    }

    public async Task IngestDocumentAsync(Document document, CancellationToken cancellationToken)
    {
        var project = document.Project ?? throw new InvalidOperationException($"Project not loaded for document {document.Id}.");
        var workspace = project.Workspace ?? throw new InvalidOperationException($"Workspace not loaded for project {project.Id}.");
        var runtime = await _platformSettings.GetAsync(cancellationToken);

        var idempotencyKey = $"rag:ingest:{document.Id}:{document.Version}:{project.RagIndexVersion}";
        if (await _idempotency.ExistsAsync(idempotencyKey, cancellationToken))
        {
            _logger.LogInformation("Skipping ingestion for {DocumentId} because idempotency key exists.", document.Id);
            return;
        }

        try
        {
            if (document.IsContainer && document.ContainerType == DocumentContainerType.Archive)
            {
                var containerExpansion = await _containerExpansion.ExpandAsync(document, runtime, cancellationToken);
                await _idempotency.StoreAsync(idempotencyKey, TimeSpan.FromDays(7), cancellationToken);

                document.State = DocumentState.Completed;
                document.ExtractionState = containerExpansion.HasWarnings
                    ? DocumentExtractionState.CompletedWithWarnings
                    : DocumentExtractionState.Completed;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                return;
            }

            var allowOcr = await _usageLimits.IsOcrAllowedAsync(workspace.TenantId, cancellationToken);

            var ragModel = ResolveEffectiveRagModel(project);
            string? apiKey = null;
            if (ragModel is not null)
            {
                await _usageLimits.EnsureRagIndexingAllowedAsync(workspace.TenantId, cancellationToken);
                apiKey = string.IsNullOrWhiteSpace(ragModel.Connection.EncryptedApiKey)
                    ? string.Empty
                    : _encryption.Decrypt(ragModel.Connection.EncryptedApiKey);
            }

            var ocrModel = ResolveEffectiveOcrModel(project);
            OcrModelContext? ocrContext = null;
            if (ocrModel is not null)
            {
                ocrContext = new OcrModelContext(
                    ocrModel.Connection.Provider,
                    string.IsNullOrWhiteSpace(ocrModel.Connection.EncryptedApiKey)
                        ? string.Empty
                        : _encryption.Decrypt(ocrModel.Connection.EncryptedApiKey),
                    ocrModel.ModelId);
            }

            Stream content;

            if (!string.IsNullOrWhiteSpace(document.SourceUrl))
            {
                var fetchResult = await _urlFetcher.FetchAsync(document.SourceUrl, cancellationToken);
                content = fetchResult.Content;

                if (!string.IsNullOrWhiteSpace(fetchResult.ContentType))
                    document.ContentType = fetchResult.ContentType;
                if (fetchResult.ContentLength.HasValue)
                    document.SizeBytes = fetchResult.ContentLength.Value;
                if (!string.IsNullOrWhiteSpace(fetchResult.Title))
                    document.FileName = fetchResult.Title;

                var scan = await _malwareScanner.ScanAsync(
                    document.FileName, document.ContentType, content, cancellationToken);
                if (scan.Verdict is MalwareScanVerdict.Infected or MalwareScanVerdict.Error)
                {
                    document.State = DocumentState.Rejected;
                    document.ExtractionState = DocumentExtractionState.BlockedByPolicy;
                    document.UpdatedAt = DateTimeOffset.UtcNow;
                    await _auditService.RecordAsync("url_rejected_malware", null, null,
                        $"Document {document.Id} from URL rejected by malware scan. Verdict: {scan.Verdict}. Signature: {scan.Signature ?? "n/a"}",
                        cancellationToken);
                    await content.DisposeAsync();
                    return;
                }

                if (content.CanSeek)
                    content.Position = 0;
            }
            else
            {
                content = await _storage.OpenReadAsync(document.StorageKey, cancellationToken);
            }

            try
            {
                var pages = await _extraction.ExtractAsync(
                    document.FileName,
                    document.ContentType,
                    content,
                    allowOcr,
                    ocrContext,
                    cancellationToken);
                var chunkCandidates = _chunking.BuildChunks(pages);
                if (chunkCandidates.Count == 0)
                    throw new InvalidOperationException($"No chunk candidates were generated for document {document.Id}.");

                List<float[]>? vectors = null;
                if (ragModel is not null)
                {
                    vectors = new List<float[]>(chunkCandidates.Count);
                    var batchSize = Math.Max(1, _options.EmbeddingBatchSize);
                    for (var i = 0; i < chunkCandidates.Count; i += batchSize)
                    {
                        var batch = chunkCandidates
                            .Skip(i)
                            .Take(batchSize)
                            .Select(c => c.Content)
                            .ToList();

                        var embedded = await _embedding.EmbedAsync(
                            batch,
                            ragModel,
                            apiKey!,
                            cancellationToken);
                        vectors.AddRange(embedded);
                    }

                    if (vectors.Count != chunkCandidates.Count)
                        throw new InvalidOperationException("Embedding response count does not match chunk count.");
                }

                await _chunks.RemoveByDocumentIdAsync(document.Id, cancellationToken);
                var entities = new List<DocumentChunk>(chunkCandidates.Count);
                var openSearchDocuments = runtime.RagUseOpenSearchIndexing && _openSearchOptions.Enabled
                    ? new List<OpenSearchChunkIndexDocument>(chunkCandidates.Count)
                    : null;

                for (var i = 0; i < chunkCandidates.Count; i++)
                {
                    var chunk = chunkCandidates[i];
                    var vector = vectors?[i];
                    entities.Add(new DocumentChunk
                    {
                        DocumentId = document.Id,
                        ChunkIndex = chunk.ChunkIndex,
                        PageNumber = chunk.PageNumber,
                        SourceType = chunk.SourceType,
                        OcrConfidence = chunk.OcrConfidence,
                        Content = chunk.Content,
                        CharacterCount = chunk.Content.Length,
                        IndexVersion = project.RagIndexVersion,
                        Embedding = vector is null ? Array.Empty<byte>() : EmbeddingVectorSerializer.Serialize(vector)
                    });

                    if (openSearchDocuments is not null)
                    {
                        var keywords = _keywords.ExtractKeywords(
                            chunk.Content,
                            Math.Max(1, runtime.RagKeywordMaxCount),
                            runtime.RagUseRakeKeywordExtraction);
                        var chunkUid = $"{document.Id:N}:{project.RagIndexVersion}:{chunk.ChunkIndex}";
                        openSearchDocuments.Add(new OpenSearchChunkIndexDocument(
                            chunkUid,
                            workspace.TenantId,
                            workspace.Id,
                            project.Id,
                            document.Id,
                            document.FileName,
                            chunk.ChunkIndex,
                            chunk.PageNumber,
                            chunk.SourceType,
                            chunk.OcrConfidence,
                            chunk.Content,
                            keywords,
                            vector is null ? null : _openSearch.NormalizeVector(vector),
                            project.RagIndexVersion,
                            DocumentState.Completed.ToString(),
                            DateTimeOffset.UtcNow));
                    }
                }

                await _chunks.AddRangeAsync(entities, cancellationToken);

                if (openSearchDocuments is not null)
                {
                    await _openSearch.DeleteByDocumentAndVersionAsync(project.Id, document.Id, project.RagIndexVersion, cancellationToken);
                    await _openSearch.UpsertChunksAsync(openSearchDocuments, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var hasContainerWarnings = false;
                if (document.IsContainer && document.ContainerType == DocumentContainerType.Email)
                {
                    var containerExpansion = await _containerExpansion.ExpandAsync(document, runtime, cancellationToken);
                    hasContainerWarnings = containerExpansion.HasWarnings;
                }

                await _idempotency.StoreAsync(idempotencyKey, TimeSpan.FromDays(7), cancellationToken);

                document.State = DocumentState.Completed;
                document.ExtractionState = hasContainerWarnings
                    ? DocumentExtractionState.CompletedWithWarnings
                    : DocumentExtractionState.Completed;
                document.UpdatedAt = DateTimeOffset.UtcNow;
            }
            finally
            {
                await content.DisposeAsync();
            }
        }
        catch (NotSupportedException ex)
        {
            _logger.LogInformation(ex, "Document {DocumentId} is unsupported for extraction.", document.Id);
            document.State = DocumentState.Completed;
            document.ExtractionState = DocumentExtractionState.Unsupported;
            document.UpdatedAt = DateTimeOffset.UtcNow;
        }
        catch
        {
            document.ExtractionState = DocumentExtractionState.Failed;
            throw;
        }
    }

    private static LlmModel? ResolveEffectiveRagModel(Project project)
    {
        return project.RagLlmOverride switch
        {
            LlmFeatureOverride.Enabled => IsRagModelConfigured(project.RagEmbeddingModelId, project.RagEmbeddingModel)
                ? project.RagEmbeddingModel
                : null,
            LlmFeatureOverride.Disabled => null,
            _ => project.Workspace.IsRagLlmEnabled &&
                 IsRagModelConfigured(project.Workspace.RagEmbeddingModelId, project.Workspace.RagEmbeddingModel)
                ? project.Workspace.RagEmbeddingModel
                : null
        };
    }

    private static LlmModel? ResolveEffectiveOcrModel(Project project)
    {
        return project.OcrLlmOverride switch
        {
            LlmFeatureOverride.Enabled => IsOcrModelConfigured(project.OcrModelId, project.OcrModel)
                ? project.OcrModel
                : null,
            LlmFeatureOverride.Disabled => null,
            _ => project.Workspace.IsOcrLlmEnabled &&
                 IsOcrModelConfigured(project.Workspace.OcrModelId, project.Workspace.OcrModel)
                ? project.Workspace.OcrModel
                : null
        };
    }

    private static bool IsRagModelConfigured(Guid? modelId, LlmModel? model)
        => modelId is not null &&
           model is not null &&
           model.Capability == LlmProviderType.RagEmbedding &&
           IsConfiguredModel(model);

    private static bool IsOcrModelConfigured(Guid? modelId, LlmModel? model)
        => modelId is not null &&
           model is not null &&
           model.Capability == LlmProviderType.Ocr &&
           IsConfiguredModel(model);

    private static bool IsConfiguredModel(LlmModel model)
        => model.IsEnabled &&
           model.Connection.IsEnabled &&
           model.Connection.Provider != LlmProvider.None &&
           (model.Connection.Scope == LlmConnectionScope.Hosted
                ? model.IsPublished
                : !string.IsNullOrWhiteSpace(model.Connection.EncryptedApiKey));
}
