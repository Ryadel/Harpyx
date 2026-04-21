using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;

namespace Harpyx.Application.Services;

public class AuditService : IAuditService
{
    private readonly IAuditEventRepository _audits;
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(IAuditEventRepository audits, IUnitOfWork unitOfWork)
    {
        _audits = audits;
        _unitOfWork = unitOfWork;
    }

    public async Task RecordAsync(string eventType, string? userObjectId, string? userEmail, string? details, CancellationToken cancellationToken)
    {
        var auditEvent = new AuditEvent
        {
            EventType = eventType,
            UserObjectId = userObjectId,
            UserEmail = userEmail,
            Details = details
        };

        await _audits.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
