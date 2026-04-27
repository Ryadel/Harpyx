using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harpyx.WebApp.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Mode { get; set; }

    public bool IsSignupMode =>
        string.Equals(Mode, "signup", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Mode, "register", StringComparison.OrdinalIgnoreCase);

    public string SafeReturnUrl => Url.IsLocalUrl(ReturnUrl)
        ? ReturnUrl!
        : "/Workspaces/Index";
}
