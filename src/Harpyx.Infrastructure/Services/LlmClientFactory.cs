using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Harpyx.Infrastructure.Services;

public class LlmClientFactory : ILlmClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public LlmClientFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    public ILlmClient Create(LlmProvider provider, string apiKey)
        => Create(provider, apiKey, null);

    public ILlmClient Create(LlmProvider provider, string apiKey, string? baseUrl)
        => provider switch
        {
            LlmProvider.OpenAI => new OpenAiLlmClient(
                apiKey,
                _httpClientFactory.CreateClient("OpenAI"),
                _loggerFactory.CreateLogger<OpenAiLlmClient>()),

            LlmProvider.Claude => new ClaudeLlmClient(
                apiKey,
                _httpClientFactory.CreateClient("Claude"),
                _loggerFactory.CreateLogger<ClaudeLlmClient>()),

            LlmProvider.Google => new GoogleGeminiLlmClient(
                apiKey,
                _httpClientFactory.CreateClient("Google"),
                _loggerFactory.CreateLogger<GoogleGeminiLlmClient>()),

            LlmProvider.OpenAICompatible => new OpenAiLlmClient(
                apiKey,
                _httpClientFactory.CreateClient("OpenAICompatible"),
                _loggerFactory.CreateLogger<OpenAiLlmClient>(),
                baseUrl,
                providerLabel: "OpenAI-compatible"),

            LlmProvider.AzureOpenAI => new OpenAiLlmClient(
                apiKey,
                _httpClientFactory.CreateClient("AzureOpenAI"),
                _loggerFactory.CreateLogger<OpenAiLlmClient>(),
                RequireBaseUrl(provider, baseUrl),
                apiKeyHeaderName: "api-key",
                providerLabel: "Azure OpenAI"),

            LlmProvider.AmazonBedrock => new OpenAiLlmClient(
                apiKey,
                _httpClientFactory.CreateClient("Bedrock"),
                _loggerFactory.CreateLogger<OpenAiLlmClient>(),
                RequireBaseUrl(provider, baseUrl),
                providerLabel: "Amazon Bedrock"),

            _ => throw new InvalidOperationException($"Unsupported LLM provider: {provider}")
        };

    public ILlmClient Create(LlmConnection connection, string apiKey)
        => connection.Provider switch
        {
            LlmProvider.OpenAICompatible or LlmProvider.AzureOpenAI or LlmProvider.AmazonBedrock => Create(
                connection.Provider,
                apiKey,
                connection.BaseUrl),

            _ => Create(connection.Provider, apiKey)
        };

    private static string RequireBaseUrl(LlmProvider provider, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException($"{provider} requires a base URL.");

        return baseUrl.Trim();
    }
}
