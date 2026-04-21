using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harpyx.WebApp.Pages;

public class WelcomeModel : PageModel
{
    public void OnGet()
    {
        Response.Cookies.Delete(OnboardingConstants.WelcomeCookieName);
    }
}
