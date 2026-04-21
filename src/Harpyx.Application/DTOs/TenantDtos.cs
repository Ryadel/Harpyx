namespace Harpyx.Application.DTOs;

public record TenantDto(
    Guid Id,
    string Name,
    bool IsActive,
    bool IsVisibleToAllUsers,
    bool IsPersonal = false,
    Guid? CreatedByUserId = null);

public record CreateTenantRequest(Guid? CreatedByUserId, string Name, bool IsActive, bool IsVisibleToAllUsers);
