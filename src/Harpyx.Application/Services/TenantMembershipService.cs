using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class TenantMembershipService : ITenantMembershipService
{
    private readonly IUserTenantRepository _userTenants;
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IUnitOfWork _unitOfWork;

    public TenantMembershipService(
        IUserTenantRepository userTenants,
        IUserRepository users,
        ITenantRepository tenants,
        IUnitOfWork unitOfWork)
    {
        _userTenants = userTenants;
        _users = users;
        _tenants = tenants;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<TenantMemberDto>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var memberships = await _userTenants.GetMembershipsByTenantIdAsync(tenantId, cancellationToken);
        return memberships
            .OrderBy(m => m.TenantRole)
            .ThenBy(m => m.User.Email)
            .Select(MapMember)
            .ToList();
    }

    public async Task SaveAsync(Guid actorUserId, Guid tenantId, SaveTenantMembershipRequest request, bool isPlatformAdmin, CancellationToken cancellationToken)
    {
        var actorMembership = await GetActorMembershipAsync(actorUserId, tenantId, isPlatformAdmin, cancellationToken);

        if (!isPlatformAdmin)
        {
            ValidateCanManageMembers(actorMembership!);
            ValidateCanAssignRole(actorMembership!.TenantRole, request.TenantRole);
        }

        var targetUser = await _users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Target user not found.");
        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        if (!targetUser.IsActive)
            throw new InvalidOperationException("Cannot assign permissions to an inactive user.");

        if (tenant.IsPersonal &&
            tenant.CreatedByUserId == request.UserId &&
            request.TenantRole != TenantRole.TenantOwner)
        {
            throw new InvalidOperationException("You cannot change the owner role on a personal tenant.");
        }

        var existing = await _userTenants.GetMembershipAsync(request.UserId, tenantId, cancellationToken);
        if (!isPlatformAdmin && existing is not null && existing.TenantRole < actorMembership!.TenantRole)
            throw new InvalidOperationException("You cannot modify a member with higher privileges.");

        if (existing is not null &&
            existing.TenantRole == TenantRole.TenantOwner &&
            request.TenantRole != TenantRole.TenantOwner)
        {
            var ownerCount = await _userTenants.CountMembersByRoleAsync(tenantId, TenantRole.TenantOwner, cancellationToken);
            if (ownerCount <= 1)
                throw new InvalidOperationException("At least one Tenant Owner is required.");
        }

        var canGrant = CanGrantForRole(request.TenantRole);

        await _userTenants.AddOrUpdateMembershipAsync(
            request.UserId,
            tenantId,
            request.TenantRole,
            canGrant,
            actorUserId,
            DateTimeOffset.UtcNow,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(Guid actorUserId, Guid tenantId, Guid targetUserId, bool isPlatformAdmin, CancellationToken cancellationToken)
    {
        var actorMembership = await GetActorMembershipAsync(actorUserId, tenantId, isPlatformAdmin, cancellationToken);
        var targetMembership = await _userTenants.GetMembershipAsync(targetUserId, tenantId, cancellationToken);
        if (targetMembership is null)
            return;

        if (!isPlatformAdmin)
        {
            ValidateCanManageMembers(actorMembership!);
            if (targetMembership.TenantRole < actorMembership!.TenantRole)
                throw new InvalidOperationException("You cannot revoke a member with higher privileges.");
        }

        if (targetMembership.TenantRole == TenantRole.TenantOwner)
        {
            var ownerCount = await _userTenants.CountMembersByRoleAsync(tenantId, TenantRole.TenantOwner, cancellationToken);
            if (ownerCount <= 1)
                throw new InvalidOperationException("At least one Tenant Owner is required.");
        }

        var visited = new HashSet<Guid>();
        await RevokeRecursiveAsync(tenantId, targetUserId, visited, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserTenant?> GetActorMembershipAsync(Guid actorUserId, Guid tenantId, bool isPlatformAdmin, CancellationToken cancellationToken)
    {
        if (isPlatformAdmin)
            return null;

        var membership = await _userTenants.GetMembershipAsync(actorUserId, tenantId, cancellationToken);
        if (membership is null)
            throw new InvalidOperationException("You are not a member of the current tenant.");

        return membership;
    }

    private static TenantMemberDto MapMember(UserTenant membership)
        => new(
            membership.UserId,
            membership.User.Email,
            membership.User.IsActive,
            membership.TenantRole,
            membership.CanGrant,
            membership.GrantedByUserId,
            membership.GrantedByUser?.Email,
            membership.GrantedAt);

    private static void ValidateCanManageMembers(UserTenant membership)
    {
        var canManage = membership.TenantRole <= TenantRole.TenantManager && membership.CanGrant;
        if (!canManage)
            throw new InvalidOperationException("You do not have permission to manage tenant members.");
    }

    private static void ValidateCanAssignRole(TenantRole actorRole, TenantRole targetRole)
    {
        if (targetRole < actorRole)
            throw new InvalidOperationException("You cannot assign a role higher than your own.");
    }

    private static bool CanGrantForRole(TenantRole role)
        => role is TenantRole.TenantOwner or TenantRole.TenantManager;

    private async Task RevokeRecursiveAsync(Guid tenantId, Guid userId, HashSet<Guid> visited, CancellationToken cancellationToken)
    {
        if (!visited.Add(userId))
            return;

        var delegated = await _userTenants.GetMembershipsGrantedByUserAsync(tenantId, userId, cancellationToken);
        foreach (var child in delegated)
        {
            await RevokeRecursiveAsync(tenantId, child.UserId, visited, cancellationToken);
        }

        await _userTenants.RemoveMembershipAsync(userId, tenantId, cancellationToken);
    }
}
