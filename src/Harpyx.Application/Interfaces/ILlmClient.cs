namespace Harpyx.Application.Interfaces;

using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

public interface ILlmClient
{
    Task<LlmCompletionResult> ChatCompletionAsync(
        string systemPrompt,
        string userMessage,
        string? model,
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> ChatCompletionStreamAsync(
        string systemPrompt,
        string userMessage,
        string? model,
        CancellationToken cancellationToken);
}

public record LlmCompletionResult(bool Success, string? Content, string? Error);

public interface ILlmClientFactory
{
    ILlmClient Create(LlmProvider provider, string apiKey);
    ILlmClient Create(LlmProvider provider, string apiKey, string? baseUrl);
    ILlmClient Create(LlmConnection connection, string apiKey);
}
