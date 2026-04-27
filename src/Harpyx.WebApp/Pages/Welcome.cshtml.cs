using Harpyx.Application.Interfaces;
using Harpyx.Domain.Enums;
using Harpyx.WebApp.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Identity.Web;

namespace Harpyx.WebApp.Pages;

public class WelcomeModel : PageModel
{
    private readonly IUserLlmProviderService _providerService;
    private readonly IUserService _userService;

    public WelcomeModel(IUserLlmProviderService providerService, IUserService userService)
    {
        _providerService = providerService;
        _userService = userService;
    }

    public bool HasRequiredLlmProviders { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Response.Cookies.Delete(OnboardingConstants.WelcomeCookieName);

        var userId = await _userService.ResolveUserIdAsync(
            User.GetObjectId(),
            User.GetSubjectId(),
            User.GetEmail(),
            HttpContext.RequestAborted);

        if (userId is null)
        {
            return RedirectToPage("/AccessDenied");
        }

        var chatProviders = await _providerService.GetConfiguredByUsersAsync(
            [userId.Value],
            LlmProviderType.Chat,
            HttpContext.RequestAborted);
        var ragProviders = await _providerService.GetConfiguredByUsersAsync(
            [userId.Value],
            LlmProviderType.RagEmbedding,
            HttpContext.RequestAborted);

        HasRequiredLlmProviders = chatProviders.Count > 0 && ragProviders.Count > 0;
        return Page();
    }
}
