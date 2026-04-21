namespace Harpyx.Application.Interfaces;

public interface IAuditService
{
    Task RecordAsync(string eventType, string? userObjectId, string? userEmail, string? details, CancellationToken cancellationToken);
}
