using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Harpyx.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public sealed class OpenAiLlmClient : ILlmClient
{
    private readonly string _apiKey;
    private readonly string? _apiKeyHeaderName;
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiLlmClient> _logger;
    private readonly string _baseUrl;
    private readonly string _providerLabel;
    private const string DefaultModel = "gpt-4o";

    public OpenAiLlmClient(
        string apiKey,
        HttpClient http,
        ILogger<OpenAiLlmClient> logger,
        string? baseUrl = null,
        string? apiKeyHeaderName = null,
        string providerLabel = "OpenAI")
    {
        _apiKey = apiKey;
        _apiKeyHeaderName = apiKeyHeaderName;
        _http = http;
        _logger = logger;
        _baseUrl = NormalizeBaseUrl(baseUrl);
        _providerLabel = providerLabel;
    }

    public async Task<LlmCompletionResult> ChatCompletionAsync(
        string systemPrompt, string userMessage, string? model, CancellationToken cancellationToken)
    {
        try
        {
            var completion = new StringBuilder();
            await foreach (var chunk in ChatCompletionStreamAsync(systemPrompt, userMessage, model, cancellationToken))
            {
                completion.Append(chunk);
            }

            var text = completion.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return new LlmCompletionResult(false, null, $"{_providerLabel} API returned an empty response.");

            return new LlmCompletionResult(true, text, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Provider} API call failed", _providerLabel);
            return new LlmCompletionResult(false, null, $"{_providerLabel} API call failed.");
        }
    }

    public async IAsyncEnumerable<string> ChatCompletionStreamAsync(
        string systemPrompt,
        string userMessage,
        string? model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = model ?? DefaultModel,
            stream = true,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        AddApiKeyHeader(request);

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var apiError = await BuildApiErrorAsync(_providerLabel, response, cancellationToken);
            _logger.LogWarning("{Provider} streaming API returned {StatusCode}: {Message}", _providerLabel, response.StatusCode, apiError.Message);
            throw apiError;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            if (!TryGetSseData(line, out var data))
                continue;

            if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
                yield break;

            using var doc = JsonDocument.Parse(data);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                continue;

            var choice = choices[0];
            if (!choice.TryGetProperty("delta", out var delta))
                continue;

            if (!delta.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.String)
                continue;

            var chunk = contentElement.GetString();
            if (!string.IsNullOrEmpty(chunk))
                yield return chunk;
        }
    }

    private static bool TryGetSseData(string line, out string data)
    {
        data = string.Empty;
        if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        data = line[5..].Trim();
        return data.Length > 0;
    }

    private static string NormalizeBaseUrl(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1"
            : baseUrl.Trim().TrimEnd('/');

    private void AddApiKeyHeader(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return;

        if (string.Equals(_apiKeyHeaderName, "api-key", StringComparison.OrdinalIgnoreCase))
            request.Headers.Add("api-key", _apiKey);
        else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    private static async Task<InvalidOperationException> BuildApiErrorAsync(
        string provider,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = $"{(int)response.StatusCode} {response.StatusCode}";
        if (string.IsNullOrWhiteSpace(body))
            return new InvalidOperationException($"{provider} API error: {status}");

        var trimmed = body.Length <= 512 ? body : $"{body[..512]}...";
        return new InvalidOperationException($"{provider} API error: {status}. {trimmed}");
    }
}
