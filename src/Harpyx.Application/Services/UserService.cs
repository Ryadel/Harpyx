using Harpyx.Application.DTOs;
using Harpyx.Application.Interfaces;
using Harpyx.Domain.Entities;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Services;

public class UserService : IUserService
{
    private const string PersonalTenantName = "Personal";

    private readonly IUserRepository _users;
    private readonly IUserTenantRepository _userTenants;
    private readonly ITenantRepository _tenants;
    private readonly IProjectRepository _projects;
    private readonly IDocumentRepository _documents;
    private readonly IStorageService _storage;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly IUserInvitationRepository _invitations;
    private readonly ILlmCatalogRepository _llmCatalog;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(
        IUserRepository users,
        IUserTenantRepository userTenants,
        ITenantRepository tenants,
        IProjectRepository projects,
        IDocumentRepository documents,
        IStorageService storage,
        IPlatformSettingsService platformSettings,
        IUserInvitationRepository invitations,
        ILlmCatalogRepository llmCatalog,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _userTenants = userTenants;
        _tenants = tenants;
        _projects = projects;
        _documents = documents;
        _storage = storage;
        _platformSettings = platformSettings;
        _invitations = invitations;
        _llmCatalog = llmCatalog;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> IsAuthorizedAsync(string? objectId, string? subjectId, string email, string? loginProvider, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        User? user = null;

        if (!string.IsNullOrWhiteSpace(objectId))
            user = await _users.GetByObjectIdAsync(objectId, cancellationToken);

        if (user is null && !string.IsNullOrWhiteSpace(subjectId))
            user = await _users.GetBySubjectIdAsync(subjectId, cancellationToken);

        if (user is null && !string.IsNullOrWhiteSpace(email))
            user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            var now = DateTimeOffset.UtcNow;
            var pendingInvitation = await _invitations.GetLatestPendingByEmailAsync(email.Trim(), now, cancellationToken);
            var allowSelfRegistration = await _platformSettings.IsSelfRegistrationAllowedAsync(cancellationToken);
            if (!allowSelfRegistration && pendingInvitation is null)
                return false;

            user = new User
            {
                ObjectId = string.IsNullOrWhiteSpace(objectId) ? null : objectId,
                SubjectId = string.IsNullOrWhiteSpace(subjectId) ? null : subjectId,
                Email = email.Trim(),
                Role = UserRole.Standard,
                IsActive = true,
                LastLoginAt = now,
                LastLoginProvider = string.IsNullOrWhiteSpace(loginProvider) ? null : loginProvider
            };

            await _users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var tenant = new Tenant
            {
                CreatedByUserId = user.Id,
                Name = PersonalTenantName,
                IsActive = true,
                IsVisibleToAllUsers = false,
                IsPersonal = true
            };

            await _tenants.AddAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _userTenants.AddOrUpdateMembershipAsync(
                user.Id,
                tenant.Id,
                TenantRole.TenantOwner,
                canGrant: true,
                grantedByUserId: user.Id,
                grantedAt: now,
                cancellationToken);

            if (pendingInvitation?.Scope == UserInvitationScope.TenantMembership && pendingInvitation.TenantId is Guid invitedTenantId)
            {
                var invitedTenant = await _tenants.GetByIdAsync(invitedTenantId, cancellationToken);
                if (invitedTenant is not null)
                {
                    await _userTenants.AddOrUpdateMembershipAsync(
                        user.Id,
                        invitedTenantId,
                        TenantRole.Viewer,
                        canGrant: false,
                        grantedByUserId: pendingInvitation.InvitedByUserId,
                        grantedAt: now,
                        cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (pendingInvitation is not null)
            {
                pendingInvitation.Status = UserInvitationStatus.Accepted;
                pendingInvitation.AcceptedAt = now;
                pendingInvitation.AcceptedUserId = user.Id;
                pendingInvitation.UpdatedAt = now;
                _invitations.Update(pendingInvitation);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }

        if (user.IsActive)
        {
            bool updated = false;
            if (string.IsNullOrWhiteSpace(user.ObjectId) && !string.IsNullOrWhiteSpace(objectId))
            {
                user.ObjectId = objectId;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(user.SubjectId) && !string.IsNullOrWhiteSpace(subjectId))
            {
                user.SubjectId = subjectId;
                updated = true;
            }
            if (!string.IsNullOrWhiteSpace(loginProvider))
            {
                user.LastLoginProvider = loginProvider;
                user.LastLoginAt = DateTimeOffset.UtcNow;
                updated = true;
            }
            if (updated)
            {
                _users.Update(user);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return true;
        }

        return false;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = await _users.GetAllAsync(cancellationToken);
        return users.Select(u =>
            new UserDto(
                u.Id,
                u.ObjectId,
                u.SubjectId,
                u.Email,
                u.Role,
                u.IsActive,
                u.LastLoginAt,
                u.LastLoginProvider
            )
        ).ToList();
    }

    public async Task<UserEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var tenantIds = await _userTenants.GetTenantIdsByUserIdAsync(user.Id, cancellationToken);
        return new UserEditDto(
            user.Id,
            user.ObjectId,
            user.SubjectId,
            user.Email,
            user.Role,
            user.IsActive,
            tenantIds
        );
    }

    public async Task<UserDto> SaveAsync(UserSaveRequest request, CancellationToken cancellationToken)
    {
        User entity;
        if (request.Id is null || request.Id == Guid.Empty)
        {
            entity = new User
            {
                ObjectId = request.ObjectId,
                SubjectId = request.SubjectId,
                Email = request.Email,
                Role = request.Role,
                IsActive = request.IsActive,
                LastLoginAt = null,
                LastLoginProvider = null
            };
            await _users.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity = await _users.GetByIdAsync(request.Id.Value, cancellationToken) ?? throw new InvalidOperationException("User not found.");
            entity.ObjectId = request.ObjectId;
            entity.SubjectId = request.SubjectId;
            entity.Email = request.Email;
            entity.Role = request.Role;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            _users.Update(entity);
        }

        await _userTenants.ReplaceAsync(entity.Id, request.TenantIds, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDto(
            entity.Id,
            entity.ObjectId,
            entity.SubjectId,
            entity.Email,
            entity.Role,
            entity.IsActive,
            entity.LastLoginAt,
            entity.LastLoginProvider
        );
    }

    public async Task<IReadOnlyList<UserDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var userIds = await _userTenants.GetUserIdsByTenantIdAsync(tenantId, cancellationToken);
        if (userIds.Count == 0)
            return Array.Empty<UserDto>();

        var users = await _users.GetByIdsAsync(userIds, cancellationToken);
        return users.Select(u => new UserDto(
            u.Id,
            u.ObjectId,
            u.SubjectId,
            u.Email,
            u.Role,
            u.IsActive,
            u.LastLoginAt,
            u.LastLoginProvider)).ToList();
    }

    public async Task<TenantMembershipDto?> GetTenantMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var membership = await _userTenants.GetMembershipAsync(userId, tenantId, cancellationToken);
        if (membership is null)
            return null;

        return new TenantMembershipDto(
            membership.UserId,
            membership.TenantId,
            membership.TenantRole,
            membership.CanGrant,
            membership.GrantedByUserId,
            membership.GrantedByUser?.Email,
            membership.GrantedAt);
    }

    public async Task<IReadOnlyList<Guid>> GetTenantIdsAsync(string? objectId, string? subjectId, string email, CancellationToken cancellationToken)
    {
        User? user = null;

        if (!string.IsNullOrWhiteSpace(objectId))
            user = await _users.GetByObjectIdAsync(objectId, cancellationToken);

        if (user is null && !string.IsNullOrWhiteSpace(subjectId))
            user = await _users.GetBySubjectIdAsync(subjectId, cancellationToken);

        if (user is null && !string.IsNullOrWhiteSpace(email))
            user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            return Array.Empty<Guid>();
        }

        var tenantIds = await _userTenants.GetTenantIdsByUserIdAsync(user.Id, cancellationToken);

        if (tenantIds.Count > 0)
            return tenantIds;

        // Fallback: include tenants that are visible to all users.
        var visibleToAll = await _tenants.GetAsync(
            new Harpyx.Application.Filters.TenantFilter { IncludeVisibleToAllUsers = true },
            cancellationToken);

        return visibleToAll
            .Where(t => t.IsVisibleToAllUsers)
            .Select(t => t.Id)
            .ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _users.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("User not found.");

        var tenantIds = await _userTenants.GetTenantIdsByUserIdAsync(id, cancellationToken);
        if (tenantIds.Count > 0)
        {
            var tenants = await _tenants.GetAsync(new Harpyx.Application.Filters.TenantFilter { Ids = tenantIds }, cancellationToken);
            var personalTenants = new List<Tenant>();

            foreach (var tenant in tenants.Where(t => t.IsPersonal))
            {
                var assignedUserIds = await _userTenants.GetUserIdsByTenantIdAsync(tenant.Id, cancellationToken);
                if (assignedUserIds.Count == 1 && assignedUserIds[0] == id)
                    personalTenants.Add(tenant);
            }

            foreach (var personalTenant in personalTenants)
            {
                var projects = await _projects.GetAllAsync(new[] { personalTenant.Id }, cancellationToken);
                foreach (var project in projects)
                {
                    var documents = await _documents.GetByProjectAsync(project.Id, cancellationToken);
                    foreach (var document in documents)
                    {
                        try
                        {
                            await _storage.DeleteAsync(document.StorageKey, cancellationToken);
                        }
                        catch
                        {
                            // Best effort cleanup: we still remove the tenant tree from DB.
                        }
                    }
                }

                _tenants.Remove(personalTenant);
            }
        }

        await _users.ClearOwnershipReferencesAsync(id, cancellationToken);
        var preferences = await _llmCatalog.GetPreferencesByUserAsync(id, cancellationToken);
        foreach (var preference in preferences)
        {
            _llmCatalog.RemovePreference(preference);
        }

        _users.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> ResolveUserIdAsync(string? objectId, string? subjectId, string email, CancellationToken cancellationToken)
    {
        User? user = null;

        if (!string.IsNullOrWhiteSpace(objectId))
            user = await _users.GetByObjectIdAsync(objectId, cancellationToken);

        if (user is null && !string.IsNullOrWhiteSpace(subjectId))
            user = await _users.GetBySubjectIdAsync(subjectId, cancellationToken);

        if (user is null && !string.IsNullOrWhiteSpace(email))
            user = await _users.GetByEmailAsync(email, cancellationToken);

        return user?.Id;
    }

    public async Task<UserRole?> ResolveRoleAsync(string? objectId, string? subjectId, string email, CancellationToken cancellationToken)
    {
        User? user = null;

        if (!string.IsNullOrWhiteSpace(objectId))
            user = await _users.GetByObjectIdAsync(objectId, cancellationToken);

        if (user is null && !string.IsNullOrWhiteSpace(subjectId))
            user = await _users.GetBySubjectIdAsync(subjectId, cancellationToken);

        if (user is null && !string.IsNullOrWhiteSpace(email))
            user = await _users.GetByEmailAsync(email, cancellationToken);

        return user?.Role;
    }
}
