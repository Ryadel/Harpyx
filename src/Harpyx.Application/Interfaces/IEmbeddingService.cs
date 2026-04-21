using Harpyx.Domain.Enums;
using Harpyx.Domain.Entities;

namespace Harpyx.Application.Interfaces;

public interface IEmbeddingService
{
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        LlmProvider provider,
        string apiKey,
        string? model,
        CancellationToken cancellationToken,
        string? baseUrl = null);

    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        LlmModel model,
        string apiKey,
        CancellationToken cancellationToken);
}
