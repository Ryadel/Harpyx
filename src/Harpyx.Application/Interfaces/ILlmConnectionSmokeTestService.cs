using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Interfaces;

public interface ILlmConnectionSmokeTestService
{
    Task ValidateAsync(
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
        CancellationToken cancellationToken);

    Task ValidateModelAsync(
        LlmModel model,
        string apiKey,
        CancellationToken cancellationToken);

    Task ValidateConfiguredModelsAsync(
        LlmConnection connection,
        string apiKey,
        CancellationToken cancellationToken);
}
