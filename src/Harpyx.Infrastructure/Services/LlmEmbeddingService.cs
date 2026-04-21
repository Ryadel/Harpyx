using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public class LlmEmbeddingService : IEmbeddingService
{
    private readonly RagOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlmEmbeddingService> _logger;

    public LlmEmbeddingService(
        RagOptions options,
        IHttpClientFactory httpClientFactory,
        ILogger<LlmEmbeddingService> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        LlmProvider provider,
        string apiKey,
        string? model,
        CancellationToken cancellationToken,
        string? baseUrl = null)
    {
        if (inputs.Count == 0)
            return Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());

        return provider switch
        {
            LlmProvider.OpenAI => EmbedOpenAiAsync(inputs, apiKey, model ?? _options.OpenAiEmbeddingModel, cancellationToken),
            LlmProvider.OpenAICompatible => EmbedOpenAiCompatibleAsync(
                inputs,
                apiKey,
                model ?? _options.OpenAiEmbeddingModel,
                baseUrl,
                "OpenAI-compatible",
                null,
                cancellationToken),
            LlmProvider.AzureOpenAI => EmbedOpenAiCompatibleAsync(
                inputs,
                apiKey,
                model ?? _options.OpenAiEmbeddingModel,
                baseUrl,
                "Azure OpenAI",
                "api-key",
                cancellationToken),
            LlmProvider.Google => EmbedGoogleAsync(inputs, apiKey, model ?? _options.GoogleEmbeddingModel, cancellationToken),
            _ => throw new InvalidOperationException($"{provider} does not provide embedding support in this implementation.")
        };
    }

    public Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        LlmModel model,
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (model.Connection.Provider is LlmProvider.OpenAICompatible or LlmProvider.AzureOpenAI)
        {
            return EmbedOpenAiCompatibleAsync(
                inputs,
                apiKey,
                model.ModelId,
                model.Connection.BaseUrl,
                model.Connection.Provider == LlmProvider.AzureOpenAI
                    ? "Azure OpenAI"
                    : "OpenAI-compatible",
                model.Connection.Provider == LlmProvider.AzureOpenAI
                    ? "api-key"
                    : null,
                cancellationToken);
        }

        return EmbedAsync(inputs, model.Connection.Provider, apiKey, model.ModelId, cancellationToken);
    }

    private async Task<IReadOnlyList<float[]>> EmbedOpenAiAsync(
        IReadOnlyList<string> inputs,
        string apiKey,
        string model,
        CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient("OpenAIEmbeddings");

        var requestBody = new
        {
            model,
            input = inputs
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI embeddings API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"OpenAI embeddings API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            throw new InvalidOperationException("OpenAI embeddings API returned no data.");

        var vectors = new List<float[]>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            vectors.Add(ParseVector(item.GetProperty("embedding")));
        }

        return vectors;
    }

    private async Task<IReadOnlyList<float[]>> EmbedOpenAiCompatibleAsync(
        IReadOnlyList<string> inputs,
        string apiKey,
        string model,
        string? baseUrl,
        string providerLabel,
        string? apiKeyHeaderName,
        CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient("OpenAIEmbeddings");

        var requestBody = new
        {
            model,
            input = inputs
        };

        var url = $"{NormalizeBaseUrl(baseUrl)}/embeddings";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        AddApiKeyHeader(request, apiKey, apiKeyHeaderName);

        using var response = await http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{Provider} embeddings API returned {StatusCode}: {Body}", providerLabel, response.StatusCode, responseBody);
            throw new InvalidOperationException($"{providerLabel} embeddings API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            throw new InvalidOperationException($"{providerLabel} embeddings API returned no data.");

        var vectors = new List<float[]>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            vectors.Add(ParseVector(item.GetProperty("embedding")));
        }

        return vectors;
    }

    private async Task<IReadOnlyList<float[]>> EmbedGoogleAsync(
        IReadOnlyList<string> inputs,
        string apiKey,
        string model,
        CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient("GoogleEmbeddings");
        var vectors = new List<float[]>(inputs.Count);
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:embedContent?key={Uri.EscapeDataString(apiKey)}";

        foreach (var input in inputs)
        {
            var requestBody = new
            {
                content = new
                {
                    parts = new[]
                    {
                        new { text = input }
                    }
                }
            };

            using var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google embeddings API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"Google embeddings API error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("embedding", out var embedding))
                throw new InvalidOperationException("Google embeddings API returned no embedding payload.");

            vectors.Add(ParseVector(embedding.GetProperty("values")));
        }

        return vectors;
    }

    private static float[] ParseVector(JsonElement values)
    {
        var vector = new float[values.GetArrayLength()];
        var idx = 0;
        foreach (var value in values.EnumerateArray())
        {
            vector[idx++] = value.GetSingle();
        }

        return vector;
    }

    private static string NormalizeBaseUrl(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1"
            : baseUrl.Trim().TrimEnd('/');

    private static void AddApiKeyHeader(
        HttpRequestMessage request,
        string apiKey,
        string? apiKeyHeaderName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        if (string.Equals(apiKeyHeaderName, "api-key", StringComparison.OrdinalIgnoreCase))
            request.Headers.Add("api-key", apiKey);
        else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }
}
