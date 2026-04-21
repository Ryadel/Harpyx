using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Harpyx.Application.DTOs;
using Harpyx.Application.Exceptions;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Harpyx.Infrastructure.Services;

public class RagRetrievalService : IRagRetrievalService
{
    private readonly IProjectRepository _projects;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IEmbeddingService _embedding;
    private readonly IKeywordExtractionService _keywords;
    private readonly IOpenSearchChunkIndexService _openSearch;
    private readonly IEncryptionService _encryption;
    private readonly IUsageLimitService _usageLimits;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly OpenSearchOptions _openSearchOptions;
    private readonly IDatabase _cache;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        IProjectRepository projects,
        IDocumentChunkRepository chunks,
        IEmbeddingService embedding,
        IKeywordExtractionService keywords,
        IOpenSearchChunkIndexService openSearch,
        IEncryptionService encryption,
        IUsageLimitService usageLimits,
        IPlatformSettingsService platformSettings,
        IConnectionMultiplexer redis,
        OpenSearchOptions openSearchOptions,
        ILogger<RagRetrievalService> logger)
    {
        _projects = projects;
        _chunks = chunks;
        _embedding = embedding;
        _keywords = keywords;
        _openSearch = openSearch;
        _encryption = encryption;
        _usageLimits = usageLimits;
        _platformSettings = platformSettings;
        _cache = redis.GetDatabase();
        _openSearchOptions = openSearchOptions;
        _logger = logger;
    }

    public async Task<RagContextResult> BuildContextAsync(
        Guid projectId,
        IReadOnlyList<Guid> documentIds,
        string query,
        CancellationToken cancellationToken)
    {
        if (documentIds.Count == 0 || string.IsNullOrWhiteSpace(query))
            return new RagContextResult("No RAG context available.", Array.Empty<RagChunkContextDto>());

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return new RagContextResult("Project not found.", Array.Empty<RagChunkContextDto>());

        var runtime = await _platformSettings.GetAsync(cancellationToken);
        var ragModel = ResolveEffectiveRagModel(project);
        var useOpenSearchRetrieval = runtime.RagUseOpenSearchRetrieval && _openSearchOptions.Enabled;
        var cacheKey = BuildCacheKey(project, ragModel?.Id, documentIds, query, useOpenSearchRetrieval);
        var cached = await _cache.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            var cachedResult = JsonSerializer.Deserialize<RagContextResult>(cached.ToString());
            if (cachedResult is not null)
                return cachedResult;
        }

        RagContextResult result;
        if (useOpenSearchRetrieval)
        {
            try
            {
                result = await BuildContextFromOpenSearchAsync(project, documentIds, query, ragModel, runtime, cancellationToken);
                if (result.Chunks.Count == 0 && runtime.RagFallbackToSqlRetrievalOnOpenSearchFailure)
                {
                    result = await BuildContextFromSqlAsync(project, documentIds, query, ragModel, runtime, cancellationToken);
                }
            }
            catch (Exception ex) when (runtime.RagFallbackToSqlRetrievalOnOpenSearchFailure)
            {
                _logger.LogWarning(ex, "OpenSearch retrieval failed for project {ProjectId}. Falling back to SQL retrieval.", projectId);
                result = await BuildContextFromSqlAsync(project, documentIds, query, ragModel, runtime, cancellationToken);
            }
        }
        else
        {
            result = await BuildContextFromSqlAsync(project, documentIds, query, ragModel, runtime, cancellationToken);
        }

        await _cache.StringSetAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            TimeSpan.FromSeconds(Math.Max(5, runtime.RagContextCacheTtlSeconds)));

        return result;
    }

    private async Task<RagContextResult> BuildContextFromOpenSearchAsync(
        Project project,
        IReadOnlyList<Guid> documentIds,
        string query,
        LlmModel? ragModel,
        PlatformSettingsDto runtime,
        CancellationToken cancellationToken)
    {
        if (project.Workspace is null)
            return new RagContextResult("Project workspace is not available for filters.", Array.Empty<RagChunkContextDto>());

        float[]? queryVector = null;
        if (ragModel is not null)
        {
            try
            {
                await _usageLimits.EnsureRagIndexingAllowedAsync(project.Workspace.TenantId, cancellationToken);
            }
            catch (UsageLimitExceededException ex)
            {
                return new RagContextResult(ex.Message, Array.Empty<RagChunkContextDto>());
            }

            var apiKey = string.IsNullOrWhiteSpace(ragModel.Connection.EncryptedApiKey)
                ? string.Empty
                : _encryption.Decrypt(ragModel.Connection.EncryptedApiKey);
            queryVector = (await _embedding.EmbedAsync(
                new[] { query },
                ragModel,
                apiKey,
                cancellationToken)).Single();
        }

        var queryKeywords = _keywords.ExtractKeywords(
            query,
            Math.Max(1, runtime.RagKeywordMaxCount),
            runtime.RagUseRakeKeywordExtraction);
        var lexicalHits = await _openSearch.SearchLexicalAsync(
            new OpenSearchLexicalSearchRequest(
                project.Workspace.TenantId,
                project.WorkspaceId,
                project.Id,
                documentIds,
                project.RagIndexVersion,
                query.Trim(),
                queryKeywords,
                Math.Max(runtime.RagTopK, runtime.RagLexicalCandidateK)),
            cancellationToken);

        IReadOnlyList<OpenSearchChunkSearchHit> vectorHits = Array.Empty<OpenSearchChunkSearchHit>();
        if (queryVector is not null)
        {
            vectorHits = await _openSearch.SearchVectorAsync(
                new OpenSearchVectorSearchRequest(
                    project.Workspace.TenantId,
                    project.WorkspaceId,
                    project.Id,
                    documentIds,
                    project.RagIndexVersion,
                    queryVector,
                    Math.Max(runtime.RagTopK, runtime.RagVectorCandidateK)),
                cancellationToken);
        }

        var fused = FuseWithRrf(lexicalHits, vectorHits, Math.Max(1, runtime.RagRrfK), Math.Max(1, runtime.RagTopK));
        if (fused.Count == 0)
            return new RagContextResult("No relevant chunk found for the selected documents.", Array.Empty<RagChunkContextDto>());

        var context = BuildContextText(fused, runtime.RagMaxContextChars);
        return new RagContextResult(context, fused);
    }

    private async Task<RagContextResult> BuildContextFromSqlAsync(
        Project project,
        IReadOnlyList<Guid> documentIds,
        string query,
        LlmModel? ragModel,
        PlatformSettingsDto runtime,
        CancellationToken cancellationToken)
    {
        var allChunks = await _chunks.GetByDocumentIdsAsync(documentIds, cancellationToken);
        var candidates = allChunks
            .Where(c => c.Document?.ProjectId == project.Id && c.IndexVersion == project.RagIndexVersion)
            .ToList();

        if (candidates.Count == 0)
            return new RagContextResult("No indexed chunk available for selected documents.", Array.Empty<RagChunkContextDto>());

        List<RagChunkContextDto> scored;
        if (ragModel is not null)
        {
            try
            {
                await _usageLimits.EnsureRagIndexingAllowedAsync(project.Workspace.TenantId, cancellationToken);
            }
            catch (UsageLimitExceededException ex)
            {
                return new RagContextResult(ex.Message, Array.Empty<RagChunkContextDto>());
            }

            var apiKey = string.IsNullOrWhiteSpace(ragModel.Connection.EncryptedApiKey)
                ? string.Empty
                : _encryption.Decrypt(ragModel.Connection.EncryptedApiKey);
            var queryVector = (await _embedding.EmbedAsync(
                new[] { query },
                ragModel,
                apiKey,
                cancellationToken)).Single();

            scored = candidates
                .Where(c => c.Embedding.Length > 0)
                .Select(c =>
                {
                    var vector = EmbeddingVectorSerializer.Deserialize(c.Embedding);
                    var score = CosineSimilarity(queryVector, vector);
                    return new RagChunkContextDto(
                        c.DocumentId,
                        c.Document?.FileName ?? "unknown",
                        c.ChunkIndex,
                        c.PageNumber,
                        c.SourceType,
                        c.OcrConfidence,
                        c.Content,
                        score);
                })
                .OrderByDescending(c => c.Score)
                .Take(Math.Max(1, runtime.RagTopK))
                .ToList();
        }
        else
        {
            var keywords = _keywords.ExtractKeywords(
                query,
                Math.Max(1, runtime.RagKeywordMaxCount),
                runtime.RagUseRakeKeywordExtraction);
            scored = candidates
                .Select(c =>
                {
                    var score = ComputeLexicalScore(c.Content, query, keywords);
                    return new RagChunkContextDto(
                        c.DocumentId,
                        c.Document?.FileName ?? "unknown",
                        c.ChunkIndex,
                        c.PageNumber,
                        c.SourceType,
                        c.OcrConfidence,
                        c.Content,
                        score);
                })
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .Take(Math.Max(1, runtime.RagTopK))
                .ToList();
        }

        if (scored.Count == 0)
            return new RagContextResult("No relevant chunk found for the selected documents.", Array.Empty<RagChunkContextDto>());

        return new RagContextResult(BuildContextText(scored, runtime.RagMaxContextChars), scored);
    }

    private static string BuildContextText(IReadOnlyList<RagChunkContextDto> chunks, int maxContextChars)
    {
        var contextBuilder = new StringBuilder();
        var currentChars = 0;
        foreach (var chunk in chunks)
        {
            var prefix = $"[doc:{chunk.FileName} page:{chunk.PageNumber?.ToString() ?? "-"} source:{chunk.SourceType} score:{chunk.Score:0.000}] ";
            var line = prefix + chunk.Content;
            if (currentChars + line.Length > Math.Max(2000, maxContextChars))
                break;

            contextBuilder.AppendLine(line);
            currentChars += line.Length;
        }

        return contextBuilder.Length > 0
            ? contextBuilder.ToString().Trim()
            : "No context assembled due to context-size limits.";
    }

    private static List<RagChunkContextDto> FuseWithRrf(
        IReadOnlyList<OpenSearchChunkSearchHit> lexicalHits,
        IReadOnlyList<OpenSearchChunkSearchHit> vectorHits,
        int rrfK,
        int topK)
    {
        var map = new Dictionary<string, RagChunkContextDto>(StringComparer.Ordinal);
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);

        AccumulateRrf(lexicalHits);
        AccumulateRrf(vectorHits);

        return scores
            .OrderByDescending(kv => kv.Value)
            .Take(topK)
            .Select(kv =>
            {
                var dto = map[kv.Key];
                return dto with { Score = kv.Value };
            })
            .ToList();

        void AccumulateRrf(IReadOnlyList<OpenSearchChunkSearchHit> hits)
        {
            for (var rank = 0; rank < hits.Count; rank++)
            {
                var hit = hits[rank];
                if (!map.ContainsKey(hit.ChunkUid))
                {
                    map[hit.ChunkUid] = new RagChunkContextDto(
                        hit.DocumentId,
                        hit.FileName,
                        hit.ChunkIndex,
                        hit.PageNumber,
                        hit.SourceType,
                        hit.OcrConfidence,
                        hit.Text,
                        0);
                }

                var increment = 1d / (rrfK + rank + 1);
                scores[hit.ChunkUid] = scores.TryGetValue(hit.ChunkUid, out var value) ? value + increment : increment;
            }
        }
    }

    private static double ComputeLexicalScore(string content, string query, IReadOnlyList<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(content))
            return 0;

        var normalizedContent = content.ToLowerInvariant();
        var normalizedQuery = query.ToLowerInvariant();

        var score = normalizedContent.Contains(normalizedQuery, StringComparison.Ordinal) ? 1.0 : 0.0;
        foreach (var keyword in keywords)
        {
            if (normalizedContent.Contains(keyword, StringComparison.Ordinal))
                score += 0.25;
        }

        return score;
    }

    private static LlmModel? ResolveEffectiveRagModel(Project? project)
    {
        if (project is null)
            return null;

        return project.RagLlmOverride switch
        {
            LlmFeatureOverride.Enabled => IsRagModelConfigured(project.RagEmbeddingModelId, project.RagEmbeddingModel)
                ? project.RagEmbeddingModel
                : null,
            LlmFeatureOverride.Disabled => null,
            _ => project.Workspace is not null &&
                 project.Workspace.IsRagLlmEnabled &&
                 IsRagModelConfigured(project.Workspace.RagEmbeddingModelId, project.Workspace.RagEmbeddingModel)
                ? project.Workspace.RagEmbeddingModel
                : null
        };
    }

    private static bool IsRagModelConfigured(Guid? modelId, LlmModel? model)
        => modelId is not null &&
           model is not null &&
           model.Capability == LlmProviderType.RagEmbedding &&
           model.IsEnabled &&
           model.Connection.IsEnabled &&
           model.Connection.Provider != LlmProvider.None &&
           (model.Connection.Scope == LlmConnectionScope.Hosted
                ? model.IsPublished
                : !string.IsNullOrWhiteSpace(model.Connection.EncryptedApiKey));

    private static double CosineSimilarity(float[] a, float[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        if (len == 0)
            return 0;

        double dot = 0;
        double magA = 0;
        double magB = 0;

        for (var i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA <= 0 || magB <= 0)
            return 0;

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private static string BuildCacheKey(
        Project project,
        Guid? ragProviderId,
        IReadOnlyList<Guid> documentIds,
        string query,
        bool openSearchEnabled)
    {
        var orderedDocs = documentIds.OrderBy(id => id).Select(id => id.ToString("N"));
        var providerPart = ragProviderId?.ToString("N") ?? "none";
        var mode = openSearchEnabled ? "os" : "sql";
        var source = $"{project.Id:N}|{project.RagIndexVersion}|{providerPart}|{mode}|{string.Join(",", orderedDocs)}|{query.Trim()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        return $"harpyx:rag:ctx:{hash}";
    }
}
