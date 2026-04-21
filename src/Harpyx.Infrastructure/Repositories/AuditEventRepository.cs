using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Infrastructure.Data;

namespace Harpyx.Infrastructure.Repositories;

public class AuditEventRepository : IAuditEventRepository
{
    private readonly HarpyxDbContext _dbContext;

    public AuditEventRepository(HarpyxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        => _dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken).AsTask();
}
