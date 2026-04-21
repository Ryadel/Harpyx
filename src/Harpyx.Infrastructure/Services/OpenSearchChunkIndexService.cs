using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public record OpenSearchChunkIndexDocument(
    string ChunkUid,
    Guid TenantId,
    Guid WorkspaceId,
    Guid ProjectId,
    Guid DocumentId,
    string FileName,
    int ChunkIndex,
    int? PageNumber,
    string SourceType,
    double? OcrConfidence,
    string Text,
    IReadOnlyList<string> Keywords,
    float[]? Embedding,
    int IndexVersion,
    string DocumentState,
    DateTimeOffset UpdatedAt);

public record OpenSearchLexicalSearchRequest(
    Guid TenantId,
    Guid WorkspaceId,
    Guid ProjectId,
    IReadOnlyList<Guid> DocumentIds,
    int IndexVersion,
    string Query,
    IReadOnlyList<string> QueryKeywords,
    int Size);

public record OpenSearchVectorSearchRequest(
    Guid TenantId,
    Guid WorkspaceId,
    Guid ProjectId,
    IReadOnlyList<Guid> DocumentIds,
    int IndexVersion,
    float[] QueryVector,
    int Size);

public record OpenSearchChunkSearchHit(
    string ChunkUid,
    Guid DocumentId,
    string FileName,
    int ChunkIndex,
    int? PageNumber,
    string SourceType,
    double? OcrConfidence,
    string Text,
    int IndexVersion,
    double Score);

