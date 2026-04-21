using Harpyx.Application.DTOs;
using Harpyx.Domain.Enums;

namespace Harpyx.Application.Interfaces;

public interface IUserService
{
    Task<bool> IsAuthorizedAsync(string? objectId, string? subjectId, string email, string? loginProvider, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken);
    Task<UserDto> SaveAsync(UserSaveRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<TenantMembershipDto?> GetTenantMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetTenantIdsAsync(string? objectId, string? subjectId, string email, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<Guid?> ResolveUserIdAsync(string? objectId, string? subjectId, string email, CancellationToken cancellationToken);
    Task<UserRole?> ResolveRoleAsync(string? objectId, string? subjectId, string email, CancellationToken cancellationToken);
}
