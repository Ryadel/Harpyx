using Harpyx.Application.Interfaces;
using Harpyx.Application.Filters;
using Harpyx.Domain.Enums;
using Microsoft.Identity.Web;
using System.Security.Claims;

namespace Harpyx.WebApp.Security;

public class TenantScopeService : ITenantScopeService
{
    private const string ScopeCookieName = "harpyx_tenant_scope";
    private const string AllTenantsCookieValue = "all";

    private readonly IUserService _users;
    private readonly ITenantService _tenants;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantScopeService(IUserService users, ITenantService tenants, IHttpContextAccessor httpContextAccessor)
    {
        _users = users;
        _tenants = tenants;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TenantScopeContext> GetScopeAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var isAdmin = await IsAdminAsync(principal, cancellationToken);
        var accessibleTenantIds = await ResolveAccessibleTenantIdsAsync(principal, isAdmin, cancellationToken);
        var currentUserId = await _users.ResolveUserIdAsync(
            principal.GetObjectId(),
            principal.GetSubjectId(),
            principal.GetEmail(),
            cancellationToken);

        if (accessibleTenantIds.Count == 0)
        {
            return new TenantScopeContext(Array.Empty<Guid>(), Array.Empty<Guid>(), null, false, isAdmin, null, false);
        }

        var requestedScope = _httpContextAccessor.HttpContext?.Request.Cookies[ScopeCookieName];
        var isAllTenants = isAdmin && string.Equals(requestedScope, AllTenantsCookieValue, StringComparison.OrdinalIgnoreCase);

        Guid? currentTenantId = null;
        if (!isAllTenants)
        {
            if (Guid.TryParse(requestedScope, out var requestedTenantId) && accessibleTenantIds.Contains(requestedTenantId))
            {
                currentTenantId = requestedTenantId;
            }
            else
            {
                currentTenantId = accessibleTenantIds[0];
            }
        }

        var effectiveTenantIds = isAllTenants
            ? accessibleTenantIds
            : currentTenantId is Guid tenantId
                ? new[] { tenantId }
                : Array.Empty<Guid>();

        Domain.Enums.TenantRole? currentTenantRole = null;
        bool currentTenantCanGrant = false;
        if (!isAllTenants && currentTenantId is Guid selectedTenantId && currentUserId is Guid userId)
        {
            var membership = await _users.GetTenantMembershipAsync(userId, selectedTenantId, cancellationToken);
            currentTenantRole = membership?.TenantRole;
            currentTenantCanGrant = membership?.CanGrant == true;
        }

        return new TenantScopeContext(
            accessibleTenantIds,
            effectiveTenantIds,
            currentTenantId,
            isAllTenants,
            isAdmin,
            currentTenantRole,
            currentTenantCanGrant);
    }

    public async Task<bool> SetCurrentTenantAsync(ClaimsPrincipal principal, Guid tenantId, CancellationToken cancellationToken)
    {
        var isAdmin = await IsAdminAsync(principal, cancellationToken);
        var accessibleTenantIds = await ResolveAccessibleTenantIdsAsync(principal, isAdmin, cancellationToken);
        if (!accessibleTenantIds.Contains(tenantId))
            return false;

        var context = _httpContextAccessor.HttpContext;
        if (context is null)
            return false;

        context.Response.Cookies.Append(ScopeCookieName, tenantId.ToString("D"), BuildCookieOptions(context));
        return true;
    }

    public async Task<bool> SetAllTenantsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var isAdmin = await IsAdminAsync(principal, cancellationToken);
        if (!isAdmin)
            return false;

        var accessibleTenantIds = await ResolveAccessibleTenantIdsAsync(principal, isAdmin, cancellationToken);
        if (accessibleTenantIds.Count == 0)
            return false;

        var context = _httpContextAccessor.HttpContext;
        if (context is null)
            return false;

        context.Response.Cookies.Append(ScopeCookieName, AllTenantsCookieValue, BuildCookieOptions(context));
        return true;
    }

    public void ClearScope()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
            return;

        context.Response.Cookies.Delete(ScopeCookieName, BuildCookieOptions(context));
    }

    private async Task<IReadOnlyList<Guid>> ResolveAccessibleTenantIdsAsync(ClaimsPrincipal principal, bool isAdmin, CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            var tenants = await _tenants.GetAsync(new TenantFilter(), cancellationToken);
            return tenants.Select(t => t.Id).ToList();
        }

        return await _users.GetTenantIdsAsync(
            principal.GetObjectId(),
            principal.GetSubjectId(),
            principal.GetEmail(),
            cancellationToken);
    }

    private async Task<bool> IsAdminAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var role = await _users.ResolveRoleAsync(
            principal.GetObjectId(),
            principal.GetSubjectId(),
            principal.GetEmail(),
            cancellationToken);

        return role == UserRole.Admin;
    }

    private static CookieOptions BuildCookieOptions(HttpContext context)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            MaxAge = TimeSpan.FromDays(30)
        };
    }
}
