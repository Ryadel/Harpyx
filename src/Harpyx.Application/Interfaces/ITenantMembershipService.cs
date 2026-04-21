using Harpyx.Application.DTOs;

namespace Harpyx.Application.Interfaces;

public interface ITenantMembershipService
{
    Task<IReadOnlyList<TenantMemberDto>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken);
    Task SaveAsync(Guid actorUserId, Guid tenantId, SaveTenantMembershipRequest request, bool isPlatformAdmin, CancellationToken cancellationToken);
    Task RevokeAsync(Guid actorUserId, Guid tenantId, Guid targetUserId, bool isPlatformAdmin, CancellationToken cancellationToken);
}
