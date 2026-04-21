using Harpyx.Domain.Enums;

namespace Harpyx.Application.DTOs;

public record TenantMembershipDto(
    Guid UserId,
    Guid TenantId,
    TenantRole TenantRole,
    bool CanGrant,
    Guid? GrantedByUserId,
    string? GrantedByEmail,
    DateTimeOffset? GrantedAt);

public record TenantMemberDto(
    Guid UserId,
    string Email,
    bool IsActive,
    TenantRole TenantRole,
    bool CanGrant,
    Guid? GrantedByUserId,
    string? GrantedByEmail,
    DateTimeOffset? GrantedAt);

public record SaveTenantMembershipRequest(
    Guid UserId,
    TenantRole TenantRole);
