using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Harpyx.WebApp.Controllers;

[Authorize]
[DisableRateLimiting]
public class TenantScopeController : Controller
{
    private readonly ITenantScopeService _tenantScope;

    public TenantScopeController(ITenantScopeService tenantScope)
    {
        _tenantScope = tenantScope;
    }

    [HttpGet]
    public Task<IActionResult> Switch(Guid? tenantId, bool all = false, string? returnUrl = null)
        => SwitchInternalAsync(tenantId, all, returnUrl);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Switch")]
    public Task<IActionResult> SwitchPost(Guid? tenantId, bool all = false, string? returnUrl = null)
        => SwitchInternalAsync(tenantId, all, returnUrl);

    private async Task<IActionResult> SwitchInternalAsync(Guid? tenantId, bool all, string? returnUrl)
    {
        var changed = false;
        if (all)
        {
            changed = await _tenantScope.SetAllTenantsAsync(User, HttpContext.RequestAborted);
        }
        else if (tenantId is Guid requestedTenantId)
        {
            changed = await _tenantScope.SetCurrentTenantAsync(User, requestedTenantId, HttpContext.RequestAborted);
        }

        if (!changed)
            return Forbid();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToPage("/Index");
    }
}
