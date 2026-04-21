using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class LlmConnectionSmokeTestService : ILlmConnectionSmokeTestService
{
    private readonly IEmbeddingService _embedding;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly ILlmOcrSmokeTestService _ocrSmokeTest;

    public LlmConnectionSmokeTestService(
        IEmbeddingService embedding,
        ILlmClientFactory llmClientFactory,
        ILlmOcrSmokeTestService ocrSmokeTest)
    {
        _embedding = embedding;
        _llmClientFactory = llmClientFactory;
        _ocrSmokeTest = ocrSmokeTest;
    }

    public async Task ValidateAsync(
        LlmProvider provider,
        string apiKey,
        string? baseUrl,
        bool enableChat,
        string? chatModel,
        bool enableRagEmbedding,
        string? ragEmbeddingModel,
        bool enableOcr,
        string? ocrModel,
        string displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

            if (enableRagEmbedding)
            {
                _ = await _embedding.EmbedAsync(
                    new[] { "ping" },
                    provider,
                    apiKey,
                    NormalizeModel(ragEmbeddingModel),
                    cancellationToken,
                    normalizedBaseUrl);
            }

            if (enableChat)
            {
                var client = RequiresBaseUrl(provider)
                    ? _llmClientFactory.Create(provider, apiKey, normalizedBaseUrl)
                    : _llmClientFactory.Create(provider, apiKey);
                var completion = await client.ChatCompletionAsync(
                    systemPrompt: "You are a connectivity test. Reply with 'ok'.",
                    userMessage: "ok",
                    model: NormalizeModel(chatModel),
                    cancellationToken: cancellationToken);

                if (!completion.Success)
                    throw new InvalidOperationException(completion.Error ?? "Chat test failed.");
            }

            if (enableOcr)
            {
                await _ocrSmokeTest.ValidateAsync(
                    provider,
                    apiKey,
                    NormalizeModel(ocrModel),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Smoke test failed for {displayName}. Verify API key, endpoint, and selected models. Details: {ex.Message}");
        }
    }

    public Task ValidateModelAsync(
        LlmModel model,
        string apiKey,
        CancellationToken cancellationToken)
    {
        return ValidateAsync(
            model.Connection.Provider,
            apiKey,
            model.Connection.BaseUrl,
            enableChat: model.Capability == LlmProviderType.Chat,
            chatModel: model.ModelId,
            enableRagEmbedding: model.Capability == LlmProviderType.RagEmbedding,
            ragEmbeddingModel: model.ModelId,
            enableOcr: model.Capability == LlmProviderType.Ocr,
            ocrModel: model.ModelId,
            displayName: model.GetName(),
            cancellationToken);
    }

    public async Task ValidateConfiguredModelsAsync(
        LlmConnection connection,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var tasks = connection.Models
            .Where(m => m.IsEnabled && m.IsPublished)
            .Select(m => ValidateModelAsync(m, apiKey, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private static string? NormalizeModel(string? model)
        => string.IsNullOrWhiteSpace(model) ? null : model.Trim();

    private static string? NormalizeBaseUrl(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/');

    private static bool RequiresBaseUrl(LlmProvider provider)
        => provider is LlmProvider.OpenAICompatible or LlmProvider.AzureOpenAI or LlmProvider.AmazonBedrock;
}
