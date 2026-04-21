using System.Security.Claims;

namespace Harpyx.WebApp.Security;

public interface ITenantScopeService
{
    Task<TenantScopeContext> GetScopeAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<bool> SetCurrentTenantAsync(ClaimsPrincipal principal, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> SetAllTenantsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    void ClearScope();
}

