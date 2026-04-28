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
    private readonly ILogger<AccountController> _logger;

    public AccountController(ITenantScopeService tenantScope, ILogger<AccountController> logger)
    {
        _tenantScope = tenantScope;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult LoginEntra(string? returnUrl = "/")
    {
        var redirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        _logger.LogInformation(
            "Starting external sign-in. Provider: entra; Scheme: {Scheme}; RedirectUriAfterExternalLogin: {RedirectUri}; Request: {RequestScheme}://{RequestHost}{RequestPath}",
            OpenIdConnectDefaults.AuthenticationScheme,
            redirectUri,
            Request.Scheme,
            Request.Host.Value,
            Request.Path.Value);
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, OpenIdConnectDefaults.AuthenticationScheme);
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
