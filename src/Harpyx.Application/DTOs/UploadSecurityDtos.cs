namespace Harpyx.Application.DTOs;

public record UploadValidationResult(
    bool IsAccepted,
    string NormalizedContentType,
    string? RejectionReason = null);

public enum MalwareScanVerdict
{
    Skipped = 0,
    Clean = 1,
    Infected = 2,
    Error = 3
}

public record MalwareScanResult(
    MalwareScanVerdict Verdict,
    string? Signature = null,
    string? Details = null);
