using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Harpyx.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public sealed class GoogleGeminiLlmClient : ILlmClient
{
    private readonly string _apiKey;
    private readonly HttpClient _http;
    private readonly ILogger<GoogleGeminiLlmClient> _logger;
    private const string DefaultModel = "gemini-1.5-pro";

    public GoogleGeminiLlmClient(string apiKey, HttpClient http, ILogger<GoogleGeminiLlmClient> logger)
    {
        _apiKey = apiKey;
        _http = http;
        _logger = logger;
    }

    public async Task<LlmCompletionResult> ChatCompletionAsync(
        string systemPrompt,
        string userMessage,
        string? model,
        CancellationToken cancellationToken)
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
                return new LlmCompletionResult(false, null, "Google Gemini API returned an empty response.");

            return new LlmCompletionResult(true, text, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Gemini API call failed");
            return new LlmCompletionResult(false, null, "Google Gemini API call failed.");
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
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = $"{systemPrompt}\n\n{userMessage}" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildStreamUrl(model ?? DefaultModel))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var apiError = await BuildApiErrorAsync("Google Gemini", response, cancellationToken);
            _logger.LogWarning("Google Gemini streaming API returned {StatusCode}: {Message}", response.StatusCode, apiError.Message);
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
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                var message = errorElement.TryGetProperty("message", out var messageElement) &&
                              messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString()
                    : null;

                throw new InvalidOperationException(message ?? "Google Gemini API returned a streaming error.");
            }

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var contentElement))
                    continue;

                if (!contentElement.TryGetProperty("parts", out var partsElement) || partsElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var part in partsElement.EnumerateArray())
                {
                    if (!part.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
                        continue;

                    var chunk = textElement.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                        yield return chunk;
                }
            }
        }
    }

    private string BuildStreamUrl(string selectedModel)
    {
        return $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(selectedModel)}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(_apiKey)}";
    }

    private static bool TryGetSseData(string line, out string data)
    {
        data = string.Empty;
        if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        data = line[5..].Trim();
        return data.Length > 0;
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
