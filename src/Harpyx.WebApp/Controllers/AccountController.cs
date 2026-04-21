using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Harpyx.WebApp.Security;

namespace Harpyx.WebApp.Controllers;

public class AccountController : Controller
{
    private readonly ITenantScopeService _tenantScope;

    public AccountController(ITenantScopeService tenantScope)
    {
        _tenantScope = tenantScope;
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult LoginEntra(string? returnUrl = "/")
    {
        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/" }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult LoginGoogle(string? returnUrl = "/")
    {
        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/" }, "Google");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // global logout (also disconnects from Google/MS)
        // return SignOut(new AuthenticationProperties { RedirectUri = "/" }, OpenIdConnectDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme);

        // safe logout (disconnects from app only)
        _tenantScope.ClearScope();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
