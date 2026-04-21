using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;

namespace Harpyx.Infrastructure.Services;

public class LlmOcrSmokeTestService : ILlmOcrSmokeTestService
{
    private const string SmokeTestImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+b5N8AAAAASUVORK5CYII=";

    private readonly ILlmOcrService _ocrService;

    public LlmOcrSmokeTestService(ILlmOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task ValidateAsync(
        LlmProvider provider,
        string apiKey,
        string? model,
        CancellationToken cancellationToken)
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"harpyx-ocr-smoke-{Guid.NewGuid():N}.png");
        try
        {
            var imageBytes = Convert.FromBase64String(SmokeTestImageBase64);
            await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken);

            _ = await _ocrService.ExtractImageTextAsync(
                imagePath,
                "en",
                new OcrModelContext(provider, apiKey, model),
                cancellationToken);
        }
        finally
        {
            if (File.Exists(imagePath))
                File.Delete(imagePath);
        }
    }
}
