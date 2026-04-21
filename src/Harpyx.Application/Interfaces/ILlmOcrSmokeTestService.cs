using Harpyx.Domain.Enums;

namespace Harpyx.Application.Interfaces;

public interface ILlmOcrSmokeTestService
{
    Task ValidateAsync(
        LlmProvider provider,
        string apiKey,
        string? model,
        CancellationToken cancellationToken);
}
