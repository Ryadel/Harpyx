using Harpyx.Application.DTOs;
using Harpyx.Application.Filters;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenants;
    private readonly IUserTenantRepository _userTenants;
    private readonly IUserRepository _users;
    private readonly IUsageLimitService _usageLimits;
    private readonly IUnitOfWork _unitOfWork;

    public TenantService(
        ITenantRepository tenants,
        IUserTenantRepository userTenants,
        IUserRepository users,
        IUsageLimitService usageLimits,
        IUnitOfWork unitOfWork)
    {
        _tenants = tenants;
        _userTenants = userTenants;
        _users = users;
        _usageLimits = usageLimits;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<TenantDto>> GetAsync(TenantFilter filter, CancellationToken cancellationToken)
    {
        var tenants = await _tenants.GetAsync(filter, cancellationToken);
        return tenants.Select(t => new TenantDto(t.Id, t.Name, t.IsActive, t.IsVisibleToAllUsers, t.IsPersonal, t.CreatedByUserId)).ToList();
    }

    public async Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken);
        return tenant is null ? null : new TenantDto(tenant.Id, tenant.Name, tenant.IsActive, tenant.IsVisibleToAllUsers, tenant.IsPersonal, tenant.CreatedByUserId);
    }

    public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var ownerUserId = request.CreatedByUserId;
        if (ownerUserId is Guid ownerId && await _users.GetByIdAsync(ownerId, cancellationToken) is null)
            throw new InvalidOperationException("Selected owner user was not found.");

        if (ownerUserId is Guid createdByUserId)
        {
            await _usageLimits.EnsureTenantCreationAllowedAsync(createdByUserId, cancellationToken);
        }

        var tenant = new Tenant
        {
            CreatedByUserId = request.CreatedByUserId,
            Name = request.Name,
            IsActive = request.IsActive,
            IsVisibleToAllUsers = request.IsVisibleToAllUsers,
            IsPersonal = false
        };

        await _tenants.AddAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (ownerUserId is Guid ownerIdForMembership)
        {
            await _userTenants.AddOrUpdateMembershipAsync(
                ownerIdForMembership,
                tenant.Id,
                TenantRole.TenantOwner,
                canGrant: true,
                grantedByUserId: ownerIdForMembership,
                grantedAt: DateTimeOffset.UtcNow,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new TenantDto(tenant.Id, tenant.Name, tenant.IsActive, tenant.IsVisibleToAllUsers, tenant.IsPersonal, tenant.CreatedByUserId);
    }

    public async Task<TenantDto> UpdateAsync(TenantDto tenant, CancellationToken cancellationToken)
    {
        var entity = await _tenants.GetByIdAsync(tenant.Id, cancellationToken) ?? throw new InvalidOperationException("Tenant not found.");
        if (tenant.CreatedByUserId is Guid ownerUserId && await _users.GetByIdAsync(ownerUserId, cancellationToken) is null)
            throw new InvalidOperationException("Selected owner user was not found.");

        entity.Name = tenant.Name;
        entity.IsActive = tenant.IsActive;
        entity.IsVisibleToAllUsers = tenant.IsVisibleToAllUsers;
        entity.CreatedByUserId = tenant.CreatedByUserId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (tenant.CreatedByUserId is Guid newOwnerUserId)
        {
            await _userTenants.AddOrUpdateMembershipAsync(
                newOwnerUserId,
                tenant.Id,
                TenantRole.TenantOwner,
                canGrant: true,
                grantedByUserId: newOwnerUserId,
                grantedAt: DateTimeOffset.UtcNow,
                cancellationToken);
        }

        _tenants.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new TenantDto(entity.Id, entity.Name, entity.IsActive, entity.IsVisibleToAllUsers, entity.IsPersonal, entity.CreatedByUserId);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _tenants.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Tenant not found.");
        if (entity.IsPersonal)
            throw new InvalidOperationException("Personal tenant cannot be deleted. You can rename it.");

        _tenants.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
