using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Harpyx.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public sealed class ClaudeLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ClaudeLlmClient> _logger;
    private const string DefaultModel = "claude-sonnet-4-5-20250929";

    public ClaudeLlmClient(string apiKey, HttpClient http, ILogger<ClaudeLlmClient> logger)
    {
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
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
                return new LlmCompletionResult(false, null, "Claude API returned an empty response.");

            return new LlmCompletionResult(true, text, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude API call failed");
            return new LlmCompletionResult(false, null, "Claude API call failed.");
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
            max_tokens = 4096,
            system = systemPrompt,
            stream = true,
            messages = new[]
            {
                new { role = "user", content = userMessage }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
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
            var apiError = await BuildApiErrorAsync("Claude", response, cancellationToken);
            _logger.LogWarning("Claude streaming API returned {StatusCode}: {Message}", response.StatusCode, apiError.Message);
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

            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeElement) &&
                string.Equals(typeElement.GetString(), "error", StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("error", out var errorElement) &&
                    errorElement.TryGetProperty("message", out var messageElement) &&
                    messageElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(messageElement.GetString()))
                {
                    throw new InvalidOperationException(messageElement.GetString());
                }

                throw new InvalidOperationException("Claude API returned a streaming error.");
            }

            if (!root.TryGetProperty("delta", out var deltaElement))
                continue;

            if (!deltaElement.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
                continue;

            var chunk = textElement.GetString();
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