public interface IOpenSearchChunkIndexService
{
    float[] NormalizeVector(float[] vector);
    Task DeleteByDocumentAndVersionAsync(Guid projectId, Guid documentId, int indexVersion, CancellationToken cancellationToken);
    Task UpsertChunksAsync(IReadOnlyList<OpenSearchChunkIndexDocument> chunks, CancellationToken cancellationToken);
    Task<IReadOnlyList<OpenSearchChunkSearchHit>> SearchLexicalAsync(OpenSearchLexicalSearchRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<OpenSearchChunkSearchHit>> SearchVectorAsync(OpenSearchVectorSearchRequest request, CancellationToken cancellationToken);
}

public class OpenSearchChunkIndexService : IOpenSearchChunkIndexService
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] SourceFields =
    {
        "chunkUid",
        "documentId",
        "fileName",
        "chunkIndex",
        "pageNumber",
        "sourceType",
        "ocrConfidence",
        "text",
        "indexVersion"
    };

    private readonly OpenSearchOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenSearchChunkIndexService> _logger;
    private readonly SemaphoreSlim _indexInitLock = new(1, 1);
    private volatile bool _indexInitialized;

    public OpenSearchChunkIndexService(
        OpenSearchOptions options,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenSearchChunkIndexService> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public float[] NormalizeVector(float[] vector)
    {
        var target = Math.Max(8, _options.EmbeddingDimensions);
        if (vector.Length == target)
            return vector;

        var normalized = new float[target];
        var copyLength = Math.Min(vector.Length, target);
        Array.Copy(vector, normalized, copyLength);
        return normalized;
    }

    public async Task DeleteByDocumentAndVersionAsync(
        Guid projectId,
        Guid documentId,
        int indexVersion,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        await EnsureIndexReadyAsync(cancellationToken);

        var requestBody = new
        {
            query = new
            {
                @bool = new
                {
                    filter = new object[]
                    {
                        new { term = new Dictionary<string, object> { ["projectId"] = projectId.ToString("D") } },
                        new { term = new Dictionary<string, object> { ["documentId"] = documentId.ToString("D") } },
                        new { term = new Dictionary<string, object> { ["indexVersion"] = indexVersion } }
                    }
                }
            }
        };

        using var response = await SendJsonAsync(
            HttpMethod.Post,
            $"/{Uri.EscapeDataString(_options.IndexAlias)}/_delete_by_query?conflicts=proceed&refresh=true",
            requestBody,
            cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenSearch delete_by_query failed: {(int)response.StatusCode} {body}");
        }
    }

    public async Task UpsertChunksAsync(IReadOnlyList<OpenSearchChunkIndexDocument> chunks, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || chunks.Count == 0)
            return;

        await EnsureIndexReadyAsync(cancellationToken);

        var ndjson = new StringBuilder(chunks.Count * 256);
        foreach (var chunk in chunks)
        {
            var action = new
            {
                index = new
                {
                    _index = _options.IndexAlias,
                    _id = chunk.ChunkUid
                }
            };

            ndjson.AppendLine(JsonSerializer.Serialize(action, JsonWriteOptions));
            ndjson.AppendLine(JsonSerializer.Serialize(new
            {
                chunk.ChunkUid,
                TenantId = chunk.TenantId.ToString("D"),
                WorkspaceId = chunk.WorkspaceId.ToString("D"),
                ProjectId = chunk.ProjectId.ToString("D"),
                DocumentId = chunk.DocumentId.ToString("D"),
                chunk.FileName,
                chunk.ChunkIndex,
                chunk.PageNumber,
                chunk.SourceType,
                chunk.OcrConfidence,
                Text = chunk.Text,
                chunk.Keywords,
                KeywordsText = string.Join(' ', chunk.Keywords),
                chunk.Embedding,
                chunk.IndexVersion,
                chunk.DocumentState,
                chunk.UpdatedAt
            }, JsonWriteOptions));
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/_bulk")
        {
            Content = new StringContent(ndjson.ToString(), Encoding.UTF8, "application/x-ndjson")
        };
        AttachAuthHeader(request);

        var client = _httpClientFactory.CreateClient("OpenSearch");
        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenSearch bulk indexing failed: {(int)response.StatusCode} {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.True)
        {
            _logger.LogWarning("OpenSearch bulk indexing returned partial failures: {Body}", responseBody);
            throw new InvalidOperationException("OpenSearch bulk indexing returned errors.");
        }
    }

    public async Task<IReadOnlyList<OpenSearchChunkSearchHit>> SearchLexicalAsync(
        OpenSearchLexicalSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || request.DocumentIds.Count == 0 || string.IsNullOrWhiteSpace(request.Query))
            return Array.Empty<OpenSearchChunkSearchHit>();

        await EnsureIndexReadyAsync(cancellationToken);

        var should = new List<object>
        {
            new { match = new Dictionary<string, object> { ["text"] = new { query = request.Query, boost = 2.0 } } },
            new { match = new Dictionary<string, object> { ["keywordsText"] = new { query = request.Query, boost = 1.0 } } }
        };

        if (request.QueryKeywords.Count > 0)
        {
            should.Add(new
            {
                terms = new Dictionary<string, object>
                {
                    ["keywords"] = request.QueryKeywords
                }
            });
        }

        var requestBody = new
        {
            size = Math.Max(1, request.Size),
            _source = SourceFields,
            query = new
            {
                @bool = new
                {
                    filter = BuildFilters(request.TenantId, request.WorkspaceId, request.ProjectId, request.DocumentIds, request.IndexVersion),
                    should,
                    minimum_should_match = 1
                }
            }
        };

        using var response = await SendJsonAsync(
            HttpMethod.Post,
            $"/{Uri.EscapeDataString(_options.IndexAlias)}/_search",
            requestBody,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenSearch lexical search failed: {(int)response.StatusCode} {body}");
        }

        return await ParseSearchHitsAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<OpenSearchChunkSearchHit>> SearchVectorAsync(
        OpenSearchVectorSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || request.DocumentIds.Count == 0 || request.QueryVector.Length == 0)
            return Array.Empty<OpenSearchChunkSearchHit>();

        await EnsureIndexReadyAsync(cancellationToken);

        var queryVector = NormalizeVector(request.QueryVector);
        var requestBody = new
        {
            size = Math.Max(1, request.Size),
            _source = SourceFields,
            query = new
            {
                @bool = new
                {
                    filter = BuildFilters(request.TenantId, request.WorkspaceId, request.ProjectId, request.DocumentIds, request.IndexVersion),
                    must = new object[]
                    {
                        new
                        {
                            knn = new Dictionary<string, object>
                            {
                                ["embedding"] = new
                                {
                                    vector = queryVector,
                                    k = Math.Max(1, request.Size)
                                }
                            }
                        }
                    }
                }
            }
        };

        using var response = await SendJsonAsync(
            HttpMethod.Post,
            $"/{Uri.EscapeDataString(_options.IndexAlias)}/_search",
            requestBody,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenSearch vector search failed: {(int)response.StatusCode} {body}");
        }

        return await ParseSearchHitsAsync(response, cancellationToken);
    }

    private async Task EnsureIndexReadyAsync(CancellationToken cancellationToken)
    {
        if (_indexInitialized || !_options.Enabled)
            return;

        await _indexInitLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexInitialized)
                return;

            using var indexCheck = await SendAsync(HttpMethod.Head, $"/{Uri.EscapeDataString(_options.IndexName)}", null, cancellationToken);
            if (indexCheck.StatusCode == HttpStatusCode.NotFound)
            {
                var createBody = new
                {
                    settings = new
                    {
                        index = new Dictionary<string, object>
                        {
                            ["knn"] = true
                        }
                    },
                    mappings = new
                    {
                        properties = new Dictionary<string, object>
                        {
                            ["chunkUid"] = new { type = "keyword" },
                            ["tenantId"] = new { type = "keyword" },
                            ["workspaceId"] = new { type = "keyword" },
                            ["projectId"] = new { type = "keyword" },
                            ["documentId"] = new { type = "keyword" },
                            ["fileName"] = new { type = "keyword" },
                            ["chunkIndex"] = new { type = "integer" },
                            ["pageNumber"] = new { type = "integer" },
                            ["sourceType"] = new { type = "keyword" },
                            ["ocrConfidence"] = new { type = "float" },
                            ["text"] = new { type = "text", analyzer = "standard" },
                            ["keywords"] = new { type = "keyword" },
                            ["keywordsText"] = new { type = "text", analyzer = "standard" },
                            ["embedding"] = new
                            {
                                type = "knn_vector",
                                dimension = Math.Max(8, _options.EmbeddingDimensions),
                                method = new
                                {
                                    name = "hnsw",
                                    space_type = "cosinesimil",
                                    engine = "nmslib"
                                }
                            },
                            ["indexVersion"] = new { type = "integer" },
                            ["documentState"] = new { type = "keyword" },
                            ["updatedAt"] = new { type = "date" }
                        }
                    }
                };

                using var createResponse = await SendJsonAsync(
                    HttpMethod.Put,
                    $"/{Uri.EscapeDataString(_options.IndexName)}",
                    createBody,
                    cancellationToken);
                if (!createResponse.IsSuccessStatusCode)
                {
                    var body = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                    throw new InvalidOperationException($"OpenSearch index creation failed: {(int)createResponse.StatusCode} {body}");
                }
            }
            else if (!indexCheck.IsSuccessStatusCode)
            {
                var body = await indexCheck.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"OpenSearch index check failed: {(int)indexCheck.StatusCode} {body}");
            }

            using var aliasCheck = await SendAsync(HttpMethod.Head, $"/_alias/{Uri.EscapeDataString(_options.IndexAlias)}", null, cancellationToken);
            if (aliasCheck.StatusCode == HttpStatusCode.NotFound)
            {
                using var addAlias = await SendAsync(
                    HttpMethod.Put,
                    $"/{Uri.EscapeDataString(_options.IndexName)}/_alias/{Uri.EscapeDataString(_options.IndexAlias)}",
                    null,
                    cancellationToken);

                if (!addAlias.IsSuccessStatusCode)
                {
                    var body = await addAlias.Content.ReadAsStringAsync(cancellationToken);
                    throw new InvalidOperationException($"OpenSearch alias creation failed: {(int)addAlias.StatusCode} {body}");
                }
            }
            else if (!aliasCheck.IsSuccessStatusCode)
            {
                var body = await aliasCheck.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"OpenSearch alias check failed: {(int)aliasCheck.StatusCode} {body}");
            }

            _indexInitialized = true;
        }
        finally
        {
            _indexInitLock.Release();
        }
    }

    private static object[] BuildFilters(
        Guid tenantId,
        Guid workspaceId,
        Guid projectId,
        IReadOnlyList<Guid> documentIds,
        int indexVersion)
    {
        return new object[]
        {
            new { term = new Dictionary<string, object> { ["tenantId"] = tenantId.ToString("D") } },
            new { term = new Dictionary<string, object> { ["workspaceId"] = workspaceId.ToString("D") } },
            new { term = new Dictionary<string, object> { ["projectId"] = projectId.ToString("D") } },
            new { term = new Dictionary<string, object> { ["indexVersion"] = indexVersion } },
            new { terms = new Dictionary<string, object> { ["documentId"] = documentIds.Select(id => id.ToString("D")).ToArray() } }
        };
    }

    private async Task<IReadOnlyList<OpenSearchChunkSearchHit>> ParseSearchHitsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("hits", out var hitsElement) ||
            !hitsElement.TryGetProperty("hits", out var hitArray) ||
            hitArray.ValueKind != JsonValueKind.Array)
            return Array.Empty<OpenSearchChunkSearchHit>();

        var results = new List<OpenSearchChunkSearchHit>(hitArray.GetArrayLength());
        foreach (var hit in hitArray.EnumerateArray())
        {
            if (!hit.TryGetProperty("_source", out var source))
                continue;

            var chunkUid = source.TryGetProperty("chunkUid", out var chunkUidProp)
                ? chunkUidProp.GetString()
                : null;
            var documentIdValue = source.TryGetProperty("documentId", out var documentIdProp)
                ? documentIdProp.GetString()
                : null;
            var fileName = source.TryGetProperty("fileName", out var fileNameProp)
                ? fileNameProp.GetString()
                : null;
            var text = source.TryGetProperty("text", out var textProp)
                ? textProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(chunkUid) ||
                string.IsNullOrWhiteSpace(documentIdValue) ||
                !Guid.TryParse(documentIdValue, out var documentId) ||
                string.IsNullOrWhiteSpace(fileName) ||
                string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var chunkIndex = source.TryGetProperty("chunkIndex", out var chunkIndexProp) ? chunkIndexProp.GetInt32() : 0;
            var pageNumber = source.TryGetProperty("pageNumber", out var pageProp) && pageProp.ValueKind != JsonValueKind.Null
                ? pageProp.GetInt32()
                : (int?)null;
            var sourceType = source.TryGetProperty("sourceType", out var sourceTypeProp)
                ? sourceTypeProp.GetString() ?? "text"
                : "text";
            var ocrConfidence = source.TryGetProperty("ocrConfidence", out var confProp) && confProp.ValueKind != JsonValueKind.Null
                ? confProp.GetDouble()
                : (double?)null;
            var indexVersion = source.TryGetProperty("indexVersion", out var versionProp) ? versionProp.GetInt32() : 1;
            var score = hit.TryGetProperty("_score", out var scoreProp) ? scoreProp.GetDouble() : 0d;

            results.Add(new OpenSearchChunkSearchHit(
                chunkUid,
                documentId,
                fileName,
                chunkIndex,
                pageNumber,
                sourceType,
                ocrConfidence,
                text,
                indexVersion,
                score));
        }

        return results;
    }

    private Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(body, JsonWriteOptions);
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        return SendAsync(request, cancellationToken);
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };

        return SendAsync(request, cancellationToken);
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AttachAuthHeader(request);
        var client = _httpClientFactory.CreateClient("OpenSearch");
        return client.SendAsync(request, cancellationToken);
    }

    private void AttachAuthHeader(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_options.Username))
            return;

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password ?? string.Empty}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }
}
