using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface IUploadSecurityPolicyService
{
    Task<UploadValidationResult> ValidateAsync(
        string fileName,
        string? contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken);
}
